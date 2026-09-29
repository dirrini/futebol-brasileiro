#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Linq;
using FStudio.MatchEngine.Players;
using FStudio.MatchEngine.Players.PlayerController;
using NUnit.Framework;
using UnityEngine;

namespace FStudio.FootballWorld.Editor.Tests {
    public sealed class AerialShotRulesTests {
        private static AerialShotBinding Select(float height, Vector3 forward, Vector3 velocity = default,
            float distance = 0.4f) => AerialShotRules.Select(AerialShotRules.Defaults(), Vector3.zero, forward,
                Vector3.forward, new Vector3(0, height, distance), velocity, Vector3.zero);

        [Test]
        public void FacingGoalSelectsTheAuthoredHeaderOrVolleyHeightBand() {
            Assert.That(Select(2, Vector3.forward).Animation, Is.EqualTo(PlayerAnimatorVariable.AerialHeader_R));
            Assert.That(Select(1.4f, Vector3.forward).Animation, Is.EqualTo(PlayerAnimatorVariable.AerialLowHeader_R));
            Assert.That(Select(0.75f, Vector3.forward).Kind, Is.EqualTo(AerialShotKind.Volley));
            Assert.That(Select(0.1f, Vector3.forward), Is.Null, "A rolling ball uses normal possession/shot input.");
        }

        [Test]
        public void BicycleRequiresAnElevatedBallAndBackTowardsGoal() {
            Assert.That(Select(1.6f, Vector3.back).Kind, Is.EqualTo(AerialShotKind.Bicycle));
            Assert.That(Select(1.6f, Vector3.forward).Kind, Is.EqualTo(AerialShotKind.Header));
            Assert.That(Select(0.7f, Vector3.back), Is.Null);
        }

        [Test]
        public void SelectionPredictsWhereTheBallWillBeAtContactInsteadOfHittingRemotely() {
            var incoming = Select(2, Vector3.forward, Vector3.back * 12, 3);
            Assert.That(incoming, Is.Not.Null);
            Assert.That(incoming.Kind, Is.EqualTo(AerialShotKind.Header));
            Assert.That(Select(2, Vector3.forward, Vector3.forward * 10, 3), Is.Null);
            Assert.That(Select(2, Vector3.forward, Vector3.zero, 8), Is.Null);
        }

        [Test]
        public void DivingHeaderUsesItsOwnAuthoredMotionAndRequiresForwardReach() {
            var dive = AerialShotRules.Defaults().Single(x => x.Kind == AerialShotKind.DivingHeader);
            Assert.That(dive.Enabled, Is.True);
            Assert.That(dive.Animation, Is.EqualTo(PlayerAnimatorVariable.DivingHeader_R));
            Assert.That(dive.Accepts(Vector3.zero, new Vector3(0, 0.9f, 0.5f), 1), Is.False);
            Assert.That(dive.Accepts(Vector3.zero, new Vector3(0, 0.9f, 0.9f), 1, Vector3.forward), Is.True);
            Assert.That(Select(0.9f, Vector3.forward, distance: 0.9f).Kind, Is.EqualTo(AerialShotKind.DivingHeader));
        }

        [TestCase(false, true, true, true, false, true, 1f)]
        [TestCase(true, false, true, true, false, true, 1f)]
        [TestCase(true, true, false, true, false, true, 1f)]
        [TestCase(true, true, true, false, false, true, 1f)]
        [TestCase(true, true, true, true, true, true, 1f)]
        [TestCase(true, true, true, true, false, false, 1f)]
        [TestCase(true, true, true, true, false, true, 3f)]
        public void ContactCancelsForLostContextOrExpiredWindow(bool sameBall, bool controlled,
            bool free, bool playing, bool paused, bool focus, float now) {
            var binding = Select(2, Vector3.forward);
            Assert.That(AerialShotRules.CanContact(binding, Vector3.zero, new Vector3(0, 2, 0.4f), 1,
                sameBall, controlled, free, playing, paused, focus, now, 2), Is.False);
        }

        [Test]
        public void ActualContactRechecksHeightAndDistanceAfterTheAnimationStarts() {
            var binding = Select(2, Vector3.forward);
            bool Contact(Vector3 ball) => AerialShotRules.CanContact(binding, Vector3.zero, ball, 1,
                true, true, true, true, false, true, 1, 2);
            Assert.That(Contact(new Vector3(0, 2, 0.4f)), Is.True);
            Assert.That(Contact(new Vector3(0, 2, 3)), Is.False);
            Assert.That(Contact(new Vector3(0, 0.1f, 0.4f)), Is.False);
            Assert.That(Contact(new Vector3(float.NaN, 2, 0.4f)), Is.False);
        }

        [Test]
        public void DivingReachCanRequireAForwardExtensionBeyondStandingHeaderRange() {
            var dive = new AerialShotBinding { Kind = AerialShotKind.DivingHeader, MinimumHeight = 0.6f,
                MaximumHeight = 1.3f, MinimumContactDistance = 0.75f, ContactRadius = 1.4f };
            Assert.That(dive.Accepts(Vector3.zero, new Vector3(0, 1, 0.3f), 1), Is.False);
            Assert.That(dive.Accepts(Vector3.zero, new Vector3(0, 1, 1), 1), Is.True);
        }

        [Test]
        public void FacingGoalCannotHeadABallBehindThePlayersBody() {
            var header = Select(2, Vector3.forward);
            Assert.That(header.Accepts(Vector3.zero, new Vector3(0, 2, -0.8f), 1, Vector3.forward), Is.False);
            Assert.That(header.Accepts(Vector3.zero, new Vector3(0, 2, 0.4f), 1, Vector3.forward), Is.True);
            Assert.That(header.Accepts(Vector3.zero, new Vector3(0.8f, 2, 0.2f), 1, Vector3.forward), Is.False,
                "A wide ball inside a radius still lies outside the authored contact plane.");
        }
    }
}
#endif
