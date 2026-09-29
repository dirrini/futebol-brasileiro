using System;
using System.IO;
using System.Linq;
using FStudio.FootballWorld.Infrastructure.Importing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FStudio.FootballWorld.Tests
{
    public sealed class CompetitionImportTests
    {
        private readonly JsonDatabaseImporter importer = new JsonDatabaseImporter();

        [Test]
        public void LegacyVersionsRemainValidWithoutCompetitionCollections()
        {
            foreach (var version in new[] {1, 2})
            {
                var document = Legacy();
                document["schemaVersion"] = version;
                var result = Import(document);
                AssertSuccess(result);
                Assert.That(result.Catalog.Competitions, Is.Empty);
                Assert.That(result.Catalog.CompetitionEditions, Is.Empty);
                document["competitions"] = new JArray();
                AssertFailure(Import(document), "unknown_property", "$.competitions");
            }
        }

        [Test]
        public void VersionThreeMapsDatedRulesWithoutAddingSeasonProgressToAuthoredData()
        {
            var document = Valid();
            var result = Import(document);
            AssertSuccess(result);
            var edition = result.Catalog.CompetitionEditions.Single();
            Assert.That(edition.Id, Is.EqualTo("edition-test"));
            Assert.That(edition.CompetitionId, Is.EqualTo("competition-test"));
            Assert.That(edition.Name, Is.EqualTo("Example edition"));
            Assert.That(edition.ParticipantClubIds.Count, Is.EqualTo(4));
            Assert.That(edition.StartDate.ToString(), Is.EqualTo("2026-10-03"));
            Assert.That(edition.EndDate.ToString(), Is.EqualTo("2026-10-17"));
            Assert.That(edition.Rules.Legs, Is.EqualTo(1));
            Assert.That(edition.Rules.WinPoints, Is.EqualTo(3));
            Assert.That(edition.Rules.DrawPoints, Is.EqualTo(1));
            Assert.That(edition.Rules.LossPoints, Is.EqualTo(0));
            Assert.That(result.Catalog.Players.Count, Is.EqualTo(44));
            document["competitionEditions"][0]["results"] = new JArray();
            AssertFailure(Import(document), "unknown_property", "$.competitionEditions[0].results");
        }

        [Test]
        public void DoubleRoundRobinRequiresTwiceAsManyAuthoredRoundDates()
        {
            var document = Valid();
            document["competitionEditions"][0]["rules"]["legs"] = 2;
            AssertFailure(Import(document), "invalid_round_count", "$.competitionEditions[0].roundDates");
            var dates = (JArray)document["competitionEditions"][0]["roundDates"];
            dates.Add("2026-10-24"); dates.Add("2026-10-31"); dates.Add("2026-11-07");
            AssertSuccess(Import(document));
        }

        [TestCase("competitions", "null", "invalid_type")]
        [TestCase("competitionEditions", "null", "invalid_type")]
        [TestCase("competitionEditions[0].competitionId", "\"missing\"", "unknown_reference")]
        [TestCase("competitionEditions[0].participantClubIds[0]", "\"missing\"", "unknown_reference")]
        [TestCase("competitionEditions[0].roundDates[0]", "\"2026-02-29\"", "invalid_date")]
        [TestCase("competitionEditions[0].roundDates[0]", "\"1900-02-29\"", "invalid_date")]
        [TestCase("competitionEditions[0].roundDates[0]", "\"0000-01-01\"", "invalid_date")]
        [TestCase("competitionEditions[0].roundDates[0]", "\"2026-2-03\"", "invalid_date")]
        [TestCase("competitionEditions[0].roundDates[0]", "\"2026-10-03T00:00:00Z\"", "invalid_date")]
        [TestCase("competitionEditions[0].roundDates[0]", "null", "invalid_type")]
        [TestCase("competitionEditions[0].roundDates[1]", "\"2026-10-03\"", "invalid_date_order")]
        [TestCase("competitionEditions[0].rules.type", "\"knockout\"", "unsupported_rule_type")]
        [TestCase("competitionEditions[0].rules.version", "2", "unsupported_rule_version")]
        [TestCase("competitionEditions[0].rules.legs", "3", "out_of_range")]
        [TestCase("competitionEditions[0].rules.legs", "1.0", "invalid_type")]
        [TestCase("competitionEditions[0].rules.points.win", "101", "out_of_range")]
        [TestCase("competitionEditions[0].rules.points.loss", "-1", "out_of_range")]
        [TestCase("competitionEditions[0].rules.tieBreakers[0]", "\"head-to-head\"", "unsupported_tiebreaker")]
        public void RejectsUnsupportedRulesInvalidDatesAndReferences(string path, string value, string code)
        {
            var document = Valid();
            document.SelectToken(path).Replace(new JRaw(value));
            AssertFailure(Import(document), code, "$." + path);
        }

        [TestCase("competitions")]
        [TestCase("competitionEditions")]
        [TestCase("competitionEditions[0].rules.points.win")]
        [TestCase("competitionEditions[0].roundDates")]
        public void RejectsMissingVersionThreeFields(string path)
        {
            var document = Valid();
            ((JProperty)document.SelectToken(path).Parent).Remove();
            AssertFailure(Import(document), "required", "$." + path);
        }

        [Test]
        public void RejectsDuplicateEditionsCompetitionsAndParticipants()
        {
            foreach (var collection in new[] {"competitions", "competitionEditions"})
            {
                var document = Valid();
                ((JArray)document[collection]).Add(document[collection][0].DeepClone());
                AssertFailure(Import(document), "duplicate_id", "$." + collection + "[1].id");
            }
            var repeated = Valid();
            repeated["competitionEditions"][0]["participantClubIds"][1] = repeated["competitionEditions"][0]["participantClubIds"][0].DeepClone();
            AssertFailure(Import(repeated), "duplicate_participant", "$.competitionEditions[0].participantClubIds[1]");
        }

        [Test]
        public void RejectsInconsistentPointsAndCollectionLimits()
        {
            var document = Valid();
            document["competitionEditions"][0]["rules"]["points"]["win"] = 1;
            AssertFailure(Import(document), "invalid_points", "$.competitionEditions[0].rules.points");
            document = Valid();
            var competitions = (JArray)document["competitions"];
            for (var i = 1; i <= 128; i++) competitions.Add(new JObject { ["id"] = "c-" + i, ["name"] = "C " + i });
            AssertFailure(Import(document), "too_many_items", "$.competitions");
            document = Valid();
            var participants = (JArray)document["competitionEditions"][0]["participantClubIds"];
            while (participants.Count < 65) participants.Add("club-" + participants.Count);
            AssertFailure(Import(document), "too_many_items", "$.competitionEditions[0].participantClubIds");
        }

        [Test]
        public void VersionThreeCanRepresentNoEditionsAndKeepsAppearanceMetadata()
        {
            var document = Valid();
            document["competitions"] = new JArray();
            document["competitionEditions"] = new JArray();
            document["visualProfiles"][0]["appearance"] = JObject.Parse(@"{
                'skinTone':'tone-3','hairStyle':'short','hairColor':'black','beardStyle':'none',
                'beardColor':'black','bootsColor':'white','sockAccessoryColor':'none'}");
            var result = Import(document);
            AssertSuccess(result);
            Assert.That(result.Catalog.CompetitionEditions, Is.Empty);
            Assert.That(result.VisualProfiles[0].Appearance.HairStyle, Is.EqualTo("short"));
        }

        [Test]
        public void FailedEditionImportLeavesThePreviousCatalogUntouched()
        {
            var previous = Import(Valid());
            AssertSuccess(previous);
            var document = Valid();
            document["competitionEditions"][0]["roundDates"][0] = "not-a-date";
            var failed = Import(document);
            Assert.That(failed.Success, Is.False);
            Assert.That(failed.Catalog, Is.Null);
            Assert.That(failed.VisualProfiles, Is.Empty);
            Assert.That(previous.Catalog.CompetitionEditions[0].StartDate.ToString(), Is.EqualTo("2026-10-03"));
        }

        private static JObject Legacy() => JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
            "FootballSimulator/Code/FootballWorld/Tests/Fixtures/legacy-four-clubs.database.json")));
        private static JObject Valid()
        {
            var document = Legacy();
            document["schemaVersion"] = 3;
            document["competitions"] = new JArray(new JObject { ["id"] = "competition-test", ["name"] = "Example competition" });
            document["competitionEditions"] = new JArray(new JObject {
                ["id"] = "edition-test", ["competitionId"] = "competition-test", ["name"] = "Example edition",
                ["participantClubIds"] = new JArray(document["clubs"].Select(club => club["id"].DeepClone())),
                ["roundDates"] = new JArray("2026-10-03", "2026-10-10", "2026-10-17"),
                ["rules"] = JObject.Parse(@"{'type':'round-robin','version':1,'legs':1,
                    'points':{'win':3,'draw':1,'loss':0},'tieBreakers':['wins','goal-difference','goals-for']}")
            });
            return document;
        }
        private DatabaseImportResult Import(JObject document) => importer.Import(document.ToString(Formatting.None));
        private static void AssertSuccess(DatabaseImportResult result)
            => Assert.That(result.Success, Is.True, string.Join("; ", result.Errors.Select(error => error.Path + ": " + error.Message)));
        private static void AssertFailure(DatabaseImportResult result, string code, string path)
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors.Any(error => error.Code == code && error.Path == path), Is.True,
                string.Join("; ", result.Errors.Select(error => error.Code + " " + error.Path)));
        }
    }
}
