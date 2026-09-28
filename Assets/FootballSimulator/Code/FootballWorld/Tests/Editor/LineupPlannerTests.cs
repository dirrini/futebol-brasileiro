using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.Importing;
using NUnit.Framework;

namespace FStudio.FootballWorld.Tests
{
    public sealed class LineupPlannerTests
    {
        private static readonly PlayerPosition[] Slots4141 = {
            PlayerPosition.GK, PlayerPosition.LB, PlayerPosition.CB, PlayerPosition.CB, PlayerPosition.RB,
            PlayerPosition.DM, PlayerPosition.LM, PlayerPosition.CM, PlayerPosition.CM, PlayerPosition.RM, PlayerPosition.ST
        };

        private static readonly PlayerPosition[] Slots433 = {
            PlayerPosition.GK, PlayerPosition.LB, PlayerPosition.CB, PlayerPosition.CB, PlayerPosition.RB,
            PlayerPosition.LM, PlayerPosition.CM, PlayerPosition.RM, PlayerPosition.LW, PlayerPosition.RW, PlayerPosition.ST
        };

        [TestCase("Royal", false, 0)]
        [TestCase("Royal", true, 2)]
        [TestCase("Milano", true, 0)]
        [TestCase("Milano", false, 2)]
        [TestCase("London", false, 0)]
        [TestCase("Catalagna", false, 0)]
        public void PlansLegacyFixtureClubsFor4141And433(string clubName, bool use433, int expectedOutOfPosition)
        {
            var path = Path.Combine(UnityEngine.Application.dataPath,
                "FootballSimulator/Code/FootballWorld/Tests/Fixtures/legacy-four-clubs.database.json");
            var imported = new JsonDatabaseImporter().Import(File.ReadAllText(path));
            Assert.That(imported.Success, Is.True);
            var club = imported.Catalog.Clubs.Single(item => item.Name == clubName);
            var slots = use433 ? Slots433 : Slots4141;
            var plan = LineupPlanner.Plan(imported.Catalog, club.Id, slots);

            AssertLineup(plan, slots);
            Assert.That(plan.OutOfPositionPlayerIds.Count, Is.EqualTo(expectedOutOfPosition));
            Assert.That(plan.Players.Select(player => player.Id),
                Is.EquivalentTo(imported.Catalog.GetRoster(club.Id).Select(player => player.Id)));
        }

        [Test]
        public void SelectsElevenFromALargerRosterWithoutUsingFreeAgentsOrTruncatingTheCatalog()
        {
            var roster = OutfieldRoster(12, PlayerPosition.ST);
            var freeAgent = Player("a-free-agent", PlayerPosition.ST);
            var catalog = Catalog(roster, new[] { freeAgent });
            var plan = LineupPlanner.Plan(catalog, "club", RepeatedSlots(PlayerPosition.ST));

            AssertLineup(plan, RepeatedSlots(PlayerPosition.ST));
            Assert.That(catalog.GetRoster("club").Count, Is.EqualTo(13));
            Assert.That(catalog.Players.Count, Is.EqualTo(14));
            Assert.That(plan.Players.Select(player => player.Id), Does.Not.Contain(freeAgent.Id));
            Assert.That(plan.Players.Skip(1).Select(player => player.Id),
                Is.EquivalentTo(roster.Skip(1).OrderBy(player => player.Id, StringComparer.Ordinal).Take(10).Select(player => player.Id)));
        }

        [Test]
        public void ReorderingPlayersAndMembershipsDoesNotChangeThePlan()
        {
            var roster = OutfieldRoster(13, PlayerPosition.CM);
            roster.Add(Player("A-forward", PlayerPosition.ST));
            roster.Add(Player("a-forward", PlayerPosition.ST));
            var firstCatalog = Catalog(roster);
            var secondCatalog = new DatabaseCatalog("database", 1, firstCatalog.Clubs,
                firstCatalog.Players.Reverse(), firstCatalog.Memberships.Reverse());
            var first = LineupPlanner.Plan(firstCatalog, "club", Slots4141);
            var second = LineupPlanner.Plan(secondCatalog, "club", Slots4141);

            AssertLineup(first, Slots4141);
            AssertLineup(second, Slots4141);
            Assert.That(second.Players.Select(player => player.Id), Is.EqualTo(first.Players.Select(player => player.Id)));
            Assert.That(second.OutOfPositionPlayerIds, Is.EqualTo(first.OutOfPositionPlayerIds));
            Assert.That(first.Players[10].Id, Is.EqualTo("A-forward"));
        }

