#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FStudio.Data;
using FStudio.Database;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.DataContracts;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.Importing;
using FStudio.FootballWorld.Infrastructure.GameModes;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using FStudio.MatchEngine.Tactics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FStudio.FootballWorld.Editor.Tests
{
    // This fixture deliberately uses the default Editor assembly: the bridge and
    // legacy engine are in Assembly-CSharp, which an asmdef cannot reference.
    public sealed class CatalogMatchAdapterTests
    {
        private const string BindingsPath = "Assets/FootballSimulator/Code/FootballWorld/Editor/Tests/Fixtures/LegacyMatchBindings.asset";
        private const string DatabasePath = "Assets/FootballSimulator/Code/FootballWorld/Tests/Fixtures/legacy-four-clubs.database.json";
        private const string AuthoredBindingsPath = "Assets/FootballSimulator/Resources/FootballWorld/LegacyMatchBindings.asset";
        private const string AuthoredDatabasePath = "Assets/FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json";
        private readonly List<IDisposable> ownedSessions = new List<IDisposable>();
        private readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();
        private readonly Dictionary<UnityEngine.Object, string> sourceSnapshots = new Dictionary<UnityEngine.Object, string>();
        private DatabaseImportResult imported;
        private LegacyMatchBindings bindings;

        [SetUp]
        public void SetUp()
        {
            imported = new JsonDatabaseImporter().Import(File.ReadAllText(Path.GetFullPath(DatabasePath)));
            Assert.IsTrue(imported.Success, string.Join("; ", imported.Errors.Select(error => error.Message)));
            bindings = AssetDatabase.LoadAssetAtPath<LegacyMatchBindings>(BindingsPath);
            Assert.IsTrue(bindings != null, "The fixture bindings asset must import successfully.");
            Snapshot(bindings);
            foreach (var club in bindings.Clubs)
            {
                Snapshot(club.VisualTemplate);
                Snapshot(club.VisualTemplate.TeamLogo);
                Snapshot(club.VisualTemplate.HomeKit);
                Snapshot(club.VisualTemplate.AwayKit);
                foreach (var player in club.VisualTemplate.Players) Snapshot(player);
            }
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = ownedSessions.Count - 1; i >= 0; i--) ownedSessions[i].Dispose();
            ownedSessions.Clear();
            foreach (var ownedObject in ownedObjects)
                if (ownedObject != null) UnityEngine.Object.DestroyImmediate(ownedObject);
            ownedObjects.Clear();
            foreach (var pair in sourceSnapshots)
            {
                Assert.IsTrue(pair.Key != null, "Borrowed source assets must never be destroyed.");
                Assert.AreEqual(pair.Value, EditorJsonUtility.ToJson(pair.Key), "Source asset changed: " + pair.Key.name);
            }
            sourceSnapshots.Clear();
        }

        [Test]
        public void LegacyFixtureBindingsCreateFourPreviewsAndMapAllFortyFourPlayers()
        {
            Assert.AreEqual(4, bindings.Clubs.Length);
            Assert.AreEqual(44, bindings.Players.Length);
            var adapter = CreateAdapter();
            Assert.AreEqual(4, adapter.Teams.Count);
            foreach (var option in adapter.Teams)
            {
                Assert.IsTrue(option.CanPlay, option.Error);
                Assert.IsNull(option.Warning, option.Warning);
                Assert.AreEqual(11, option.Preview.Players.Length);
                Assert.AreEqual(Positions.GK, option.Preview.Players[0].Position);
                Assert.AreSame(option.Preview.TeamLogo, option.Logo);
                var binding = bindings.Clubs.Single(item => item.ClubId == option.ClubId);
                Assert.AreEqual(binding.Formation, option.Preview.Formation);
                Assert.AreSame(binding.VisualTemplate.HomeKit, option.Preview.HomeKit);
                Assert.AreNotSame(binding.VisualTemplate, option.Preview);
                Assert.AreEqual(imported.Catalog.GetClub(option.ClubId).Name, option.Name);
            }
            var checkedPlayers = new HashSet<string>(StringComparer.Ordinal);
            for (var pair = 0; pair < 4; pair += 2)
            {
                var lease = CreateLease(adapter, adapter.Teams[pair].ClubId, adapter.Teams[pair + 1].ClubId);
                Assert.AreSame(imported.Catalog, lease.Catalog);
                Assert.AreEqual(22, lease.Players.Count);
                for (var i = 0; i < lease.Players.Count; i++)
                {
                    var identity = lease.Players[i];
                    Assert.AreEqual(i, identity.LocalId);
                    var team = i < 11 ? lease.Request.homeTeam : lease.Request.awayTeam;
                    var player = team.Players[i % 11];
                    var expected = imported.Catalog.GetPlayer(identity.PlayerId);
                    Assert.AreEqual(i < 11 ? lease.HomeClubId : lease.AwayClubId, identity.ClubId);
                    Assert.IsTrue(imported.Catalog.GetRoster(identity.ClubId).Any(item => item.Id == identity.PlayerId));
                    Assert.AreSame(team, player.team);
                    Assert.AreEqual(i, player.id);
                    AssertSportingData(expected, player);
                    var appearance = bindings.Players.Single(item => item.PlayerId == identity.PlayerId).Appearance;
                    AssertAppearance(appearance, player);
                    Assert.IsTrue(checkedPlayers.Add(identity.PlayerId));
                }
            }
            Assert.AreEqual(44, checkedPlayers.Count);
        }

        [Test]
        public void AuthoredDatabaseAndBindingsCreateMatchesWithoutTruncatingFullRosters()
        {
            var authored = new JsonDatabaseImporter().Import(File.ReadAllText(Path.GetFullPath(AuthoredDatabasePath)));
            Assert.IsTrue(authored.Success, string.Join("; ", authored.Errors.Select(error => error.Message)));
            var authoredBindings = AssetDatabase.LoadAssetAtPath<LegacyMatchBindings>(AuthoredBindingsPath);
            Assert.IsTrue(authoredBindings != null, "The live bindings must import successfully.");
            Snapshot(authoredBindings);
            foreach (var binding in authoredBindings.Clubs)
            {
                Snapshot(binding.VisualTemplate);
                Snapshot(binding.VisualTemplate.TeamLogo);
                Snapshot(binding.VisualTemplate.HomeKit);
                Snapshot(binding.VisualTemplate.AwayKit);
            }

            var catalog = authored.Catalog;
            var rosterSizes = catalog.Clubs.ToDictionary(club => club.Id, club => catalog.GetRoster(club.Id).Count);
            var adapter = CreateAdapter(catalog, authored.VisualProfiles, authoredBindings);
            Assert.That(adapter.Teams.Count, Is.EqualTo(catalog.Clubs.Count).And.GreaterThanOrEqualTo(2));
            for (var index = 0; index < adapter.Teams.Count; index++)
            {
                var option = adapter.Teams[index];
                Assert.IsTrue(option.CanPlay, option.Name + ": " + option.Error);
                Assert.AreEqual(catalog.GetClub(option.ClubId).Name, option.Name);
                Assert.AreEqual(11, option.Preview.Players.Length);
                var opponent = adapter.Teams[(index + 1) % adapter.Teams.Count];
                var lease = CreateLease(adapter, option.ClubId, opponent.ClubId);
                Assert.AreSame(catalog, lease.Catalog);
                Assert.AreEqual(22, lease.Players.Count);
                Assert.AreEqual(22, lease.Players.Select(player => player.PlayerId).Distinct().Count());
                foreach (var identity in lease.Players)
                {
                    var team = identity.LocalId < 11 ? lease.Request.homeTeam : lease.Request.awayTeam;
                    Assert.IsTrue(catalog.GetRoster(identity.ClubId).Any(player => player.Id == identity.PlayerId));
                    AssertSportingData(catalog.GetPlayer(identity.PlayerId), team.Players[identity.LocalId % 11]);
                }
            }
            foreach (var club in catalog.Clubs)
                Assert.AreEqual(rosterSizes[club.Id], catalog.GetRoster(club.Id).Count,
                    "Preparing a match must preserve every registered reserve.");
        }

        [Test]
        public void PlayerNicknameIsUsedInMatchWhileCatalogNamesAndIdentityArePreserved()
        {
            var original = imported.Catalog.Players[0];
            var edited = new PlayerDefinition(original.Id, original.Name, original.NaturalPositions, original.HeightCm,
                original.WeightKg, original.Attributes, fullName: "Full registered name", nickname: "Match nickname");
            var catalog = ReplacePlayers(imported.Catalog.Players.Select(player => player.Id == original.Id ? edited : player));
            var adapter = CreateAdapter(catalog);
            var lease = CreateLease(adapter, catalog.Clubs[0].Id, catalog.Clubs[1].Id);
            var identity = lease.Players.Single(player => player.PlayerId == original.Id);
            var matchPlayer = lease.Request.homeTeam.Players[identity.LocalId];
            Assert.AreEqual("Match nickname", matchPlayer.Name);
            Assert.AreEqual("Match nickname", matchPlayer.name);
            Assert.AreEqual(original.Name, catalog.GetPlayer(original.Id).Name);
            Assert.AreEqual("Full registered name", catalog.GetPlayer(original.Id).FullName);
            AssertSportingData(edited, matchPlayer);
        }

        [Test]
        public void SportingDataComesFromCatalogEvenWhenVisualTemplateHasDifferentValues()
        {
            var original = imported.Catalog.Players[0];
            var attributes = new PlayerAttributes(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15);
            var edited = new PlayerDefinition(original.Id, "Renamed player", original.NaturalPositions, 151, 99, attributes);
            var catalog = ReplacePlayers(imported.Catalog.Players.Select(player => player.Id == original.Id ? edited : player));
            var adapter = CreateAdapter(catalog);
            var lease = CreateLease(adapter, catalog.Clubs[0].Id, catalog.Clubs[1].Id);
            var identity = lease.Players.Single(player => player.PlayerId == original.Id);
            AssertSportingData(edited, lease.Request.homeTeam.Players[identity.LocalId]);
            Assert.AreEqual(imported.Catalog.Players[0].Name, original.Name);
            Assert.AreNotEqual(original.HeightCm, edited.HeightCm);
        }

        [Test]
        public void LeaseOwnsIndependentClonesAndSurvivesAdapterDisposal()
        {
            var adapter = CreateAdapter();
            var previews = adapter.Teams.Select(option => option.Preview).ToArray();
            var previewPlayers = previews.SelectMany(team => team.Players).ToArray();
            var lease = CreateLease(adapter, adapter.Teams[0].ClubId, adapter.Teams[1].ClubId);
            var second = CreateLease(adapter, lease.HomeClubId, lease.AwayClubId);
            var matchPlayers = lease.Request.homeTeam.Players.Concat(lease.Request.awayTeam.Players).ToArray();
            Assert.AreNotSame(previews[0], lease.Request.homeTeam);
            Assert.AreNotSame(previews[0].Players[0], matchPlayers[0]);
            Assert.AreNotSame(second.Request.homeTeam.Players[0], matchPlayers[0]);
            var originalSecondName = second.Request.homeTeam.Players[0].Name;
            matchPlayers[0].Name = "Only this match";
            Assert.AreEqual(originalSecondName, second.Request.homeTeam.Players[0].Name);
            adapter.Dispose();
            adapter.Dispose();
            Assert.IsTrue(previews.All(team => team == null));
            Assert.IsTrue(previewPlayers.All(player => player == null));
            Assert.IsTrue(lease.Request.homeTeam != null && lease.Request.awayTeam != null);
            Assert.IsTrue(matchPlayers.All(player => player != null));
            Assert.AreEqual(22, lease.Players.Count);
            Assert.IsFalse(adapter.TryCreateMatch(lease.HomeClubId, lease.AwayClubId, out _, out var error));
            Assert.AreEqual(GameText.Get("adapter.released"), error);
            lease.Dispose();
            lease.Dispose();
            Assert.IsTrue(lease.IsDisposed);
            Assert.IsTrue(lease.Request.homeTeam == null && lease.Request.awayTeam == null);
            Assert.IsTrue(matchPlayers.All(player => player == null));
            Assert.IsTrue(second.Request.homeTeam != null);
            Assert.IsTrue(bindings.Clubs[0].VisualTemplate.HomeKit != null);
        }

        [Test]
        public void SameClubAndUnknownClubAreRejectedBeforeCreatingAMatch()
        {
            var adapter = CreateAdapter();
            var clubId = adapter.Teams[0].ClubId;
            Assert.IsFalse(adapter.TryCreateMatch(clubId, clubId, out var lease, out var error));
            Assert.IsNull(lease);
            Assert.AreEqual(GameText.Get("match.differentClubs"), error);
            Assert.IsFalse(adapter.TryCreateMatch(clubId, "missing-club", out lease, out error));
            Assert.IsNull(lease);
            Assert.IsNotEmpty(error);
        }

        [TestCase("community-model", 1, "football-player-v1")]
        [TestCase("builtin-player", 2, "football-player-v1")]
        [TestCase("builtin-player", 1, "unknown-profile")]
        public void UnsupportedSelectedSkinBlocksThatClub(string skinId, int revision, string profile)
        {
            var playerId = imported.Catalog.GetRoster(imported.Catalog.Clubs[0].Id).Single(player => player.NaturalPositions.Contains(PlayerPosition.GK)).Id;
            var visuals = imported.VisualProfiles.Select(item => item.PlayerId == playerId
                ? new VisualProfileData(playerId, new SkinReferenceData(skinId, revision, profile)) : item).ToArray();
            var adapter = CreateAdapter(visualProfiles: visuals);
            var invalid = adapter.Teams[0];
            Assert.IsFalse(invalid.CanPlay);
            Assert.IsTrue(invalid.Preview == null);
            Assert.AreEqual(GameText.Get("adapter.unsupportedVisual", imported.Catalog.GetPlayer(playerId).Name), invalid.Error);
            StringAssert.Contains(imported.Catalog.GetPlayer(playerId).Name, invalid.Error);
            StringAssert.DoesNotContain(playerId, invalid.Error);
            Assert.IsTrue(adapter.Teams[1].CanPlay);
            Assert.IsFalse(adapter.TryCreateMatch(invalid.ClubId, adapter.Teams[1].ClubId, out var lease, out var error));
            Assert.IsNull(lease);
            Assert.AreEqual(invalid.Error, error);
        }

        [Test]
        public void MissingVisualProfilesUseSupportedBuiltinAppearance()
        {
            var adapter = CreateAdapter(visualProfiles: Array.Empty<VisualProfileData>());
            Assert.IsTrue(adapter.Teams.All(option => option.CanPlay));
        }

        [Test]
        public void CompletePortableAppearanceNeedsNoLegacyPlayerBindingAndChangesOnlyCosmetics()
        {
            var appearance = new BuiltinAppearanceData("tone-6", "mohawk", "blue", "full", "white", "cyan", "gray");
            var profiles = imported.VisualProfiles.Select(profile =>
                new VisualProfileData(profile.PlayerId, profile.Skin, appearance)).ToArray();
            var withoutPlayers = ScriptableObject.CreateInstance<LegacyMatchBindings>();
            ownedObjects.Add(withoutPlayers);
            withoutPlayers.Clubs = bindings.Clubs;
            withoutPlayers.DefaultVisualTemplate = bindings.DefaultVisualTemplate;
            withoutPlayers.DefaultFormation = bindings.DefaultFormation;
            withoutPlayers.DefaultPlayerAppearance = null;
            withoutPlayers.Players = Array.Empty<PlayerAppearanceBinding>();
            var adapter = CreateAdapter(visualProfiles: profiles, localBindings: withoutPlayers);
            foreach (var option in adapter.Teams)
            {
                Assert.IsTrue(option.CanPlay, option.Error);
                Assert.IsNull(option.Warning, "A complete appearance must not use a legacy cosmetic fallback.");
                foreach (var player in option.Preview.Players)
                {
                    Assert.AreEqual(SkinColor.SuperDark, player.SkinColor);
                    Assert.AreEqual(HairStyles.Mohawk, player.HairStyles);
                    Assert.AreEqual(HairColors.Blue, player.HairColor);
                    Assert.AreEqual(FacialHairStyles.LongBeard, player.FacialHairStyles);
                    Assert.AreEqual(HairColors.White, player.FacialHairColor);
                    Assert.AreEqual(BootColor.Cyan, player.BootColor);
                    Assert.AreEqual(SockAccessoryColor.Gray, player.SockAccessoryColor);
                }
            }
            var lease = CreateLease(adapter, adapter.Teams[0].ClubId, adapter.Teams[1].ClubId);
            var second = CreateLease(adapter, lease.HomeClubId, lease.AwayClubId);
            foreach (var identity in lease.Players)
            {
                var team = identity.LocalId < 11 ? lease.Request.homeTeam : lease.Request.awayTeam;
                var player = team.Players[identity.LocalId % 11];
                AssertSportingData(imported.Catalog.GetPlayer(identity.PlayerId), player);
                Assert.AreEqual(HairStyles.Mohawk, player.HairStyles);
                Assert.AreEqual(BootColor.Cyan, player.BootColor);
            }
            lease.Request.homeTeam.Players[0].HairStyles = HairStyles.None;
            lease.Request.homeTeam.Players[0].BootColor = BootColor.Black;
            Assert.AreEqual(HairStyles.Mohawk, second.Request.homeTeam.Players[0].HairStyles);
            Assert.AreEqual(BootColor.Cyan, second.Request.homeTeam.Players[0].BootColor);
            Assert.AreEqual(HairStyles.Mohawk, adapter.Teams[0].Preview.Players[0].HairStyles);
        }

        [Test]
        public void PortableAppearanceOverridesLocalBindingWhileOmittedAppearanceKeepsIt()
        {
            var firstProfile = imported.VisualProfiles[0];
            var edited = new BuiltinAppearanceData("tone-1", "none", "white", "none", "white", "white", "none");
            var profiles = imported.VisualProfiles.Select(profile => profile.PlayerId == firstProfile.PlayerId
                ? new VisualProfileData(profile.PlayerId, profile.Skin, edited) : profile).ToArray();
            var adapter = CreateAdapter(visualProfiles: profiles);
            var lease = CreateLease(adapter, adapter.Teams[0].ClubId, adapter.Teams[1].ClubId);
            foreach (var identity in lease.Players)
            {
                var team = identity.LocalId < 11 ? lease.Request.homeTeam : lease.Request.awayTeam;
                var player = team.Players[identity.LocalId % 11];
                AssertSportingData(imported.Catalog.GetPlayer(identity.PlayerId), player);
                if (identity.PlayerId == firstProfile.PlayerId)
                {
                    Assert.AreEqual(SkinColor.SuperBright, player.SkinColor);
                    Assert.AreEqual(HairStyles.None, player.HairStyles);
                    Assert.AreEqual(FacialHairStyles.None, player.FacialHairStyles);
                    Assert.AreEqual(HairColors.White, player.HairColor);
                    Assert.AreEqual(HairColors.White, player.FacialHairColor);
                    Assert.AreEqual(BootColor.White, player.BootColor);
                    Assert.AreEqual(SockAccessoryColor.None, player.SockAccessoryColor);
                }
                else
                {
                    AssertAppearance(bindings.Players.Single(binding => binding.PlayerId == identity.PlayerId).Appearance, player);
                }
            }
            Assert.That(lease.Players.Any(identity => identity.PlayerId == firstProfile.PlayerId), Is.True);
        }

        [Test]
        public void FullRosterAndUnsupportedReserveRemainInCatalogWithoutEnteringEleven()
        {
            var catalog = imported.Catalog;
            var keeper = catalog.GetRoster(catalog.Clubs[0].Id).Single(player => player.NaturalPositions.Contains(PlayerPosition.GK));
            var reserve = new PlayerDefinition("zzz-reserve-keeper", "Reserve keeper", keeper.NaturalPositions,
                keeper.HeightCm, keeper.WeightKg, keeper.Attributes);
            var largerCatalog = new DatabaseCatalog(catalog.DatabaseId, catalog.DatabaseRevision,
                catalog.Clubs, catalog.Players.Concat(new[] { reserve }), catalog.Memberships.Concat(new[] { new RosterMembership(catalog.Clubs[0].Id, reserve.Id) }));
            var visuals = imported.VisualProfiles.Concat(new[] { new VisualProfileData(reserve.Id, new SkinReferenceData("unprepared-reserve", 1, "future")) }).ToArray();
            var adapter = CreateAdapter(largerCatalog, visuals);
            Assert.IsTrue(adapter.Teams[0].CanPlay, adapter.Teams[0].Error);
            var lease = CreateLease(adapter, catalog.Clubs[0].Id, catalog.Clubs[1].Id);
            Assert.AreEqual(12, largerCatalog.GetRoster(catalog.Clubs[0].Id).Count);
            Assert.AreEqual(45, largerCatalog.Players.Count);
            Assert.IsFalse(lease.Players.Any(player => player.PlayerId == reserve.Id));
            Assert.AreEqual(11, lease.Request.homeTeam.Players.Length);
            Assert.AreSame(largerCatalog, lease.Catalog);
        }

        [TestCase(CareerFormation.FourFourTwo, CareerMentality.Defensive, Formations._4_4_2, TacticPresetTypes.Defensive, false)]
        [TestCase(CareerFormation.FourThreeThree, CareerMentality.Attacking, Formations._4_3_3, TacticPresetTypes.Offensive, true)]
        [TestCase(CareerFormation.FourTwoThreeOne, CareerMentality.Balanced, Formations._4_2_3_1_A, TacticPresetTypes.Balanced, false)]
        public void CareerTacticsApplyOnlyToControlledMatchClonesAndPreserveOrdinaryPreviews(CareerFormation formation,
            CareerMentality mentality, Formations expectedFormation, TacticPresetTypes expectedTactic, bool controlledAway)
        {
            var catalog = imported.Catalog;
            var controlledId = catalog.Clubs[0].Id;
            var opponentId = catalog.Clubs[1].Id;
            var ordinary = CreateAdapter();
            var ordinaryFormation = ordinary.Teams.Single(value => value.ClubId == controlledId).Preview.Formation;
            var career = CreateAdapter(careerOptions: new CareerMatchOptions(controlledId, formation, mentality));
            var lease = CreateLease(career, controlledAway ? opponentId : controlledId, controlledAway ? controlledId : opponentId);
            var controlled = controlledAway ? lease.Request.awayTeam : lease.Request.homeTeam;
            var opponent = controlledAway ? lease.Request.homeTeam : lease.Request.awayTeam;
            Assert.That(controlled.Formation, Is.EqualTo(expectedFormation));
            Assert.That(opponent.Formation, Is.EqualTo(ordinary.Teams.Single(value => value.ClubId == opponentId).Preview.Formation));
            Assert.That(lease.Request.InitialUserTactic, Is.EqualTo(expectedTactic));
            var instructions = controlledAway ? lease.Request.AwayPlayerInstructions : lease.Request.HomePlayerInstructions;
            Assert.That(instructions.Count, Is.EqualTo(11));
            Assert.That(instructions.All(value => value.HorizontalOffset == 0 && value.DepthOffset == 0), Is.True);
            Assert.That(controlledAway ? lease.Request.HomePlayerInstructions : lease.Request.AwayPlayerInstructions, Is.Null);
            Assert.That(ordinary.Teams.Single(value => value.ClubId == controlledId).Preview.Formation, Is.EqualTo(ordinaryFormation));
            Assert.That(controlled.Players[0].Position, Is.EqualTo(Positions.GK));
            Assert.That(lease.Players.Count, Is.EqualTo(22));
            foreach (var identity in lease.Players)
            {
                var team = identity.LocalId < 11 ? lease.Request.homeTeam : lease.Request.awayTeam;
                AssertSportingData(catalog.GetPlayer(identity.PlayerId), team.Players[identity.LocalId % 11]);
            }
            var unrelated = CreateLease(career, catalog.Clubs[1].Id, catalog.Clubs[2].Id);
            Assert.That(unrelated.Request.InitialUserTactic, Is.Null);
            Assert.That(unrelated.Request.HomePlayerInstructions, Is.Null);
            Assert.That(unrelated.Request.AwayPlayerInstructions, Is.Null);
            var quick = CreateLease(ordinary, controlledId, opponentId);
            Assert.That(quick.Request.InitialUserTactic, Is.Null);
            Assert.That(quick.Request.HomePlayerInstructions, Is.Null);
            Assert.That(quick.Request.AwayPlayerInstructions, Is.Null);
        }

        [Test]
        public void CareerAdjustedRoleRemainsPinnedToControlledSlotWithoutChangingPlayerDataOrAssets()
        {
            var catalog = imported.Catalog;
            var plan = CareerTacticPlan.CreateDefault(CareerFormation.FourFourTwo);
            var slot = plan.GetSlot("left-back");
            plan = plan.WithSlot(slot.SlotId, slot.X + .08f, slot.Depth + .05f, CareerPlayerRole.CrossingFullBack);
            var adapter = CreateAdapter(careerOptions: new CareerMatchOptions(catalog.Clubs[0].Id,
                plan.Formation, CareerMentality.Balanced, plan));
            var lease = CreateLease(adapter, catalog.Clubs[1].Id, catalog.Clubs[0].Id);
            var instruction = lease.Request.AwayPlayerInstructions[1];
            Assert.That(instruction.SlotId, Is.EqualTo("left-back"));
            Assert.That(instruction.Position, Is.EqualTo(Positions.LB));
            Assert.That(instruction.HorizontalOffset, Is.EqualTo(-.08f).Within(.0001));
            Assert.That(instruction.DepthOffset, Is.EqualTo(.05f).Within(.0001));
            Assert.That(instruction.Role.EarlyCross, Is.True);
            Assert.That(lease.Request.HomePlayerInstructions, Is.Null);
            var identity = lease.Players.Single(value => value.LocalId == 12);
            AssertSportingData(catalog.GetPlayer(identity.PlayerId), lease.Request.awayTeam.Players[1]);
            // Disposal owns only the temporary players/teams; the immutable instruction survives unchanged.
            lease.Dispose();
            Assert.That(instruction.Role.EarlyCross, Is.True);
        }

        [Test]
        public void CareerFourThreeThreeReplansAllRosterMembersIntoItsNaturalWingSlots()
        {
            var catalog = imported.Catalog;
            var club = catalog.Clubs[0];
            var source = catalog.GetRoster(club.Id)[1];
            var left = new PlayerDefinition("career-left-wing", "Left winger", new[] { PlayerPosition.LW }, source.HeightCm, source.WeightKg, source.Attributes);
            var right = new PlayerDefinition("career-right-wing", "Right winger", new[] { PlayerPosition.RW }, source.HeightCm, source.WeightKg, source.Attributes);
            var expanded = new DatabaseCatalog(catalog.DatabaseId, catalog.DatabaseRevision, catalog.Clubs,
                catalog.Players.Concat(new[] { left, right }), catalog.Memberships.Concat(new[] {
                    new RosterMembership(club.Id, left.Id), new RosterMembership(club.Id, right.Id) }));
            var adapter = CreateAdapter(expanded, careerOptions: new CareerMatchOptions(club.Id,
                CareerFormation.FourThreeThree, CareerMentality.Attacking));
            var lease = CreateLease(adapter, club.Id, catalog.Clubs[1].Id);
            var homeIds = lease.Players.Where(value => value.ClubId == club.Id).OrderBy(value => value.LocalId).ToArray();
            Assert.That(homeIds[8].PlayerId, Is.EqualTo(left.Id));
            Assert.That(homeIds[9].PlayerId, Is.EqualTo(right.Id));
            Assert.That(lease.Request.homeTeam.Players[8].Position, Is.EqualTo(Positions.LW));
            Assert.That(lease.Request.homeTeam.Players[9].Position, Is.EqualTo(Positions.RW));
            Assert.That(expanded.GetRoster(club.Id).Count, Is.EqualTo(13));
            Assert.That(catalog.GetRoster(club.Id).Count, Is.EqualTo(11));
        }

        [Test]
        public void EffectiveTransferredMembershipFeedsTheNewClubWhileKeepingPlayerIdentityAndAppearance()
        {
            var catalog = imported.Catalog;
            var buyer = catalog.Clubs[0].Id;
            var seller = catalog.Clubs[1].Id;
            var recruit = catalog.GetRoster(seller).First(value => !value.NaturalPositions.Contains(PlayerPosition.GK));
            var specialist = new PlayerDefinition(recruit.Id, recruit.Name, new[] { PlayerPosition.AM },
                recruit.HeightCm, recruit.WeightKg, recruit.Attributes);
            var effective = new DatabaseCatalog(catalog.DatabaseId, catalog.DatabaseRevision, catalog.Clubs,
                catalog.Players.Select(value => value.Id == recruit.Id ? specialist : value),
                catalog.Memberships.Select(value => value.PlayerId == recruit.Id ? new RosterMembership(buyer, recruit.Id) : value));
            var adapter = CreateAdapter(effective, careerOptions: new CareerMatchOptions(buyer,
                CareerFormation.FourTwoThreeOne, CareerMentality.Balanced));
            var lease = CreateLease(adapter, buyer, catalog.Clubs[2].Id);
            var identity = lease.Players.Single(value => value.PlayerId == recruit.Id);
            Assert.That(identity.ClubId, Is.EqualTo(buyer));
            Assert.That(identity.LocalId, Is.EqualTo(9));
            Assert.That(lease.Request.homeTeam.Players[9].Position, Is.EqualTo(Positions.AMF));
            AssertSportingData(specialist, lease.Request.homeTeam.Players[9]);
            AssertAppearance(bindings.Players.Single(value => value.PlayerId == recruit.Id).Appearance, lease.Request.homeTeam.Players[9]);
            Assert.That(lease.Request.homeTeam.HomeKit, Is.SameAs(bindings.Clubs.Single(value => value.ClubId == buyer).VisualTemplate.HomeKit));
            Assert.That(effective.GetRoster(seller).Any(value => value.Id == recruit.Id), Is.False);
            Assert.That(catalog.GetRoster(seller).Any(value => value.Id == recruit.Id), Is.True);
        }

        [Test]
        public void CareerTacticOptionsRejectUnknownChoicesAndUnknownControlledClubs()
        {
            Assert.Throws<ArgumentException>(() => new CareerMatchOptions("club", (CareerFormation)999, CareerMentality.Balanced));
            Assert.Throws<ArgumentException>(() => new CareerMatchOptions("club", CareerFormation.FourFourTwo, (CareerMentality)999));
            Assert.Throws<ArgumentException>(() => new CareerMatchOptions(" ", CareerFormation.FourFourTwo, CareerMentality.Balanced));
            Assert.Throws<KeyNotFoundException>(() => CreateAdapter(careerOptions:
                new CareerMatchOptions("unknown-club", CareerFormation.FourFourTwo, CareerMentality.Balanced)));
        }

        [Test]
        public void IncompleteRosterHasNoPreviewAndPreservesTheOtherClubs()
        {
            var catalog = imported.Catalog;
            var excluded = catalog.GetRoster(catalog.Clubs[0].Id)[1].Id;
            var incomplete = new DatabaseCatalog(catalog.DatabaseId, catalog.DatabaseRevision, catalog.Clubs,
                catalog.Players, catalog.Memberships.Where(item => item.PlayerId != excluded));
            var adapter = CreateAdapter(incomplete);
            Assert.IsFalse(adapter.Teams[0].CanPlay);
            Assert.IsTrue(adapter.Teams[0].Preview == null);
            Assert.AreEqual(GameText.Get("adapter.shortRoster", 10), adapter.Teams[0].Error);
            StringAssert.DoesNotContain(catalog.Clubs[0].Id, adapter.Teams[0].Error);
            Assert.IsTrue(adapter.Teams[1].CanPlay);
            Assert.AreEqual(10, incomplete.GetRoster(catalog.Clubs[0].Id).Count);
        }

        [Test]
        public void RosterWithoutNaturalGoalkeeperCannotStart()
        {
            var keeper = imported.Catalog.GetRoster(imported.Catalog.Clubs[0].Id).Single(player => player.NaturalPositions.Contains(PlayerPosition.GK));
            var converted = new PlayerDefinition(keeper.Id, keeper.Name, new[] { PlayerPosition.CM }, keeper.HeightCm, keeper.WeightKg, keeper.Attributes);
            var catalog = ReplacePlayers(imported.Catalog.Players.Select(player => player.Id == keeper.Id ? converted : player));
            var adapter = CreateAdapter(catalog);
            Assert.IsFalse(adapter.Teams[0].CanPlay);
            Assert.AreEqual(GameText.Get("adapter.goalkeeper"), adapter.Teams[0].Error);
        }

        [Test]
        public void NewClubAndPlayersUseDeclaredDefaultsAndDisplayWarnings()
        {
            var catalog = imported.Catalog;
            var club = new ClubDefinition("new-club", "New club from JSON");
            var players = catalog.GetRoster(catalog.Clubs[0].Id).Select((player, index) =>
                new PlayerDefinition("new-player-" + index.ToString("D2"), "New " + player.Name,
                    player.NaturalPositions, player.HeightCm, player.WeightKg, player.Attributes)).ToArray();
            var expanded = new DatabaseCatalog(catalog.DatabaseId, catalog.DatabaseRevision, catalog.Clubs.Concat(new[] { club }),
                catalog.Players.Concat(players), catalog.Memberships.Concat(players.Select(player => new RosterMembership(club.Id, player.Id))));
            var adapter = CreateAdapter(expanded);
            var option = adapter.Teams.Single(item => item.ClubId == club.Id);
            Assert.IsTrue(option.CanPlay, option.Error);
            StringAssert.Contains(GameText.Get("adapter.genericClub"), option.Warning);
            StringAssert.Contains(GameText.Get("adapter.defaultAppearance"), option.Warning);
            StringAssert.Contains(GameText.Get("adapter.outOfPosition", string.Empty).TrimStart(), option.Warning);
            Assert.AreEqual(Formations._4_4_2, option.Preview.Formation);
            Assert.AreSame(bindings.DefaultVisualTemplate.HomeKit, option.Preview.HomeKit);
            Assert.AreEqual(club.Name, option.Preview.TeamName);
            var lease = CreateLease(adapter, club.Id, catalog.Clubs[1].Id);
            foreach (var identity in lease.Players.Where(item => item.ClubId == club.Id))
            {
                var player = lease.Request.homeTeam.Players[identity.LocalId];
                AssertSportingData(expanded.GetPlayer(identity.PlayerId), player);
                AssertAppearance(bindings.DefaultPlayerAppearance, player);
            }
        }

        [Test]
        public void RenamesAndJsonOrderDoNotChangeIdentityOrVisualBindings()
        {
            var catalog = imported.Catalog;
            var renamedClubs = catalog.Clubs.Select(club => new ClubDefinition(club.Id, "Same displayed club name")).Reverse().ToArray();
            var renamedPlayers = catalog.Players.Select(player => new PlayerDefinition(player.Id, "Same displayed player name",
                player.NaturalPositions, player.HeightCm, player.WeightKg, player.Attributes)).Reverse().ToArray();
            var renamed = new DatabaseCatalog(catalog.DatabaseId, catalog.DatabaseRevision + 1, renamedClubs,
                renamedPlayers, catalog.Memberships.Reverse());
            var originalAdapter = CreateAdapter();
            var adapter = CreateAdapter(renamed);
            var originalLease = CreateLease(originalAdapter, catalog.Clubs[0].Id, catalog.Clubs[1].Id);
            var lease = CreateLease(adapter, catalog.Clubs[0].Id, catalog.Clubs[1].Id);
            CollectionAssert.AreEqual(originalLease.Players.Select(player => player.PlayerId), lease.Players.Select(player => player.PlayerId));
            CollectionAssert.AreEqual(originalLease.Players.Select(player => player.ClubId), lease.Players.Select(player => player.ClubId));
            Assert.AreEqual("Same displayed club name", lease.Request.homeTeam.TeamName);
            Assert.IsTrue(lease.Request.homeTeam.Players.All(player => player.Name == "Same displayed player name" && player.name == player.Name));
            Assert.AreSame(originalLease.Request.homeTeam.HomeKit, lease.Request.homeTeam.HomeKit);
            Assert.AreSame(catalog, originalLease.Catalog);
            Assert.AreSame(renamed, lease.Catalog);
            Assert.AreEqual(catalog.DatabaseRevision + 1, lease.Catalog.DatabaseRevision);
        }

        [Test]
        public void MissingDeclaredVisualDefaultProducesAnErrorInsteadOfNullReference()
        {
            var localBindings = ScriptableObject.CreateInstance<LegacyMatchBindings>();
            ownedObjects.Add(localBindings);
            localBindings.DefaultPlayerAppearance = bindings.DefaultPlayerAppearance;
            var adapter = CreateAdapter(localBindings: localBindings);
            Assert.IsTrue(adapter.Teams.All(option => !option.CanPlay && option.Preview == null));
            Assert.AreEqual(GameText.Get("adapter.missingBadge"), adapter.Teams[0].Error);
        }

        private CatalogMatchAdapter CreateAdapter(DatabaseCatalog catalog = null,
            IReadOnlyList<VisualProfileData> visualProfiles = null, LegacyMatchBindings localBindings = null,
            CareerMatchOptions careerOptions = null)
        {
            var adapter = new CatalogMatchAdapter(catalog ?? imported.Catalog, visualProfiles ?? imported.VisualProfiles, localBindings ?? bindings, careerOptions);
            ownedSessions.Add(adapter);
            return adapter;
        }

        private CatalogMatchLease CreateLease(CatalogMatchAdapter adapter, string homeId, string awayId)
        {
            Assert.IsTrue(adapter.TryCreateMatch(homeId, awayId, out var lease, out var error), error);
            ownedSessions.Add(lease);
            return lease;
        }

        private DatabaseCatalog ReplacePlayers(IEnumerable<PlayerDefinition> players)
            => new DatabaseCatalog(imported.Catalog.DatabaseId, imported.Catalog.DatabaseRevision,
                imported.Catalog.Clubs, players, imported.Catalog.Memberships);

        private void Snapshot(UnityEngine.Object source)
        {
            Assert.IsTrue(source != null, "A source reference is missing from the bindings.");
            if (!sourceSnapshots.ContainsKey(source)) sourceSnapshots.Add(source, EditorJsonUtility.ToJson(source));
        }

        private static void AssertAppearance(PlayerEntry expected, PlayerEntry actual)
        {
            Assert.AreEqual(expected.SkinColor, actual.SkinColor);
            Assert.AreEqual(expected.HairStyles, actual.HairStyles);
            Assert.AreEqual(expected.HairColor, actual.HairColor);
            Assert.AreEqual(expected.FacialHairStyles, actual.FacialHairStyles);
            Assert.AreEqual(expected.FacialHairColor, actual.FacialHairColor);
            Assert.AreEqual(expected.BootColor, actual.BootColor);
            Assert.AreEqual(expected.SockAccessoryColor, actual.SockAccessoryColor);
        }

        private static void AssertSportingData(PlayerDefinition expected, PlayerEntry actual)
        {
            Assert.AreEqual(expected.DisplayName, actual.Name);
            Assert.AreEqual(expected.DisplayName, actual.name);
            Assert.AreEqual(expected.HeightCm, actual.height);
            Assert.AreEqual(expected.WeightKg, actual.weight);
            var attributes = expected.Attributes;
            Assert.AreEqual(attributes.Strength, actual.strength);
            Assert.AreEqual(attributes.Acceleration, actual.acceleration);
            Assert.AreEqual(attributes.TopSpeed, actual.topSpeed);
            Assert.AreEqual(attributes.DribbleSpeed, actual.dribbleSpeed);
            Assert.AreEqual(attributes.Jump, actual.jump);
            Assert.AreEqual(attributes.Tackling, actual.tackling);
            Assert.AreEqual(attributes.BallKeeping, actual.ballKeeping);
            Assert.AreEqual(attributes.Passing, actual.passing);
            Assert.AreEqual(attributes.LongBall, actual.longBall);
            Assert.AreEqual(attributes.Agility, actual.agility);
            Assert.AreEqual(attributes.Shooting, actual.shooting);
            Assert.AreEqual(attributes.ShootPower, actual.shootPower);
            Assert.AreEqual(attributes.Positioning, actual.positioning);
            Assert.AreEqual(attributes.Reaction, actual.reaction);
            Assert.AreEqual(attributes.BallControl, actual.ballControl);
        }
    }
}
#endif
