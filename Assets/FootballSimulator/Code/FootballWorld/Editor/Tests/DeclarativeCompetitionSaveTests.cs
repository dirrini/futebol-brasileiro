#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.GameModes;
using FStudio.FootballWorld.Infrastructure.Importing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FStudio.FootballWorld.Editor.Tests
{
    public sealed class DeclarativeCompetitionSaveTests
    {
        [Test]
        public void DeclarativeFixtureIdentityAndNeutralVenueSurviveSaveAndRejectTampering()
        {
            var source = CupSource();
            var imported = Import(source);
            var club = imported.Catalog.CompetitionEditions[0].ParticipantClubIds[0];
            var season = CompetitionSession.Create(imported.Catalog, "award-edition", "cup-save", club);
            season.SimulateFixture(season.NextFixture.Id);
            var json = GameSaveCodec.Championship(season, source);
            var restored = GameSaveCodec.RestoreChampionship(json).Session;
            Assert.That(restored.Fixtures.All(value => value.StageId == "final" && value.IsNeutral), Is.True);
            Assert.That(restored.Fixtures.Select(value => value.Leg), Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(restored.Results.Count, Is.EqualTo(1));
            Assert.That(restored.IsComplete, Is.False);
            var tampered = JObject.Parse(json);
            tampered["fixtures"][0]["isNeutral"] = false;
            Assert.That(() => GameSaveCodec.RestoreChampionship(tampered.ToString()), Throws.Exception);
            tampered = JObject.Parse(json);
            tampered["fixtures"][0]["stageId"] = "different-stage";
            Assert.That(() => GameSaveCodec.RestoreChampionship(tampered.ToString()), Throws.Exception);
        }

        [Test]
        public void AwardsPayOnceAtTheirActualDateAndNeutralMatchDoesNotCreateHomeGate()
        {
            var source = CupSource();
            var imported = Import(source);
            var club = imported.Catalog.CompetitionEditions[0].ParticipantClubIds[0];
            var career = CareerSession.Create(imported.Catalog, "award-edition", "prize-career", club, new GameDate(2026, 1, 1));
            var initial = career.Ledger.Single(value => value.EventKey == "opening-balance").Amount;
            Assert.That(career.FinanceBalance, Is.EqualTo(initial + 1000));
            career.AdvanceToNextFixture();
            CompleteControlledWin(career);
            Assert.That(career.FinanceBalance, Is.EqualTo(initial + 1100));
            var first = Restore(career, source);
            Assert.That(first.FinanceBalance, Is.EqualTo(initial + 1100));
            Assert.That(first.Ledger.Any(value => value.EventKey == "prize-ranking"), Is.False);
            first.AdvanceToNextFixture();
            CompleteControlledWin(first);
            first.ReconcileResults(); first.ReconcileResults();
            Assert.That(first.FinanceBalance, Is.EqualTo(initial + 1700));
            Assert.That(first.Ledger.Count(value => value.EventKey == "prize-ranking"), Is.EqualTo(1));
            Assert.That(first.Ledger.Any(value => value.EventKey == "home-match-income"), Is.False);
            var twice = Restore(Restore(first, source), source);
            Assert.That(twice.FinanceBalance, Is.EqualTo(first.FinanceBalance));
            Assert.That(twice.News.Count, Is.EqualTo(first.News.Count));
            Assert.That(twice.Competition.ChampionClubId, Is.EqualTo(club));
        }

        [Test]
        public void CareerRejectsAwardCurrencyItCannotConvert()
        {
            var root = JObject.Parse(CupSource());
            root["competitions"][0]["prizes"]["currency"] = "ZZZ";
            var imported = Import(root.ToString());
            Assert.That(() => CareerSession.Create(imported.Catalog, "award-edition", "currency", imported.Catalog.Clubs[0].Id,
                new GameDate(2026, 1, 1)), Throws.ArgumentException);
        }

        [TestCase(0)]
        [TestCase(4)]
        public void ParallelPlayoffSaveRoundTripsEveryStepWithoutReplacingTheChampion(int clubIndex)
        {
            var root = JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
                "FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json")));
            const string formatId = "format-cup-losing-playoff";
            var edition = (JObject)root["competitionEditions"][0];
            edition["formatId"] = formatId;
            edition["participantClubIds"] = new JArray(edition["participantClubIds"].Take(8));
            root["competitions"][0]["defaultFormatId"] = formatId;
            edition["stageSchedules"] = JArray.Parse(@"[
                {'stageId':'quarterfinal','roundDates':['2026-01-10'],'groups':[],'authoredFixtures':[]},
                {'stageId':'semifinal','roundDates':['2026-01-17','2026-01-24'],'groups':[],'authoredFixtures':[]},
                {'stageId':'final','roundDates':['2026-01-31'],'groups':[],'authoredFixtures':[]},
                {'stageId':'promotion-playoff','roundDates':['2026-01-17'],'groups':[],'authoredFixtures':[]}]");
            var source = root.ToString(Formatting.None);
            var catalog = Import(source).Catalog;
            var season = CompetitionSession.Create(catalog, (string)edition["id"], "parallel-codec-" + clubIndex,
                (string)edition["participantClubIds"][clubIndex]);
            var steps = 0;
            while (!season.IsComplete && steps++ < 30)
            {
                if (season.NextFixture != null) season.SimulateFixture(season.NextFixture.Id);
                else season.SimulateNextRound();
                season = GameSaveCodec.RestoreChampionship(GameSaveCodec.Championship(season, source)).Session;
            }
            Assert.That(season.IsComplete, Is.True);
            Assert.That(season.QualifiedOutcomes.Count(value => value.Kind == "promotion"), Is.EqualTo(2));
            Assert.That(season.GetStageStandings("final").First().ClubId, Is.EqualTo(season.ChampionClubId));
            Assert.That(season.GetStageStandings("promotion-playoff").Select(value => value.ClubId), Does.Not.Contain(season.ChampionClubId));
        }

        private static void CompleteControlledWin(CareerSession career)
        {
            var fixture = career.Competition.NextFixture;
            var execution = career.BeginFixture();
            var home = fixture.HomeClubId == career.Competition.ControlledClubId;
            career.CompleteFixture(new FixtureResult(fixture.Id, execution.ExecutionId, fixture.HomeClubId, fixture.AwayClubId,
                home ? 2 : 0, home ? 0 : 2));
        }

        private static CareerSession Restore(CareerSession career, string source)
        {
            var catalog = career.Competition.Catalog;
            var club = catalog.GetClub(career.Competition.ControlledClubId);
            var profile = new HubCareerProfile("Test coach", "coach-1", 1, 2026, club.Id, club.Name, catalog.DatabaseId, catalog.DatabaseRevision);
            return GameSaveCodec.RestoreDailyCareer(GameSaveCodec.DailyCareer(profile, career, source)).Session;
        }

        private static DatabaseImportResult Import(string source)
        {
            var result = new JsonDatabaseImporter().Import(source);
            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors.Select(value => value.Message)));
            return result;
        }

        private static string CupSource()
        {
            var root = JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
                "FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json")));
            root["schemaVersion"] = 6;
            var clubs = root["clubs"].Take(2).Select(value => (string)value["id"]).ToArray();
            root["competitionFormats"] = JArray.Parse(@"[{ 'id':'award-format','name':'Neutral cup','version':1,'participantCount':2,
                'matchRules':{'maxSubstitutions':4}, 'stages':[{'id':'final','name':'Final','kind':'knockout','groupCount':1,
                'opponents':'all','legs':2,'roundCount':2,'points':{'win':3,'draw':1,'loss':0},
                'tieBreakers':['wins','goal-difference','goals-for','seeded-draw'],
                'qualification':{'mode':'winners','count':1,'rankingStageIds':[]},'pairing':'seeded',
                'venue':'neutral','awayGoals':false,'tiedWinner':'penalties'}],'outcomes':[]}]");
            root["competitions"] = JArray.Parse(@"[{'id':'award-competition','name':'Award cup','defaultFormatId':'award-format',
                'prizes':{'currency':'BRL','participation':1000,'win':100,'draw':40,
                'rankingAwards':[{'stageId':'final','ranking':'overall','fromRank':1,'toRank':2,'amount':500}]}}]");
            root["competitionEditions"] = new JArray(new JObject {
                ["id"] = "award-edition", ["competitionId"] = "award-competition", ["name"] = "Award season", ["formatId"] = "award-format",
                ["participantClubIds"] = new JArray(clubs), ["stageSchedules"] = new JArray(new JObject {
                    ["stageId"] = "final", ["roundDates"] = new JArray("2026-01-10", "2026-01-12"),
                    ["groups"] = new JArray(), ["authoredFixtures"] = new JArray(), ["neutralStadiumId"] = (string)root["stadiums"][0]["id"] })
            });
            return root.ToString(Formatting.None);
        }
    }
}
#endif