        [Test]
        public void RematchesFlexiblePlayersInsteadOfLosingANaturalMatchToAGreedyChoice()
        {
            var slots = new[] { PlayerPosition.GK, PlayerPosition.LB, PlayerPosition.RB,
                PlayerPosition.CB, PlayerPosition.CB, PlayerPosition.DM, PlayerPosition.CM,
                PlayerPosition.LM, PlayerPosition.RM, PlayerPosition.LW, PlayerPosition.ST };
            var roster = new List<PlayerDefinition> {
                Player("keeper", PlayerPosition.GK),
                Player("a-flexible", PlayerPosition.LB, PlayerPosition.RB),
                Player("b-left-only", PlayerPosition.LB)
            };
            for (var slot = 3; slot < slots.Length; slot++)
                roster.Add(Player("specialist-" + slot, slots[slot]));

            var plan = LineupPlanner.Plan(Catalog(roster), "club", slots);
            AssertLineup(plan, slots);
            Assert.That(plan.OutOfPositionPlayerIds, Is.Empty);
            Assert.That(plan.Players[1].Id, Is.EqualTo("b-left-only"));
            Assert.That(plan.Players[2].Id, Is.EqualTo("a-flexible"));
        }

        [Test]
        public void PrefersAGoalkeeperOnlyPlayerAndKeepsTheHybridAvailableOutfield()
        {
            var roster = new List<PlayerDefinition> {
                Player("a-hybrid", PlayerPosition.GK, PlayerPosition.ST),
                Player("z-keeper", PlayerPosition.GK)
            };
            for (var i = 0; i < 9; i++) roster.Add(Player("forward-" + i, PlayerPosition.ST));
            var slots = RepeatedSlots(PlayerPosition.ST);
            var plan = LineupPlanner.Plan(Catalog(roster), "club", slots);

            AssertLineup(plan, slots);
            Assert.That(plan.Players[0].Id, Is.EqualTo("z-keeper"));
            Assert.That(plan.Players.Skip(1).Select(player => player.Id), Does.Contain("a-hybrid"));
            Assert.That(plan.OutOfPositionPlayerIds, Is.Empty);
        }

        [Test]
        public void NeverFillsAnOutfieldSlotWithAGoalkeeperOnlyReserve()
        {
            var roster = OutfieldRoster(10, PlayerPosition.CM);
            roster.Add(Player("a-reserve-keeper", PlayerPosition.GK));
            var slots = RepeatedSlots(PlayerPosition.ST);
            var plan = LineupPlanner.Plan(Catalog(roster), "club", slots);

            AssertLineup(plan, slots);
            Assert.That(plan.Players[0].Id, Is.EqualTo("a-reserve-keeper"));
            Assert.That(plan.Players.Skip(1).Select(player => player.Id), Does.Not.Contain("keeper"));
            Assert.That(plan.OutOfPositionPlayerIds, Is.EqualTo(plan.Players.Skip(1).Select(player => player.Id)));
            Assert.That(plan.OutOfPositionPlayerIds,
                Is.EqualTo(roster.Where(player => !player.NaturalPositions.Contains(PlayerPosition.GK))
                    .OrderBy(player => player.Id, StringComparer.Ordinal).Select(player => player.Id)));
        }

        [Test]
        public void UsesOrdinalIdToChooseAmongHybridGoalkeepersWhenNoKeeperOnlyExists()
        {
            var roster = OutfieldRoster(9, PlayerPosition.ST);
            roster.RemoveAt(0);
            roster.Add(Player("z-hybrid", PlayerPosition.GK, PlayerPosition.ST));
            roster.Add(Player("A-hybrid", PlayerPosition.GK, PlayerPosition.ST));
            var plan = LineupPlanner.Plan(Catalog(roster), "club", RepeatedSlots(PlayerPosition.ST));
            AssertLineup(plan, RepeatedSlots(PlayerPosition.ST));
            Assert.That(plan.Players[0].Id, Is.EqualTo("A-hybrid"));
        }

        [Test]
        public void RejectsMissingGoalkeepersTooFewPlayersAndTooFewOutfieldPlayers()
        {
            AssertRejected(LineupPlanner.Plan(Catalog(OutfieldRoster(9, PlayerPosition.ST)), "club", Slots4141), "eleven");
            var noKeeper = Enumerable.Range(0, 11).Select(index => Player("player-" + index, PlayerPosition.ST));
            AssertRejected(LineupPlanner.Plan(Catalog(noKeeper), "club", Slots4141), "GK");
            var tooManyKeepers = OutfieldRoster(9, PlayerPosition.ST);
            tooManyKeepers.Add(Player("reserve-keeper", PlayerPosition.GK));
            AssertRejected(LineupPlanner.Plan(Catalog(tooManyKeepers), "club", Slots4141), "ten outfield");
        }

