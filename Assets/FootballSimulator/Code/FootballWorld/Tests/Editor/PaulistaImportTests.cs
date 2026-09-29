using System;
using System.IO;
using System.Linq;
using FStudio.FootballWorld.Infrastructure.Importing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FStudio.FootballWorld.Tests
{
    public sealed class PaulistaImportTests
    {
        [Test]
        public void V5MapsAuthoredFixturesPlayoffsAndVenueExceptionsWithoutChangingClubHome()
        {
            var document = Valid();
            document["clubs"][0]["stadiumId"] = "stadium-main";
            Edition(document)["authoredFixtures"][0]["stadiumId"] = "stadium-alternate";
            var result = Import(document); Success(result);
            var edition = result.Catalog.CompetitionEditions.Single();
            Assert.That(edition.Rules.IsPaulista2026, Is.True);
            Assert.That(edition.ScheduledFixtures.Count, Is.EqualTo(64));
            Assert.That(edition.PlayoffDates.Count, Is.EqualTo(8));
            Assert.That(edition.StartDate.ToString(), Is.EqualTo("2026-01-10"));
            Assert.That(edition.EndDate.ToString(), Is.EqualTo("2026-03-08"));
            Assert.That(edition.ScheduledFixtures[0].StadiumId, Is.EqualTo("stadium-alternate"));
            Assert.That(result.Catalog.Clubs[0].StadiumId, Is.EqualTo("stadium-main"));
            Assert.That(edition.PlayoffDates[0].CompareTo(edition.PlayoffDates[1]), Is.GreaterThan(0), "Bracket order is not calendar order.");
            Edition(document)["authoredFixtures"][0]["date"] = "2026-01-12";
            Assert.That(edition.ScheduledFixtures[0].Date.ToString(), Is.EqualTo("2026-01-10"));
        }

        [TestCase("authoredFixtures[0].homeClubId", "\"missing\"", "unknown_reference")]
        [TestCase("authoredFixtures[0].date", "\"2026-01-14\"", "invalid_fixture_date")]
        [TestCase("authoredFixtures[0].date", "\"2026-02-30\"", "invalid_date")]
        [TestCase("authoredFixtures[0].round", "0", "out_of_range")]
        [TestCase("authoredFixtures[0].round", "9", "out_of_range")]
        [TestCase("authoredFixtures[0].round", "1.0", "invalid_type")]
        [TestCase("playoffDates[4]", "\"2026-02-21\"", "invalid_date_order")]
        [TestCase("playoffDates[6]", "\"2026-03-01\"", "invalid_date_order")]
        [TestCase("playoffDates[7]", "\"2026-03-04\"", "invalid_date_order")]
        [TestCase("rules.points.win", "2", "unsupported_rule_parameters")]
        [TestCase("rules.version", "2", "unsupported_rule_version")]
        [TestCase("rules.tieBreakers[3]", "\"yellow-cards\"", "unsupported_tiebreaker")]
        public void InvalidAuthoredCalendarsAreRejectedBeforeActivation(string path, string value, string code)
        {
            var document = Valid(); Edition(document).SelectToken(path).Replace(new JRaw(value));
            Failure(Import(document), code);
        }

        [Test]
        public void RequiresAllCalendarFieldsAndRejectsVenueOrResultDataThatCannotBeResolved()
        {
            foreach (var field in new[] {"authoredFixtures", "playoffDates"})
            {
                var document = Valid(); Edition(document).Property(field).Remove(); Failure(Import(document), "required");
            }
            var unknown = Valid(); Edition(unknown)["authoredFixtures"][0]["stadiumId"] = "unknown";
            Failure(Import(unknown), "unknown_reference");
            var results = Valid(); Edition(results)["authoredFixtures"][0]["homeGoals"] = 2;
            Failure(Import(results), "unknown_property");
            var oldVersion = Valid(); oldVersion["schemaVersion"] = 4;
            Failure(Import(oldVersion), "unsupported_rule_type");
        }

        [Test]
        public void RejectsDuplicateIdsOpponentsRoundAppearancesAndUnbalancedHomeSchedule()
        {
            var duplicate = Valid(); Edition(duplicate)["authoredFixtures"][1]["id"] = "fixture-1-0";
            Failure(Import(duplicate), "duplicate_id");
            var round = Valid(); Edition(round)["authoredFixtures"][1]["homeClubId"] = "club-0";
            Failure(Import(round), "duplicate_round_club");
            var repeated = Valid(); Edition(repeated)["authoredFixtures"][8]["homeClubId"] = "club-8";
            Failure(Import(repeated), "duplicate_opponents");
            var unbalanced = Valid(); var fixture = Edition(unbalanced)["authoredFixtures"][0];
            var home = (string)fixture["homeClubId"]; fixture["homeClubId"] = fixture["awayClubId"].DeepClone(); fixture["awayClubId"] = home;
            Failure(Import(unbalanced), "invalid_club_schedule");
        }

        private static JObject Edition(JObject document) => (JObject)document["competitionEditions"][0];
        private static DatabaseImportResult Import(JObject document) => new JsonDatabaseImporter().Import(document.ToString(Formatting.None));
        private static void Success(DatabaseImportResult result) => Assert.That(result.Success, Is.True, string.Join("; ", result.Errors.Select(error => error.Path + ": " + error.Message)));
        private static void Failure(DatabaseImportResult result, string code)
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors.Any(error => error.Code == code), Is.True, string.Join("; ", result.Errors.Select(error => error.Code + " " + error.Path)));
        }

        private static JObject Valid()
        {
            var document = JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
                "FootballSimulator/Code/FootballWorld/Tests/Fixtures/legacy-four-clubs.database.json")));
            document["schemaVersion"] = 5;
            var clubs = new JArray();
            for (var i = 0; i < 16; i++) clubs.Add(new JObject { ["id"] = "club-" + i, ["name"] = "Club " + i, ["countryCode"] = "BR", ["city"] = "São Paulo" });
            document["clubs"] = clubs; document["memberships"] = new JArray();
            document["countries"] = new JArray(JObject.Parse("{'code':'BR','name':'Brasil'}"));
            document["stadiums"] = new JArray(JObject.Parse("{'id':'stadium-main','name':'Principal','countryCode':'BR','city':'São Paulo'}"),
                JObject.Parse("{'id':'stadium-alternate','name':'Alternativo','countryCode':'BR','city':'São Paulo'}"));
            document["snapshot"] = JObject.Parse("{'date':'2026-01-11','label':'Test','rosterScope':'matchday-squads','notes':'','sources':[{'id':'source-a','title':'Source','url':'https://example.com/source.pdf'}]}");
            document["competitions"] = new JArray(JObject.Parse("{'id':'competition-a','name':'Competition'}"));
            var dates = new[] {"2026-01-10", "2026-01-14", "2026-01-17", "2026-01-20", "2026-01-24", "2026-01-28", "2026-02-07", "2026-02-15"};
            var fixtures = new JArray();
            for (var round = 1; round <= 8; round++) for (var i = 0; i < 8; i++)
            {
                var first = "club-" + i; var second = "club-" + (8 + (i + round - 1) % 8);
                fixtures.Add(new JObject { ["id"] = "fixture-" + round + "-" + i, ["round"] = round, ["date"] = dates[round - 1],
                    ["homeClubId"] = round % 2 == 1 ? first : second, ["awayClubId"] = round % 2 == 1 ? second : first });
            }
            document["competitionEditions"] = new JArray(new JObject {
                ["id"] = "edition-a", ["competitionId"] = "competition-a", ["name"] = "Edition", ["participantClubIds"] = new JArray(clubs.Select(club => club["id"].DeepClone())),
                ["roundDates"] = new JArray(dates), ["authoredFixtures"] = fixtures,
                ["playoffDates"] = new JArray("2026-02-22", "2026-02-21", "2026-02-21", "2026-02-22", "2026-02-28", "2026-03-01", "2026-03-04", "2026-03-08"),
                ["rules"] = JObject.Parse("{'type':'paulista-2026','version':1,'legs':1,'points':{'win':3,'draw':1,'loss':0},'tieBreakers':['wins','goal-difference','goals-for','red-cards','yellow-cards','drawing-lots']}")
            });
            return document;
        }
    }
}
