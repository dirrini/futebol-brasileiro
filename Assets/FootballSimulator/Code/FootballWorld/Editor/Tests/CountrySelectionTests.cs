#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.Importing;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using FStudio.FootballWorld.Presentation;
using FStudio.UI.Panels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FStudio.FootballWorld.Editor.Tests
{
    public sealed class CountrySelectionTests
    {
        private static CatalogMatchAdapter MixedCountries()
        {
            var imported = new JsonDatabaseImporter().Import(File.ReadAllText(
                "Assets/FootballSimulator/Code/FootballWorld/Tests/Fixtures/legacy-four-clubs.database.json"));
            Assert.That(imported.Success, Is.True);
            var source = imported.Catalog;
            var codes = new[] { "BR", "BR", "AR", null };
            var catalog = new DatabaseCatalog(source.DatabaseId, source.DatabaseRevision,
                source.Clubs.Select((club, index) => new ClubDefinition(club.Id, club.Name, codes[index], codes[index] == null ? null : "Example city")),
                source.Players, source.Memberships, countries: new[] { new CountryDefinition("BR", "Brasil"), new CountryDefinition("AR", "Argentina") });
            var bindings = AssetDatabase.LoadAssetAtPath<LegacyMatchBindings>(
                "Assets/FootballSimulator/Code/FootballWorld/Editor/Tests/Fixtures/LegacyMatchBindings.asset");
            return new CatalogMatchAdapter(catalog, imported.VisualProfiles, bindings);
        }

        [Test]
        public void CountryFilterPreservesStableSelectionAndNeverAddsNonParticipants()
        {
            using (var adapter = MixedCountries())
            {
                var countries = CatalogCountryFilter.Countries(adapter.Catalog, adapter.Teams);
                Assert.That(countries.Select(country => country.Code), Is.EqualTo(new[] { "BR", "AR", "" }));
                var brazil = CatalogCountryFilter.Teams(adapter.Teams, "BR");
                Assert.That(brazil.Select(team => team.ClubId), Is.EqualTo(adapter.Teams.Take(2).Select(team => team.ClubId)));
                var selected = brazil[1].ClubId;
                Assert.That(CatalogCountryFilter.RetainClub(brazil.Reverse(), selected), Is.EqualTo(selected));
                Assert.That(CatalogCountryFilter.RetainCountry(countries, null, adapter.Teams, adapter.Teams[2].ClubId), Is.EqualTo("AR"));
                Assert.That(CatalogCountryFilter.Teams(adapter.Teams, null), Is.Empty);
                var participants = new[] { adapter.Teams[0], adapter.Teams[2] };
                Assert.That(CatalogCountryFilter.Teams(participants, "BR").Select(team => team.ClubId), Is.EqualTo(new[] { adapter.Teams[0].ClubId }));
                Assert.That(CatalogCountryFilter.Teams(adapter.Teams, "").Single().ClubId, Is.EqualTo(adapter.Teams[3].ClubId),
                    "Legacy databases remain selectable in an explicit unspecified-country bucket.");
            }
        }

        [Test]
        public void FriendlyCountryChangeAffectsOnlyItsSideAndRejectsClubsOutsideThatCountry()
        {
            using (var adapter = MixedCountries())
            {
                var host = new GameObject("Isolated country selection"); host.SetActive(false);
                var session = host.AddComponent<FriendlyMatchSession>();
                try
                {
                    typeof(FriendlyMatchSession).GetField("adapter", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, adapter);
                    Set(session, "State", FriendlyMatchState.Ready);
                    Set(session, "SelectedHomeCountryCode", "BR"); Set(session, "SelectedAwayCountryCode", "BR");
                    Set(session, "SelectedHomeClubId", adapter.Teams[0].ClubId); Set(session, "SelectedAwayClubId", adapter.Teams[1].ClubId);
                    session.SelectCountry(true, "AR");
                    Assert.That(session.SelectedAwayClubId, Is.EqualTo(adapter.Teams[2].ClubId));
                    Assert.That(session.SelectedHomeClubId, Is.EqualTo(adapter.Teams[0].ClubId));
                    Assert.That(session.SelectedHomeCountryCode, Is.EqualTo("BR"));
                    session.Select(false, adapter.Teams[2].ClubId);
                    Assert.That(session.SelectedHomeClubId, Is.EqualTo(adapter.Teams[0].ClubId));
                    session.SelectCountry(true, "not-a-country");
                    Assert.That(session.SelectedAwayCountryCode, Is.EqualTo("AR"));
                    session.SelectCountry(true, "BR");
                    Assert.That(session.SelectedAwayClubId, Is.EqualTo(adapter.Teams[1].ClubId), "Prefer another club over selecting the home club twice.");
                }
                finally { UnityEngine.Object.DestroyImmediate(host); }
            }
        }

        [Test]
        public void AuthoredSelectorsAndGenericVisualsAreAvailableWithoutReplacingSaoPauloBinding()
        {
            var quick = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FootballSimulator/Arts/UI/Panels/MainMenuPanel.prefab");
            var quickFields = new SerializedObject(quick.GetComponent<MainMenuPanel>());
            Assert.That(quickFields.FindProperty("homeCountry").objectReferenceValue, Is.InstanceOf<TeamCountrySelection>());
            Assert.That(quickFields.FindProperty("awayCountry").objectReferenceValue, Is.InstanceOf<TeamCountrySelection>());
            var hub = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FootballSimulator/Resources/FootballWorld/GameHub.prefab");
            var hubFields = new SerializedObject(hub.GetComponent<GameHubView>());
            Assert.That(hubFields.FindProperty("careerCountryDropdown").objectReferenceValue, Is.Not.Null);
            Assert.That(hubFields.FindProperty("championshipCountryDropdown").objectReferenceValue, Is.Not.Null);
            var bindings = AssetDatabase.LoadAssetAtPath<LegacyMatchBindings>("Assets/FootballSimulator/Resources/FootballWorld/LegacyMatchBindings.asset");
            Assert.That(AssetDatabase.GetAssetPath(bindings.DefaultVisualTemplate), Is.EqualTo(GenericClubVisualAuthoring.Root + "GenericVisualTemplate.asset"));
            var generic = bindings.DefaultVisualTemplate;
            Assert.That(generic.HomeKit.Color1, Is.Not.EqualTo(generic.AwayKit.Color1));
            Assert.That(generic.HomeKit.GKColor1, Is.Not.EqualTo(generic.AwayKit.GKColor1));
            var saoPaulo = bindings.Clubs.Single(club => club.ClubId == "club-8515fca92adc4d77a428fb272bb7d160");
            Assert.That(saoPaulo.VisualTemplate, Is.Not.SameAs(generic));
            Assert.That(AssetDatabase.GetAssetPath(saoPaulo.VisualTemplate), Does.Contain("SaoPaulo"));
        }

        private static void Set(FriendlyMatchSession session, string property, object value)
            => typeof(FriendlyMatchSession).GetProperty(property).SetValue(session, value);
    }
}
#endif
