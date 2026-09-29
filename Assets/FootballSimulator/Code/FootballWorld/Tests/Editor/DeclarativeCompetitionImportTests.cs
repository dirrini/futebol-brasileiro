using System.IO;
using System.Linq;
using FStudio.FootballWorld.Infrastructure.Importing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FStudio.FootballWorld.Tests
{
    public sealed class DeclarativeCompetitionImportTests
    {
        [Test]
        public void V6MapsReusableFormatsEligibilityAssetsPrizesAndStageDatesWithoutMutableProgress()
        {
            var json = Valid(); var result = Import(json); Success(result);
            var format = result.Catalog.CompetitionFormats.Single(); var edition = result.Catalog.CompetitionEditions.Single();
            Assert.That(edition.Format, Is.SameAs(format)); Assert.That(format.MatchRules.MaxSubstitutions, Is.EqualTo(5));
            Assert.That(format.Stages[0].Points.Win, Is.EqualTo(3)); Assert.That(format.Stages[1].AwayGoals, Is.True);
            Assert.That(edition.StageSchedules[1].FixtureDates[0].ToString(), Is.EqualTo("2026-10-25"));
            var competition = result.Catalog.Competitions.Single();
            Assert.That(competition.Eligibility.StateCodes.Single(), Is.EqualTo("SP"));
            Assert.That(competition.TrophyModelUri, Is.EqualTo("models/trophy.glb")); Assert.That(competition.Prizes.Win, Is.EqualTo(30));
            Assert.That(result.Catalog.Clubs[0].StateCode, Is.EqualTo("SP"));
            json["competitionFormats"][0]["stages"][0]["points"]["win"] = 9;
            Assert.That(format.Stages[0].Points.Win, Is.EqualTo(3), "Imported definitions own immutable snapshots.");
        }

        [TestCase("competitionFormats[0].version", "2", "unsupported_rule_version")]
        [TestCase("competitionFormats[0].stages[0].kind", "\"script\"", "unsupported_rule_value")]
        [TestCase("competitionFormats[0].stages[0].points.win", "1.0", "invalid_type")]
        [TestCase("competitionEditions[0].formatId", "\"missing\"", "unknown_reference")]
        [TestCase("competitionEditions[0].stageSchedules[1].fixtureDates[0]", "\"2026-02-30\"", "invalid_date")]
        [TestCase("clubs[0].stateCode", "\"RJ\"", "ineligible_club")]
        [TestCase("competitions[0].trophyModelUri", "\"../trophy.glb\"", "invalid_media_uri")]
        [TestCase("competitions[0].logoUri", "\"https://user:secret@example.com/a.png\"", "invalid_media_uri")]
        public void UnsupportedDeclarationsAndBrokenReferencesAreRejectedBeforeMapping(string path, string value, string code)
        {
            var json = Valid(); json.SelectToken(path).Replace(new JRaw(value)); var result = Import(json);
            Assert.That(result.Success, Is.False); Assert.That(result.Errors.Any(e => e.Code == code), Is.True, Errors(result));
        }
        [Test]
        public void RejectsInvalidTransitionsUnknownFieldsAndUnsupportedRuleCombinations()
        {
            foreach (var change in new[] {"future-ranking", "too-many-qualifiers", "neutral-away-goals", "unknown-field", "mixed-edition"})
            {
                var json = Valid();
                if (change == "future-ranking") json["competitionFormats"][0]["stages"][0]["qualification"]["rankingStageIds"] = new JArray("final");
                if (change == "too-many-qualifiers") json["competitionFormats"][0]["stages"][0]["qualification"]["count"] = 5;
                if (change == "neutral-away-goals") json["competitionFormats"][0]["stages"][1]["venue"] = "neutral";
                if (change == "unknown-field") json["competitionFormats"][0]["stages"][0]["script"] = "eval()";
                if (change == "mixed-edition") json["competitionEditions"][0]["roundDates"] = new JArray("2026-10-03");
                Assert.That(Import(json).Success, Is.False, change);
            }
        }
        [Test]
        public void V6KeepsLegacyEditionsAndRequiresTheFormatCollection()
        {
            var json = Valid(); json["competitionFormats"] = new JArray(); json["competitions"] = new JArray(JObject.Parse("{'id':'competition-test','name':'Test'}"));
            json["competitionEditions"] = new JArray(JObject.Parse(@"{'id':'edition-test','competitionId':'competition-test','name':'Test','participantClubIds':[],
                'roundDates':['2026-10-03','2026-10-10','2026-10-17'],'rules':{'type':'round-robin','version':1,'legs':1,'points':{'win':3,'draw':1,'loss':0},'tieBreakers':['wins','goal-difference','goals-for']}}"));
            json["competitionEditions"][0]["participantClubIds"] = new JArray(json["clubs"].Select(c => c["id"].DeepClone()));
            Success(Import(json)); json.Property("competitionFormats").Remove(); Assert.That(Import(json).Success, Is.False);
        }
        [Test]
        public void ExplicitNoChampionAllowsAQualificationOnlyFinalStage()
        {
            var json = Valid(); json["competitionFormats"][0]["championStageId"] = JValue.CreateNull();
            var result = Import(json); Success(result); Assert.That(result.Catalog.CompetitionFormats[0].ChampionStageId, Is.Null);
        }
        [Test]
        public void WinnersAndLosersCanRunParallelCalendarsWithOnlyTheNamedFinalGrantingTheTitle()
        {
            var json = Valid(); var format = json["competitionFormats"][0]; var semi = format["stages"][0];
            semi["kind"] = "knockout"; semi["roundCount"] = 1; semi["venue"] = "seeded";
            semi["qualification"] = JObject.Parse("{'mode':'winners','count':2}");
            format["championStageId"] = "final";
            var bronze = (JObject)format["stages"][1].DeepClone(); bronze["id"] = "bronze"; bronze["name"] = "Additional qualification";
            bronze["legs"] = 1; bronze["roundCount"] = 1; bronze["awayGoals"] = false;
            bronze["qualification"] = JObject.Parse("{'mode':'winners','count':1}");
            bronze["source"] = JObject.Parse("{'stageId':'league','selection':'losers'}"); ((JArray)format["stages"]).Add(bronze);
            json["competitionEditions"][0]["stageSchedules"][0]["roundDates"] = new JArray("2026-10-03");
            ((JArray)json["competitionEditions"][0]["stageSchedules"]).Add(JObject.Parse("{'stageId':'bronze','roundDates':['2026-10-24'],'groups':[],'authoredFixtures':[]}"));
            var result = Import(json); Success(result);
            Assert.That(result.Catalog.CompetitionFormats[0].ChampionStageId, Is.EqualTo("final"));
            Assert.That(result.Catalog.CompetitionFormats[0].Stages[2].Source.Selection, Is.EqualTo("losers"));
            bronze["qualification"]["rankingStageIds"] = new JArray("final"); Assert.That(Import(json).Success, Is.False, "A sibling branch is not part of this stage's accumulated campaign.");
        }
        [Test]
        public void RejectsDanglingRoutesAndPrizesWithoutReturningPartialCatalogs()
        {
            var json = Valid(); json["competitions"][0]["qualificationRoutes"] = JArray.Parse("[{'outcomeId':'relegated','targetCompetitionId':'missing'}]");
            var result = Import(json); Assert.That(result.Catalog, Is.Null); Assert.That(result.Errors.Any(e => e.Code == "unknown_reference"), Is.True);
            json = Valid(); json["competitions"][0]["prizes"]["rankingAwards"][0]["stageId"] = "missing";
            Assert.That(Import(json).Success, Is.False);
        }
        private static DatabaseImportResult Import(JObject document) => new JsonDatabaseImporter().Import(document.ToString(Formatting.None));
        private static string Errors(DatabaseImportResult result) => string.Join("; ", result.Errors.Select(e => e.Code + " " + e.Path + ": " + e.Message));
        private static void Success(DatabaseImportResult result) => Assert.That(result.Success, Is.True, Errors(result));
        private static JObject Valid()
        {
            var json = JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "FootballSimulator/Code/FootballWorld/Tests/Fixtures/legacy-four-clubs.database.json")));
            json["schemaVersion"] = 6;
            foreach (var club in json["clubs"]) { club["countryCode"] = "BR"; club["city"] = "São Paulo"; club["stateCode"] = "SP"; }
            json["countries"] = JArray.Parse("[{'code':'BR','name':'Brasil'}]");
            json["stadiums"] = JArray.Parse("[{'id':'stadium-test','name':'Estádio','countryCode':'BR','city':'São Paulo'}]");
            json["snapshot"] = JObject.Parse("{'date':'2026-01-11','label':'Test','rosterScope':'matchday-squads','notes':'','sources':[{'id':'source-test','title':'Source','url':'https://example.com/source.pdf'}]}");
            json["competitions"] = JArray.Parse(@"[{'id':'competition-test','name':'Test','defaultFormatId':'format-test','level':'state','reputation':70,'prizeLevel':40,
                'logoUri':'media/league.png','trophyImageUri':'https://example.com/trophy.png','trophyModelUri':'models/trophy.glb',
                'eligibility':{'countryCodes':['BR'],'stateCodes':['SP'],'allowedClubIds':[],'excludedClubIds':[]},
                'prizes':{'currency':'BRL','participation':100,'win':30,'draw':10,'rankingAwards':[{'stageId':'final','ranking':'overall','fromRank':1,'toRank':1,'amount':500}]}}]");
            json["competitionFormats"] = JArray.Parse(@"[{'id':'format-test','name':'Liga e final','version':1,'participantCount':4,'matchRules':{'maxSubstitutions':5},
                'stages':[{'id':'league','name':'Liga','kind':'league','groupCount':1,'opponents':'all','legs':1,'roundCount':3,'points':{'win':3,'draw':1,'loss':0},
                'tieBreakers':['wins','goal-difference','goals-for','seeded-draw'],'qualification':{'mode':'overall','count':2},'pairing':'seeded','venue':'first-listed','awayGoals':false,'tiedWinner':'penalties'},
                {'id':'final','name':'Final','kind':'knockout','groupCount':1,'opponents':'all','legs':2,'roundCount':2,'points':{'win':3,'draw':1,'loss':0},
                'tieBreakers':['wins','goal-difference','goals-for','seeded-draw'],'qualification':{'mode':'winners','count':1,'rankingStageIds':['league','final']},'pairing':'seeded','venue':'seeded','awayGoals':true,'tiedWinner':'penalties'}],
                'outcomes':[{'id':'relegated','label':'Rebaixados','stageId':'league','kind':'relegation','ranking':'overall','fromRank':4,'toRank':4}]}]");
            json["competitionEditions"] = JArray.Parse(@"[{'id':'edition-test','competitionId':'competition-test','name':'Test','formatId':'format-test','participantClubIds':[],
                'stageSchedules':[{'stageId':'league','roundDates':['2026-10-03','2026-10-10','2026-10-17'],'groups':[],'authoredFixtures':[]},
                {'stageId':'final','roundDates':['2026-10-24','2026-10-31'],'groups':[],'authoredFixtures':[],'fixtureDates':['2026-10-25','2026-10-31']}]}]");
            json["competitionEditions"][0]["participantClubIds"] = new JArray(json["clubs"].Select(c => c["id"].DeepClone())); return json;
        }
    }
}
