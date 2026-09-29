#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using FStudio.Data;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using FStudio.MatchEngine.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace FStudio.FootballWorld.Editor.Tests
{
    public sealed class CareerTacticalInstructionTests
    {
        private static CareerRoleTuning Tuning => Resources.Load<CareerRoleTuning>(CareerRoleTuning.ResourcePath);

        [TestCase(CareerFormation.FourFourTwo, Formations._4_4_2)]
        [TestCase(CareerFormation.FourThreeThree, Formations._4_3_3)]
        [TestCase(CareerFormation.FourTwoThreeOne, Formations._4_2_3_1_A)]
        public void DefaultPlansMapEveryCanonicalSlotWithoutChangingExistingShape(CareerFormation formation, Formations legacy)
        {
            var options = new CareerMatchOptions("club", formation, CareerMentality.Balanced);
            var instructions = options.CreateInstructions();
            CollectionAssert.AreEqual(FormationRules.GetTeamFormation(legacy).Positions, instructions.Select(value => value.Position));
            CollectionAssert.AreEqual(options.TacticPlan.Slots.Select(value => value.SlotId), instructions.Select(value => value.SlotId));
            var origin = new Vector3(23, 0, 47);
            foreach (var instruction in instructions)
            {
                Assert.That(instruction.HorizontalOffset, Is.Zero);
                Assert.That(instruction.DepthOffset, Is.Zero);
                Assert.That(instruction.ApplyPosition(origin, Vector3.right, 100, 60, true, new Vector3(80, 5, 10)), Is.EqualTo(origin));
                Assert.That(instruction.ApplyPosition(origin, Vector3.left, 100, 60, false, Vector3.zero), Is.EqualTo(origin));
                Assert.That(instruction.PassPriority(false, 25, 0), Is.Zero);
                Assert.That(instruction.Role.MarkingWeight, Is.EqualTo(1));
                Assert.That(instruction.Role.CrossingChance, Is.EqualTo(1));
            }
        }

        [Test]
        public void DragOffsetsHaveTheCorrectWidthOrientationAndMirrorAtHalfTime()
        {
            var plan = CareerTacticPlan.CreateDefault(CareerFormation.FourFourTwo);
            var slot = plan.GetSlot("left-back");
            plan = plan.WithSlot(slot.SlotId, slot.X + .10f, slot.Depth + .10f, slot.Role);
            var instruction = new CareerMatchOptions("club", plan.Formation, CareerMentality.Balanced, plan)
                .CreateInstructions().Single(value => value.SlotId == slot.SlotId);
            var home = instruction.ApplyPosition(new Vector3(30, 0, 54), Vector3.right, 100, 60, false, Vector3.zero);
            var away = instruction.ApplyPosition(new Vector3(70, 0, 6), Vector3.left, 100, 60, false, Vector3.zero);
            Assert.That(home.x, Is.EqualTo(40).Within(.0001));
            Assert.That(home.z, Is.EqualTo(48).Within(.0001));
            Assert.That(away.x, Is.EqualTo(100 - home.x).Within(.0001));
            Assert.That(away.z, Is.EqualTo(60 - home.z).Within(.0001));
        }

        [Test]
        public void AuthoredProfilesCoverEverySupportedRoleAndStandardStaysNeutral()
        {
            Assert.That(Tuning, Is.Not.Null);
            foreach (CareerPlayerRole role in Enum.GetValues(typeof(CareerPlayerRole)))
                Assert.That(Tuning.Resolve(role), Is.Not.Null, role.ToString());
            Assert.That(Tuning.Resolve(CareerPlayerRole.Standard), Is.SameAs(RoleTacticalSettings.Standard));
        }

        [Test]
        public void InvertedAndCrossingFullbacksChooseDistinctAttackingWidthAndPassPreferences()
        {
            var inverted = Instruction(CareerPlayerRole.InvertedFullBack);
            var crossing = Instruction(CareerPlayerRole.CrossingFullBack);
            var original = new Vector3(40, 0, 54);
            var ball = new Vector3(60, 2, 30);
            var central = inverted.ApplyPosition(original, Vector3.right, 100, 60, true, ball);
            var wide = crossing.ApplyPosition(original, Vector3.right, 100, 60, true, ball);
            Assert.That(central.z, Is.LessThan(original.z));
            Assert.That(wide.z, Is.GreaterThan(original.z));
            Assert.That(central.y, Is.Zero, "Tactical support remains on the ground.");
            Assert.That(inverted.PassPriority(false, 0, 0), Is.GreaterThan(inverted.PassPriority(true, 0, 0)));
            Assert.That(crossing.Role.CrossingChance, Is.GreaterThan(inverted.Role.CrossingChance));
            Assert.That(crossing.Role.EarlyCross, Is.True);
            Assert.That(inverted.Role.EarlyPass, Is.True);
        }

        [Test]
        public void BallWinnerHoldsDeeperAndMarksTighterWhileCentreBackCanAdvance()
        {
            var winner = Instruction(CareerPlayerRole.BallWinningMidfielder);
            var advancing = Instruction(CareerPlayerRole.AdvancingCentreBack);
            var origin = new Vector3(40, 0, 30);
            var ball = new Vector3(60, 0, 30);
            Assert.That(winner.ApplyPosition(origin, Vector3.right, 100, 60, true, ball).x, Is.LessThan(origin.x));
            Assert.That(advancing.ApplyPosition(origin, Vector3.right, 100, 60, true, ball).x, Is.GreaterThan(origin.x));
            Assert.That(winner.Role.MarkingWeight, Is.GreaterThan(1));
            Assert.That(winner.Role.MarkingDistance, Is.LessThan(1));
            Assert.That(winner.Role.JoinAttack, Is.LessThan(advancing.Role.JoinAttack));
        }

        [Test]
        public void PlaymakerPrefersProgressivePassWhileTargetOffersShortLayoffAndReceives()
        {
            var creator = Instruction(CareerPlayerRole.CreativePlaymaker);
            var target = Instruction(CareerPlayerRole.TargetForward);
            Assert.That(creator.PassPriority(false, 20, 0), Is.GreaterThan(creator.PassPriority(false, -10, 0)));
            Assert.That(target.PassPriority(false, -10, 0), Is.GreaterThan(target.PassPriority(false, 20, 0)));
            Assert.That(target.Role.PreferLayoff, Is.True);
            Assert.That(target.Role.ReceivePassPriority, Is.GreaterThan(0));
            Assert.That(Instruction(CareerPlayerRole.MobileForward).Role.JoinAttack, Is.GreaterThan(target.Role.JoinAttack));
        }

        [Test]
        public void MatchSnapshotsDoNotObserveLaterInspectorChanges()
        {
            var tuning = ScriptableObject.CreateInstance<CareerRoleTuning>();
            try
            {
                var profile = new CareerRoleTuning.RoleProfile { Role = CareerPlayerRole.CrossingFullBack, CrossingChance = 2 };
                tuning.Profiles = new[] { profile };
                var snapshot = tuning.Resolve(profile.Role);
                profile.CrossingChance = .1f;
                Assert.That(snapshot.CrossingChance, Is.EqualTo(2));
                Assert.That(tuning.Resolve(profile.Role).CrossingChance, Is.EqualTo(.1f));
            }
            finally { UnityEngine.Object.DestroyImmediate(tuning); }
        }

        [Test]
        public void GoalkeeperExtremeAdjustmentsStayOnThePitchInEitherDirection()
        {
            var plan = CareerTacticPlan.CreateDefault(CareerFormation.FourFourTwo);
            var definition = CareerTacticPlan.GetSlotDefinitions(plan.Formation)[0];
            foreach (var depth in new[] { definition.MinDepth, definition.MaxDepth })
            foreach (var x in new[] { definition.MinX, definition.MaxX })
            foreach (var direction in new[] { Vector3.right, Vector3.left })
            {
                var modified = plan.WithSlot(definition.SlotId, x, depth, CareerPlayerRole.Standard);
                var instruction = new CareerMatchOptions("club", plan.Formation, CareerMentality.Balanced, modified).CreateInstructions()[0];
                var baseline = new Vector3(direction.x > 0 ? 1 : 99, 0, 30);
                var point = instruction.ApplyBoundedPosition(baseline, direction, 100, 60, false, Vector3.zero);
                Assert.That(point.x, Is.InRange(0f, 100f));
                Assert.That(point.z, Is.InRange(0f, 60f));
            }
        }

        [Test]
        public void TuningRejectsInvalidMarkingParametersBeforeCreatingMatchInstructions()
        {
            var profile = new CareerRoleTuning.RoleProfile { MarkingWeight = float.NaN };
            Assert.Throws<InvalidOperationException>(() => profile.Snapshot());
            profile.MarkingWeight = 1;
            profile.MarkingDistance = -1;
            Assert.Throws<InvalidOperationException>(() => profile.Snapshot());
        }

        [Test]
        public void RejectsPlanForDifferentFormationInsteadOfReassigningSlotInstructions()
        {
            Assert.Throws<ArgumentException>(() => new CareerMatchOptions("club", CareerFormation.FourFourTwo,
                CareerMentality.Balanced, CareerTacticPlan.CreateDefault(CareerFormation.FourThreeThree)));
        }

        private static PlayerTacticalInstruction Instruction(CareerPlayerRole role)
            => new PlayerTacticalInstruction("test-slot", Positions.CM, 0, 0, Tuning.Resolve(role));
    }
}
#endif
