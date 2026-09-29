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
using NUnit.Framework;
using UnityEngine;

namespace FStudio.FootballWorld.Editor.Tests
{
    public sealed class CareerTacticsTransactionTests
    {
        [Test]
        public void FailedWriteKeepsSavedTacticsAndTheSameCareerWhileTheDraftCanBeRetried()
        {
            var source = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json"));
            var imported = new JsonDatabaseImporter().Import(source);
            Assert.That(imported.Success, Is.True);
            var edition = imported.Catalog.CompetitionEditions.First();
            var club = imported.Catalog.GetClub(edition.ParticipantClubIds[0]);
            var career = CareerSession.Create(imported.Catalog, edition.Id, "tactics-transaction", club.Id, new GameDate(2026, 1, 1));
            var profile = new HubCareerProfile("Treinador", "coach-1", 1, 2026, club.Id, club.Name, imported.Catalog.DatabaseId, imported.Catalog.DatabaseRevision);
            var preferences = new FailingPreferences();
            var storage = new LocalGameSaveStore(preferences);
            var previousJson = GameSaveCodec.DailyCareer(profile, career, source);
            Assert.That(storage.Write(GameHubSession.CareerSaveKey, previousJson, out _), Is.True);
            var previousPlan = career.TacticPlan;
            var draft = CareerTacticPlan.CreateDefault(CareerFormation.FourTwoThreeOne)
                .WithSlot("attacking-midfielder", .55f, .8f, CareerPlayerRole.CreativePlaymaker);

            // Dormant hosts use the same isolated preference seam as GameSaveTests.
            // Preserve the actual friendly-session singleton and all user's saves.
            var host = new GameObject("Isolated tactical save transaction"); host.SetActive(false);
            var hub = host.AddComponent<GameHubSession>();
            var friendly = host.AddComponent<FriendlyMatchSession>();
            var currentField = typeof(FriendlyMatchSession).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
            var previousFriendly = currentField.GetValue(null);
            try
            {
                currentField.SetValue(null, friendly);
                Field(hub, "careerSession", career); Field(hub, "careerDatabaseJson", source); Field(hub, "saves", storage);
                Field(hub, "<Career>k__BackingField", profile);
                preferences.FailWrites = true;
                Assert.That(hub.SetCareerTactics("4-2-3-1", "Attacking", draft), Is.False);
                Assert.That(hub.CareerProgress, Is.SameAs(career));
                Assert.That(career.Formation, Is.EqualTo(CareerFormation.FourFourTwo));
                Assert.That(career.Mentality, Is.EqualTo(CareerMentality.Balanced));
                Assert.That(career.TacticPlan, Is.SameAs(previousPlan));
                Assert.That(storage.Read(GameHubSession.CareerSaveKey), Is.EqualTo(previousJson));
                Assert.That(draft.IsDefault, Is.False);
                preferences.FailWrites = false;
                Assert.That(hub.SetCareerTactics("4-2-3-1", "Attacking", draft), Is.True);
                Assert.That(career.TacticPlan, Is.SameAs(draft));
                var restored = GameSaveCodec.RestoreDailyCareer(storage.Read(GameHubSession.CareerSaveKey));
                Assert.That(restored.Session.TacticPlan.SameConfiguration(draft), Is.True);
                Assert.That(restored.Session.Mentality, Is.EqualTo(CareerMentality.Attacking));
            }
            finally
            {
                currentField.SetValue(null, previousFriendly);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static void Field(GameHubSession target, string name, object value)
            => typeof(GameHubSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private sealed class FailingPreferences : IGamePreferenceStore
        {
            private readonly Dictionary<string, string> values = new Dictionary<string, string>();
            public bool FailWrites;
            public bool HasKey(string key) => values.ContainsKey(key);
            public string GetString(string key) => values[key];
            public void SetString(string key, string value) { if (FailWrites) throw new IOException("Storage unavailable."); values[key] = value; }
            public void DeleteKey(string key) => values.Remove(key);
            public void Flush() { }
        }
    }
}
#endif
