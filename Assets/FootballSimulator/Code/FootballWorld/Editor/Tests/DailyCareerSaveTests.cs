#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.GameModes;
using FStudio.FootballWorld.Infrastructure.Importing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FStudio.FootballWorld.Editor.Tests
{
    public sealed class DailyCareerSaveTests
    {
        [Test]
        public void DailyCodecRestoresTrainingFinancesDatesAndPinnedSourceWithoutDuplicateTransactions()
        {
            var source = ManagementTestDatabase();
            var imported = Import(source);
            var career = CareerSession.Create(imported.Catalog, "management-edition", "management-save", imported.Catalog.Clubs[0].Id, new GameDate(2026, 1, 1));
            career.SetTraining(CareerTraining.Intensive);
            career.AdvanceToNextFixture();
            career.SimulateNextFixture();
            career.SetTraining(CareerTraining.Recovery);
            career.AdvanceToNextFixture();
            career.SimulateNextFixture();
            var profile = Profile(career);
            var serialized = GameSaveCodec.DailyCareer(profile, career, source);
            var restored = GameSaveCodec.RestoreDailyCareer(serialized);
            var twice = GameSaveCodec.RestoreDailyCareer(GameSaveCodec.DailyCareer(restored.Profile, restored.Session, restored.DatabaseJson));
            Assert.That(twice.Session.CurrentDate, Is.EqualTo(new GameDate(2026, 2, 10)));
            Assert.That(twice.Session.FinanceBalance, Is.EqualTo(career.FinanceBalance));
            Assert.That(twice.Session.Condition, Is.EqualTo(career.Condition));
            Assert.That(twice.Session.Preparation, Is.EqualTo(career.Preparation));
            Assert.That(twice.Session.Training, Is.EqualTo(CareerTraining.Recovery));
            Assert.That(twice.Session.Ledger.Count(value => value.EventKey == "monthly-wages"), Is.EqualTo(1));
            Assert.That(twice.Session.News.Count, Is.EqualTo(career.News.Count));
            Assert.That(twice.Profiles.Count, Is.EqualTo(imported.VisualProfiles.Count));
            Assert.That(JToken.DeepEquals(JToken.Parse(twice.DatabaseJson), JToken.Parse(source)), Is.True);
        }

        [Test]
        public void FullHistoricalDatabaseFitsBothTwoSlotSavesAndPreservesContentAboveOldPayloadLimit()
        {
            var source = CurrentDatabase();
            Assert.That((int)JObject.Parse(source)["schemaVersion"], Is.EqualTo(6));
            var imported = Import(source);
            var edition = imported.Catalog.CompetitionEditions.First();
            var club = edition.ParticipantClubIds[0];
            var career = CareerSession.Create(imported.Catalog, edition.Id, "historical-career", club, new GameDate(2026, 1, 1));
            career.AdvanceToNextFixture();
            career.SimulateNextFixture();
            var careerSave = GameSaveCodec.DailyCareer(Profile(career), career, source);
            var championship = CompetitionSession.Create(imported.Catalog, edition.Id, "historical-championship", club);
            var championshipSave = GameSaveCodec.Championship(championship, source);
            Assert.That(Encoding.UTF8.GetByteCount(careerSave), Is.GreaterThan(384 * 1024));
            var preferences = new Preferences();
            var store = new LocalGameSaveStore(preferences);
            for (var i = 0; i < 2; i++)
            {
                Assert.That(store.Write("career", careerSave, out var careerError), Is.True, careerError);
                Assert.That(store.Write("championship", championshipSave, out var championshipError), Is.True, championshipError);
            }
            Assert.That(preferences.Values.Values.Count(value => value.StartsWith("football-save:gzip:1:", StringComparison.Ordinal)), Is.EqualTo(4));
            // Include both slots for both modes, pointers, and UTF-16 string storage overhead.
            Assert.That(preferences.Values.Sum(value => Encoding.Unicode.GetByteCount(value.Key + value.Value)), Is.LessThan(1024 * 1024));
            var restored = GameSaveCodec.RestoreDailyCareer(store.Read("career"));
            Assert.That(restored.Session.Competition.Results.Count, Is.EqualTo(career.Competition.Results.Count));
            Assert.That(restored.Session.FinanceBalance, Is.EqualTo(career.FinanceBalance));
            Assert.That(GameSaveCodec.RestoreChampionship(store.Read("championship")).Session.Catalog.Players.Count, Is.EqualTo(imported.Catalog.Players.Count));
            Assert.That(JToken.DeepEquals(JToken.Parse(restored.DatabaseJson), JToken.Parse(source)), Is.True);
        }

        [Test]
        public void StorageRejectsCorruptOrExcessivelyExpandedContentWithoutReplacingOtherSave()
        {
            var preferences = new Preferences();
            var store = new LocalGameSaveStore(preferences);
            Assert.That(store.Write("good", "preserved", out _), Is.True);
            preferences.SetString("bad.active", "0");
            preferences.SetString("bad.0", "football-save:gzip:1:invalid-base64!");
            Assert.That(() => store.Read("bad"), Throws.Exception);
            preferences.SetString("bad.0", Compress(new string('x', LocalGameSaveStore.MaxPayloadBytes + 1)));
            Assert.That(() => store.Read("bad"), Throws.Exception);
            Assert.That(store.Read("good"), Is.EqualTo("preserved"));
            Assert.That(preferences.GetString("bad.active"), Is.EqualTo("0"));
        }

        [Test]
        public void HighEntropyPayloadCannotExceedThePhysicalSlotBudgetOrErasePriorValue()
        {
            var preferences = new Preferences();
            var store = new LocalGameSaveStore(preferences);
            Assert.That(store.Write("test", "previous", out _), Is.True);
            var bytes = new byte[400 * 1024];
            new Random(72642).NextBytes(bytes);
            Assert.That(store.Write("test", Convert.ToBase64String(bytes), out var error), Is.False);
            Assert.That(error, Is.EqualTo("save_too_large"));
            Assert.That(store.Read("test"), Is.EqualTo("previous"));
        }

        [Test]
        public void InvalidCommitPointerPreventsWriteAndPreservesBothSlots()
        {
            var preferences = new Preferences();
            preferences.SetString("test.active", "corrupt");
            preferences.SetString("test.0", "first-copy");
            preferences.SetString("test.1", "second-copy");
            Assert.That(new LocalGameSaveStore(preferences).Write("test", "replacement", out var error), Is.False);
            Assert.That(error, Is.EqualTo("save_failed"));
            Assert.That(preferences.GetString("test.active"), Is.EqualTo("corrupt"));
            Assert.That(preferences.GetString("test.0"), Is.EqualTo("first-copy"));
            Assert.That(preferences.GetString("test.1"), Is.EqualTo("second-copy"));
        }

        [Test]
        public void LegacyProfileOnlySaveRemainsReadableWithoutInventingADailySeason()
        {
            var profile = new HubCareerProfile("Treinador histórico", "coach-3", 6, 2027, "club-preserved", "Clube preservado", "database-old", 6);
            var original = GameSaveCodec.Career(profile);
            var preferences = new Preferences();
            preferences.SetString("legacy.active", "0");
            preferences.SetString("legacy.0", original);
            var restored = GameSaveCodec.RestoreDailyCareer(new LocalGameSaveStore(preferences).Read("legacy"));
            Assert.That(restored.Profile.CoachName, Is.EqualTo(profile.CoachName));
            Assert.That(restored.Profile.StartYear, Is.EqualTo(2027));
            Assert.That(restored.Profile.StartMonth, Is.EqualTo(6));
            Assert.That(restored.Session, Is.Null);
            Assert.That(restored.DatabaseJson, Is.Null);
            Assert.That(preferences.GetString("legacy.0"), Is.EqualTo(original));
        }

        [TestCase("condition", "0")]
        [TestCase("ledger[0].amount", "999999999")]
        [TestCase("profile.startMonth", "2")]
        [TestCase("profile.clubId", "\"unknown\"")]
        [TestCase("training", "99")]
        [TestCase("news[0].outletId", "\"invented-outlet\"")]
        [TestCase("version", "999")]
        public void DailyCodecRejectsInconsistentProfileOrManagementHistory(string path, string replacement)
        {
            var source = ManagementTestDatabase();
            var imported = Import(source);
            var career = CareerSession.Create(imported.Catalog, "management-edition", "tamper-save", imported.Catalog.Clubs[0].Id, new GameDate(2026, 1, 1));
            var saved = JObject.Parse(GameSaveCodec.DailyCareer(Profile(career), career, source));
            saved.SelectToken(path).Replace(JToken.Parse(replacement));
            Assert.That(() => GameSaveCodec.RestoreDailyCareer(saved.ToString(Formatting.None)), Throws.Exception);
        }

        private static DatabaseImportResult Import(string json)
        {
            var imported = new JsonDatabaseImporter().Import(json);
            Assert.That(imported.Success, Is.True, string.Join("; ", imported.Errors.Select(value => value.Message)));
            return imported;
        }

        private static HubCareerProfile Profile(CareerSession career)
        {
            var club = career.Competition.Catalog.GetClub(career.Competition.ControlledClubId);
            return new HubCareerProfile("Treinador de teste", "coach-1", career.StartDate.Month, career.StartDate.Year,
                club.Id, club.Name, career.Competition.Catalog.DatabaseId, career.Competition.Catalog.DatabaseRevision);
        }

        private static string CurrentDatabase() => File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
            "FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json"));

        private static string ManagementTestDatabase()
        {
            var document = JObject.Parse(CurrentDatabase());
            document["competitions"] = new JArray(new JObject { ["id"] = "management-competition", ["name"] = "Management fixture" });
            document["competitionEditions"] = new JArray(new JObject {
                ["id"] = "management-edition", ["competitionId"] = "management-competition", ["name"] = "Management fixture 2026",
                ["participantClubIds"] = new JArray(document["clubs"].Take(4).Select(club => club["id"].DeepClone())),
                ["roundDates"] = new JArray("2026-01-10", "2026-02-10", "2026-03-10"),
                ["rules"] = JObject.Parse(@"{'type':'round-robin','version':1,'legs':1,'points':{'win':3,'draw':1,'loss':0},'tieBreakers':['wins','goal-difference','goals-for']}")
            });
            return document.ToString(Formatting.None);
        }

        private static string Compress(string value)
        {
            using (var output = new MemoryStream())
            {
                using (var zip = new GZipStream(output, CompressionLevel.Optimal, true))
                {
                    var bytes = Encoding.UTF8.GetBytes(value);
                    zip.Write(bytes, 0, bytes.Length);
                }
                return "football-save:gzip:1:" + Convert.ToBase64String(output.ToArray());
            }
        }

        private sealed class Preferences : IGamePreferenceStore
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
            public bool HasKey(string key) => Values.ContainsKey(key);
            public string GetString(string key) => Values[key];
            public void SetString(string key, string value) => Values[key] = value;
            public void DeleteKey(string key) => Values.Remove(key);
            public void Flush() { }
        }
    }
}
#endif
