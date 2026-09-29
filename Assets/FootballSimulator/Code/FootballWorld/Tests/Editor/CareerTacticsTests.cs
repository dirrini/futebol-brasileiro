using System;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;
using NUnit.Framework;

namespace FStudio.FootballWorld.Tests
{
    public sealed class CareerTacticsTests
    {
        [TestCase(CareerFormation.FourFourTwo)]
        [TestCase(CareerFormation.FourThreeThree)]
        [TestCase(CareerFormation.FourTwoThreeOne)]
        public void EveryFormationAcceptsRoleBoundsAndCanonicalizesSlotsByIdentity(CareerFormation formation)
        {
            var definitions = CareerTacticPlan.GetSlotDefinitions(formation);
            var defaults = CareerTacticPlan.CreateDefault(formation);
            Assert.That(defaults.IsDefault, Is.True);
            Assert.That(definitions.Count, Is.EqualTo(11));
            Assert.That(definitions.Select(value => value.SlotId).Distinct().Count(), Is.EqualTo(11));
            var reversed = new CareerTacticPlan(formation, defaults.Slots.Reverse());
            Assert.That(reversed.SameConfiguration(defaults), Is.True);
            Assert.That(reversed.Slots.Select(value => value.SlotId), Is.EqualTo(definitions.Select(value => value.SlotId)));
            foreach (var definition in definitions)
            foreach (var role in definition.AllowedRoles)
            {
                Assert.DoesNotThrow(() => defaults.WithSlot(definition.SlotId, definition.MinX, definition.MinDepth, role));
                Assert.DoesNotThrow(() => defaults.WithSlot(definition.SlotId, definition.MaxX, definition.MaxDepth, role));
            }
        }

