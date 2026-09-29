#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.GameModes;
using FStudio.FootballWorld.Infrastructure.Importing;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using FStudio.MatchEngine.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FStudio.FootballWorld.Editor.Tests
{
    public sealed class GameSaveTests
    {
        [Test]
        public void ChampionshipSnapshotCompactsWhitespaceWithoutDroppingAuthoredData()
        {
            var json = JToken.Parse(ChampionshipDatabase()).ToString(Formatting.Indented);
            var imported = new JsonDatabaseImporter().Import(json);
            var season = CompetitionSession.Create(imported.Catalog, "save-edition", "compact-save", imported.Catalog.Clubs[0].Id);
            var saved = GameSaveCodec.Championship(season, json);
            var compacted = (string)JObject.Parse(saved)["databaseJson"];
            Assert.That(compacted.Length, Is.LessThan(json.Length));
            Assert.That(JToken.DeepEquals(JToken.Parse(compacted), JToken.Parse(json)), Is.True);
            Assert.That(GameSaveCodec.RestoreChampionship(saved).Profiles.Count, Is.EqualTo(imported.VisualProfiles.Count));
            // Existing v1 saves can retain their original formatting and still restore.
            var legacy = JObject.Parse(saved); legacy["databaseJson"] = json;
            Assert.That(GameSaveCodec.RestoreChampionship(legacy.ToString(Formatting.None)).Session.SeasonId, Is.EqualTo("compact-save"));
        }

        [Test]
        public void RuntimeBridgeAcceptsOnlyItsLeaseIgnoresDuplicateWhistlesAndAbortsWithoutAwardingPoints()
        {
            var json = ChampionshipDatabase();
            var imported = new JsonDatabaseImporter().Import(json);
            var season = CompetitionSession.Create(imported.Catalog, "save-edition", "bridge-season", imported.Catalog.Clubs[0].Id);
            var bindings = AssetDatabase.LoadAssetAtPath<LegacyMatchBindings>(
                "Assets/FootballSimulator/Code/FootballWorld/Editor/Tests/Fixtures/LegacyMatchBindings.asset");
            var preferences = new FakePreferences();
            var storage = new LocalGameSaveStore(preferences);
            // Keep MonoBehaviour lifecycle dormant: the fixture supplies an
            // isolated catalog, lease and preference store, never the user's save.
            var host = new GameObject("Isolated championship result bridge");
            host.SetActive(false);
            var hub = host.AddComponent<GameHubSession>();
            var owned = new List<CatalogMatchLease>();
            using (var adapter = new CatalogMatchAdapter(imported.Catalog, imported.VisualProfiles, bindings))
            {
                try
                {
                    Field(hub, "season", season); Field(hub, "seasonDatabaseJson", json); Field(hub, "saves", storage);
                    var fixture = season.NextFixture;
                    Assert.That(adapter.TryCreateMatch(fixture.HomeClubId, fixture.AwayClubId, out var lease, out var error), Is.True, error);
                    owned.Add(lease);
                    Assert.That(adapter.TryCreateMatch(fixture.HomeClubId, fixture.AwayClubId, out var unrelated, out error), Is.True, error);
                    owned.Add(unrelated);
                    var execution = season.BeginFixture(fixture.Id, "bridge-execution-1");
                    Field(hub, "execution", execution); Field(hub, "executionLease", lease);
                    Whistle(hub, new FinalWhistleEvent(unrelated.Request.homeTeam, unrelated.Request.awayTeam, 9, 0));
                    Assert.That(season.Results, Is.Empty, "Same clubs from another lease must not complete this execution.");
                    var result = new FinalWhistleEvent(lease.Request.homeTeam, lease.Request.awayTeam, 2, 1);
                    Whistle(hub, result);
                    Whistle(hub, result);
                    Assert.That(season.Results.Count, Is.EqualTo(2));
                    Assert.That(season.GetResult(fixture.Id).HomeGoals, Is.EqualTo(2));
                    Assert.That(GameSaveCodec.RestoreChampionship(storage.Read(GameHubSession.ChampionshipSaveKey)).Session.Results.Count, Is.EqualTo(2));
                    hub.NotifyMatchUnloaded();
                    Assert.That(season.Results.Count, Is.EqualTo(2));

                    fixture = season.NextFixture;
                    Assert.That(adapter.TryCreateMatch(fixture.HomeClubId, fixture.AwayClubId, out var nextLease, out error), Is.True, error);
                    owned.Add(nextLease);
                    Field(hub, "execution", season.BeginFixture(fixture.Id, "bridge-execution-2")); Field(hub, "executionLease", nextLease);
                    Whistle(hub, result);
                    Assert.That(season.Results.Count, Is.EqualTo(2), "A late whistle from the previous fixture must be ignored.");
                    hub.NotifyMatchUnloaded();
                    Assert.That(season.ActiveExecution, Is.Null);
                    Assert.That(season.NextFixture.Id, Is.EqualTo(fixture.Id));
                    Assert.That(season.Results.Count, Is.EqualTo(2));
                    var restored = GameSaveCodec.RestoreChampionship(storage.Read(GameHubSession.ChampionshipSaveKey));
                    Assert.That(restored.Session.NextFixture.Id, Is.EqualTo(fixture.Id));
                    Assert.Throws<InvalidOperationException>(() => restored.Session.BeginFixture(fixture.Id, "bridge-execution-2"));
                }
                finally
                {
                    foreach (var lease in owned) lease.Dispose();
                    UnityEngine.Object.DestroyImmediate(host);
                }
            }
        }

        private static void Field(GameHubSession target, string name, object value)
            => typeof(GameHubSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Whistle(GameHubSession target, FinalWhistleEvent result)
            => typeof(GameHubSession).GetMethod("OnFinalWhistle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, new object[] { result });

        [Test]
        public void SavedChampionshipRestoresExactContentAppearanceProgressAndInterruptedExecution()
        {
            var json = ChampionshipDatabase();
            var imported = new JsonDatabaseImporter().Import(json);
            Assert.That(imported.Success, Is.True, string.Join("; ", imported.Errors.Select(error => error.Message)));
            var season = CompetitionSession.Create(imported.Catalog, "save-edition", "save-season", imported.Catalog.Clubs[0].Id);
            var first = season.NextFixture;
            var completed = season.BeginFixture(first.Id, "completed-execution");
            season.CompleteFixture(new FixtureResult(first.Id, completed.ExecutionId, first.HomeClubId, first.AwayClubId, 3, 1));
            var interrupted = season.BeginFixture(season.NextFixture.Id, "interrupted-execution");
            var persisted = GameSaveCodec.Championship(season, json);
            var restored = GameSaveCodec.RestoreChampionship(persisted);

            Assert.That(JToken.DeepEquals(JToken.Parse(restored.DatabaseJson), JToken.Parse(json)), Is.True);
            Assert.That(restored.Session.Catalog, Is.Not.SameAs(imported.Catalog));
            Assert.That(restored.Session.Catalog.DatabaseRevision, Is.EqualTo(17));
            Assert.That(restored.Profiles[0].Appearance.HairStyle, Is.EqualTo("locs"));
            Assert.That(restored.Profiles[0].Appearance.BootsColor, Is.EqualTo("cyan"));
            Assert.That(restored.Session.Results.Count, Is.EqualTo(2), "The completed round includes its simulated opponent match.");
            Assert.That(restored.Session.GetResult(first.Id).HomeGoals, Is.EqualTo(3));
            Assert.That(restored.Session.ActiveExecution, Is.Null, "An interrupted 3D match must be replayable after refresh.");
            Assert.That(restored.Session.NextFixture.Id, Is.EqualTo(interrupted.FixtureId));
            Assert.Throws<InvalidOperationException>(() => restored.Session.BeginFixture(interrupted.FixtureId, interrupted.ExecutionId));
            Assert.That(restored.Session.Standings.Select(row => new { row.ClubId, row.Points, row.Played }),
                Is.EqualTo(season.Standings.Select(row => new { row.ClubId, row.Points, row.Played })));

            var newerJson = JObject.Parse(json);
            newerJson["databaseRevision"] = 18;
            newerJson["clubs"][0]["name"] = "Different database revision";
            var newer = new JsonDatabaseImporter().Import(newerJson.ToString());
            Assert.That(newer.Success, Is.True);
            Assert.That(restored.Session.Catalog.GetClub(imported.Catalog.Clubs[0].Id).Name,
                Is.EqualTo(imported.Catalog.Clubs[0].Name), "A newer source must not mutate the saved catalog.");
        }

        [TestCase("version", "999")]
        [TestCase("databaseRevision", "18")]
        [TestCase("controlledClubId", "\"unknown-club\"")]
        [TestCase("fixtures[0].day", "4")]
        [TestCase("fixtures[0].round", "2")]
        [TestCase("fixtures[0].homeClubId", "\"unknown-club\"")]
        public void SavedChampionshipRejectsInvalidOrIncompatibleSnapshots(string path, string replacement)
        {
            var json = ChampionshipDatabase();
            var imported = new JsonDatabaseImporter().Import(json);
            var session = CompetitionSession.Create(imported.Catalog, "save-edition", "save-season", imported.Catalog.Clubs[0].Id);
            var saved = JObject.Parse(GameSaveCodec.Championship(session, json));
            saved.SelectToken(path).Replace(JToken.Parse(replacement));
            Assert.That(() => GameSaveCodec.RestoreChampionship(saved.ToString()), Throws.Exception);
        }

        [TestCase(1)]
        [TestCase(9999)]
        public void CareerRoundTripPreservesCoachDateAvatarAndPinnedClub(int year)
        {
            var profile = new HubCareerProfile("Treinador Áureo", "coach-3", 12, year, "club-a", "Club A", "database-a", 5);
            var restored = GameSaveCodec.RestoreCareer(GameSaveCodec.Career(profile));
            Assert.That(restored.CoachName, Is.EqualTo(profile.CoachName));
            Assert.That(restored.AvatarId, Is.EqualTo("coach-3"));
            Assert.That(restored.StartYear, Is.EqualTo(year));
            Assert.That(restored.StartMonth, Is.EqualTo(12));
            Assert.That(restored.ClubId, Is.EqualTo("club-a"));
            Assert.That(restored.DatabaseId, Is.EqualTo("database-a"));
            Assert.That(restored.DatabaseRevision, Is.EqualTo(5));
        }

        [TestCase("coachName", "\" \"")]
        [TestCase("avatarId", "\"unknown\"")]
        [TestCase("startYear", "0")]
        [TestCase("startYear", "10000")]
        [TestCase("startMonth", "13")]
        [TestCase("databaseRevision", "0")]
        public void InvalidCareerSaveIsRejected(string path, string replacement)
        {
            var saved = JObject.Parse(GameSaveCodec.Career(new HubCareerProfile("Coach", "coach-1", 10, 2026, "club", "Club", "db", 1)));
            saved[path] = JToken.Parse(replacement);
            Assert.That(() => GameSaveCodec.RestoreCareer(saved.ToString()), Throws.Exception);
        }

        [Test]
        public void SaveCodecRejectsDuplicateFieldsTrailingDocumentsAndUnknownFields()
        {
            var json = GameSaveCodec.Career(new HubCareerProfile("Coach", "coach-1", 10, 2026, "club", "Club", "db", 1));
            Assert.That(() => GameSaveCodec.RestoreCareer(json.Replace("\"version\":1", "\"version\":1,\"version\":1")), Throws.Exception);
            Assert.That(() => GameSaveCodec.RestoreCareer(json + "{}"), Throws.Exception);
            var saved = JObject.Parse(json);
            saved["unrecognized"] = true;
            Assert.That(() => GameSaveCodec.RestoreCareer(saved.ToString()), Throws.Exception);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void FailedStagingOrCommitPreservesPreviousSaveAcrossReload(int failingFlush)
        {
            var preferences = new FakePreferences();
            var store = new LocalGameSaveStore(preferences);
            Assert.That(store.Write("test", "first", out _), Is.True);
            Assert.That(store.Write("test", "second", out _), Is.True);
            preferences.FailFlushAt = preferences.Flushes + failingFlush;
            Assert.That(store.Write("test", "replacement", out var error), Is.False);
            Assert.That(error, Is.EqualTo("save_failed"));
            Assert.That(store.Read("test"), Is.EqualTo("second"));
            preferences.Reload();
            Assert.That(store.Read("test"), Is.EqualTo("second"));
            Assert.That(store.Write("test", "retry", out _), Is.True);
            preferences.Reload();
            Assert.That(store.Read("test"), Is.EqualTo("retry"));
        }

        [Test]
        public void OversizedUtf8PayloadAndInvalidPointerNeverEraseExistingData()
        {
            var preferences = new FakePreferences();
            var store = new LocalGameSaveStore(preferences);
            Assert.That(store.Write("test", "existing", out _), Is.True);
            var flushes = preferences.Flushes;
            Assert.That(store.Write("test", new string('á', LocalGameSaveStore.MaxPayloadBytes / 2 + 1), out var error), Is.False);
            Assert.That(error, Is.EqualTo("save_too_large"));
            Assert.That(preferences.Flushes, Is.EqualTo(flushes));
            Assert.That(store.Read("test"), Is.EqualTo("existing"));
            preferences.SetString("test.active", "corrupt");
            Assert.Throws<InvalidOperationException>(() => store.Read("test"));
            Assert.That(preferences.GetString("test.active"), Is.EqualTo("corrupt"));
            Assert.That(preferences.GetString("test.0"), Is.EqualTo("existing"));
        }

        private static string ChampionshipDatabase()
        {
            var path = Path.Combine(UnityEngine.Application.dataPath, "FootballSimulator/Code/FootballWorld/Tests/Fixtures/legacy-four-clubs.database.json");
            var document = JObject.Parse(File.ReadAllText(path));
            document["schemaVersion"] = 3;
            document["databaseRevision"] = 17;
            document["competitions"] = new JArray(new JObject { ["id"] = "save-competition", ["name"] = "Saved competition" });
            document["competitionEditions"] = new JArray(new JObject {
                ["id"] = "save-edition", ["competitionId"] = "save-competition", ["name"] = "Saved edition",
                ["participantClubIds"] = new JArray(document["clubs"].Select(club => club["id"].DeepClone())),
                ["roundDates"] = new JArray("2026-10-03", "2026-10-10", "2026-10-17"),
                ["rules"] = JObject.Parse(@"{'type':'round-robin','version':1,'legs':1,
                    'points':{'win':3,'draw':1,'loss':0},'tieBreakers':['wins','goal-difference','goals-for']}")
            });
            document["visualProfiles"][0]["appearance"] = JObject.Parse(@"{'skinTone':'tone-4','hairStyle':'locs','hairColor':'black',
                'beardStyle':'full','beardColor':'dark-brown','bootsColor':'cyan','sockAccessoryColor':'white'}");
            return document.ToString(Formatting.None);
        }

        private sealed class FakePreferences : IGamePreferenceStore
        {
            private Dictionary<string, string> current = new Dictionary<string, string>();
            private Dictionary<string, string> persisted = new Dictionary<string, string>();
            public int Flushes { get; private set; }
            public int FailFlushAt { get; set; } = -1;
            public bool HasKey(string key) => current.ContainsKey(key);
            public string GetString(string key) => current[key];
            public void SetString(string key, string value) { current[key] = value; }
            public void DeleteKey(string key) { current.Remove(key); }
            public void Flush()
            {
                if (++Flushes == FailFlushAt) throw new InvalidOperationException("Storage unavailable.");
                persisted = new Dictionary<string, string>(current);
            }
            public void Reload() { current = new Dictionary<string, string>(persisted); }
        }
    }
}
#endif