        [Test]
        public void RejectsInvalidInputsWithAnExplicitEmptyFailureResult()
        {
            var catalog = Catalog(OutfieldRoster(10, PlayerPosition.ST));
            AssertRejected(LineupPlanner.Plan(null, "club", Slots4141), "catalog");
            AssertRejected(LineupPlanner.Plan(catalog, null, Slots4141), "club ID");
            AssertRejected(LineupPlanner.Plan(catalog, "", Slots4141), "club ID");
            AssertRejected(LineupPlanner.Plan(catalog, "club\n", Slots4141), "Invalid club ID");
            AssertRejected(LineupPlanner.Plan(catalog, "missing", Slots4141), "does not exist");
            AssertRejected(LineupPlanner.Plan(catalog, "club", null), "eleven");
            AssertRejected(LineupPlanner.Plan(catalog, "club", Slots4141.Take(10).ToArray()), "eleven");
            AssertRejected(LineupPlanner.Plan(catalog, "club", Slots4141.Concat(new[] {PlayerPosition.ST}).ToArray()), "eleven");
            var invalidSlots = (PlayerPosition[])Slots4141.Clone();
            invalidSlots[0] = PlayerPosition.ST;
            AssertRejected(LineupPlanner.Plan(catalog, "club", invalidSlots), "first formation slot");
            invalidSlots[0] = PlayerPosition.GK;
            invalidSlots[1] = PlayerPosition.GK;
            AssertRejected(LineupPlanner.Plan(catalog, "club", invalidSlots), "remaining ten");
            invalidSlots[1] = (PlayerPosition)999;
            AssertRejected(LineupPlanner.Plan(catalog, "club", invalidSlots), "unknown position");
        }

        [Test]
        public void KeepsAReadOnlySnapshotWhenSlotsAndTheActiveCatalogChange()
        {
            var catalog = Catalog(OutfieldRoster(10, PlayerPosition.CM));
            var slots = RepeatedSlots(PlayerPosition.ST);
            var session = new CatalogSession();
            session.Activate(catalog);
            var plan = LineupPlanner.Plan(session.ActiveCatalog, "club", slots);
            var originalIds = plan.Players.Select(player => player.Id).ToArray();
            var originalOutOfPositionIds = plan.OutOfPositionPlayerIds.ToArray();
            slots[1] = PlayerPosition.CM;
            var replacement = new DatabaseCatalog("database", 2, catalog.Clubs,
                catalog.Players.Select(player => new PlayerDefinition(player.Id, "Changed Name", player.NaturalPositions,
                    player.HeightCm, player.WeightKg, player.Attributes)), catalog.Memberships);
            session.Activate(replacement);

            Assert.That(plan.Success, Is.True);
            Assert.That(plan.Players.Select(player => player.Id), Is.EqualTo(originalIds));
            Assert.That(plan.OutOfPositionPlayerIds, Is.EqualTo(originalOutOfPositionIds));
            foreach (var player in plan.Players)
                Assert.That(player, Is.SameAs(catalog.GetPlayer(player.Id)));
            Assert.Throws<NotSupportedException>(() => ((IList<PlayerDefinition>)plan.Players).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<string>)plan.OutOfPositionPlayerIds).Clear());
            Assert.That(session.ActiveCatalog.DatabaseRevision, Is.EqualTo(2));
        }

        private static void AssertLineup(LineupPlan plan, IReadOnlyList<PlayerPosition> slots)
        {
            Assert.That(plan.Success, Is.True, plan.Error);
            Assert.That(plan.Error, Is.Null);
            Assert.That(plan.Players.Count, Is.EqualTo(11));
            Assert.That(plan.Players.Select(player => player.Id).Distinct().Count(), Is.EqualTo(11));
            Assert.That(plan.Players[0].NaturalPositions, Does.Contain(PlayerPosition.GK));
            Assert.That(plan.Players.Skip(1).All(player => player.NaturalPositions.Any(position => position != PlayerPosition.GK)), Is.True);
            var actualOutOfPosition = Enumerable.Range(1, 10)
                .Where(slot => !plan.Players[slot].NaturalPositions.Contains(slots[slot]))
                .Select(slot => plan.Players[slot].Id);
            Assert.That(plan.OutOfPositionPlayerIds, Is.EqualTo(actualOutOfPosition));
        }

        private static void AssertRejected(LineupPlan plan, string expectedError)
        {
            Assert.That(plan.Success, Is.False);
            Assert.That(plan.Error, Does.Contain(expectedError));
            Assert.That(plan.Players, Is.Empty);
            Assert.That(plan.OutOfPositionPlayerIds, Is.Empty);
        }

        private static PlayerPosition[] RepeatedSlots(PlayerPosition position)
            => new[] {PlayerPosition.GK}.Concat(Enumerable.Repeat(position, 10)).ToArray();

        private static List<PlayerDefinition> OutfieldRoster(int outfieldCount, PlayerPosition position)
        {
            var players = new List<PlayerDefinition> {Player("keeper", PlayerPosition.GK)};
            for (var i = 0; i < outfieldCount; i++)
                players.Add(Player("player-" + i.ToString("D2"), position));
            return players;
        }

        private static DatabaseCatalog Catalog(IEnumerable<PlayerDefinition> roster,
            IEnumerable<PlayerDefinition> freeAgents = null)
        {
            var players = roster.ToList();
            return new DatabaseCatalog("database", 1, new[] {new ClubDefinition("club", "Club")},
                players.Concat(freeAgents ?? Enumerable.Empty<PlayerDefinition>()),
                players.Select(player => new RosterMembership("club", player.Id)));
        }

        private static PlayerDefinition Player(string id, params PlayerPosition[] positions)
            => new PlayerDefinition(id, id, positions, 180, 80,
                new PlayerAttributes(50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50));
    }
}