        [Test]
        public void InvalidCoordinatesRolesAndSlotIdentitiesCannotProduceAPlan()
        {
            var plan = CareerTacticPlan.CreateDefault(CareerFormation.FourFourTwo);
            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -.01f, 1.01f })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => plan.WithSlot("left-back", invalid, .31f, CareerPlayerRole.Standard));
                Assert.Throws<ArgumentOutOfRangeException>(() => plan.WithSlot("left-back", .11f, invalid, CareerPlayerRole.Standard));
            }
            Assert.Throws<ArgumentException>(() => plan.WithSlot("goalkeeper", .5f, .21f, CareerPlayerRole.Standard));
            Assert.Throws<ArgumentException>(() => plan.WithSlot("left-back", .5f, .31f, CareerPlayerRole.Standard));
            Assert.Throws<ArgumentException>(() => plan.WithSlot("left-back", .11f, .6f, CareerPlayerRole.Standard));
            Assert.Throws<ArgumentException>(() => plan.WithSlot("left-back", .11f, .31f, CareerPlayerRole.TargetForward));
            Assert.Throws<ArgumentException>(() => plan.WithSlot("goalkeeper", .5f, .12f, CareerPlayerRole.DeepLyingPlaymaker));
            Assert.Throws<ArgumentException>(() => plan.WithSlot("left-back", .11f, .31f, (CareerPlayerRole)999));
            Assert.Throws<ArgumentException>(() => plan.WithSlot("player-1", .11f, .31f, CareerPlayerRole.Standard));
            Assert.Throws<ArgumentException>(() => new CareerTacticPlan(plan.Formation, plan.Slots.Take(10)));
            Assert.Throws<ArgumentException>(() => new CareerTacticPlan(plan.Formation, plan.Slots.Take(10).Concat(new[] {plan.Slots[0]})));
            Assert.Throws<ArgumentException>(() => new CareerTacticPlan(plan.Formation, plan.Slots.Take(10).Concat(new[] {new CareerTacticSlot("player-1", .5f, .5f, CareerPlayerRole.Standard)})));
            Assert.Throws<ArgumentException>(() => new CareerTacticPlan(CareerFormation.FourThreeThree, plan.Slots));
            Assert.Throws<ArgumentException>(() => new CareerTacticPlan(plan.Formation, plan.Slots, version: 2));
            Assert.Throws<ArgumentException>(() => CareerTacticPlan.CreateDefault((CareerFormation)999));
            Assert.That(plan.IsDefault, Is.True);
        }

        [Test]
        public void FineAdjustmentsAreImmutableAndFormationChangesResetOnlyTheirOwnPlan()
        {
            var career = Create();
            var original = career.TacticPlan;
            var customized = original.WithSlot("left-back", .20f, .45f, CareerPlayerRole.InvertedFullBack);
            career.SetTactics(career.Formation, CareerMentality.Attacking, customized);
            var snapshot = career.CaptureSnapshot();
            Assert.That(original.IsDefault, Is.True);
            Assert.That(customized.IsDefault, Is.False);
            career.SetTactics(career.Formation, CareerMentality.Defensive);
            Assert.That(career.TacticPlan, Is.SameAs(customized));
            Assert.Throws<ArgumentException>(() => career.SetTactics(CareerFormation.FourThreeThree, CareerMentality.Balanced, customized));
            Assert.That(career.Formation, Is.EqualTo(CareerFormation.FourFourTwo));
            Assert.That(career.Mentality, Is.EqualTo(CareerMentality.Defensive));
            Assert.That(career.TacticPlan, Is.SameAs(customized));
            career.SetTactics(CareerFormation.FourTwoThreeOne, CareerMentality.Balanced);
            Assert.That(career.TacticPlan.Formation, Is.EqualTo(CareerFormation.FourTwoThreeOne));
            Assert.That(career.TacticPlan.IsDefault, Is.True);
            Assert.That(snapshot.TacticPlan, Is.SameAs(customized));
            Assert.That(snapshot.Mentality, Is.EqualTo(CareerMentality.Attacking));
        }

        [Test]
        public void CurrentInstructionsSurviveSportingAndFinanceReplayWithoutChangingPinnedContent()
        {
            var career = Create();
            var source = career.Competition.Catalog;
            var plan = CareerTacticPlan.CreateDefault(CareerFormation.FourTwoThreeOne)
                .WithSlot("attacking-midfielder", .57f, .78f, CareerPlayerRole.CreativePlaymaker)
                .WithSlot("striker", .48f, .88f, CareerPlayerRole.TargetForward);
            career.SetTactics(plan.Formation, CareerMentality.Attacking, plan);
            career.AdvanceToNextFixture();
            career.SimulateNextFixture();
            var finance = career.FinanceBalance;
            var results = career.Competition.Results.Count;
            for (var i = 0; i < 3; i++) career = CareerSession.Restore(source, career.CaptureSnapshot());
            Assert.That(career.TacticPlan.SameConfiguration(plan), Is.True);
            Assert.That(career.Mentality, Is.EqualTo(CareerMentality.Attacking));
            Assert.That(career.FinanceBalance, Is.EqualTo(finance));
            Assert.That(career.Competition.Results.Count, Is.EqualTo(results));
            Assert.That(career.Competition.Catalog, Is.SameAs(source));
        }

        [Test]
        public void AdjustmentsAreRejectedDuringExecutionAndAfterCalendarCompletion()
        {
            var career = Create();
            var original = career.TacticPlan;
            var custom = original.WithSlot("left-centre-back", .40f, .35f, CareerPlayerRole.AdvancingCentreBack);
            career.AdvanceToNextFixture();
            var execution = career.BeginFixture();
            Assert.Throws<InvalidOperationException>(() => career.SetTactics(career.Formation, career.Mentality, custom));
            Assert.That(career.TacticPlan, Is.SameAs(original));
            career.Competition.AbortFixture(execution.FixtureId, execution.ExecutionId);
            while (!career.IsComplete)
            {
                if (career.IsMatchDay) career.SimulateNextFixture();
                else career.AdvanceToNextFixture();
            }
            Assert.Throws<InvalidOperationException>(() => career.SetTactics(career.Formation, career.Mentality, custom));
            Assert.That(career.TacticPlan, Is.SameAs(original));
        }

        private static CareerSession Create()
        {
            var clubs = Enumerable.Range(1, 4).Select(value => new ClubDefinition("club-" + value, "Club " + value)).ToArray();
            var edition = new CompetitionEditionDefinition("edition", "competition", "Test season", clubs.Select(value => value.Id),
                new[] {new GameDate(2026, 1, 10), new GameDate(2026, 1, 20), new GameDate(2026, 2, 10)}, new LeagueRules(1, 3, 1, 0));
            var catalog = new DatabaseCatalog("database", 1, clubs, Array.Empty<PlayerDefinition>(), Array.Empty<RosterMembership>(),
                new[] {new CompetitionDefinition("competition", "Test league")}, new[] {edition});
            return CareerSession.Create(catalog, edition.Id, "season", clubs[0].Id, new GameDate(2026, 1, 1));
        }
    }
}
