#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using FStudio.Data;
using FStudio.Database;
using FStudio.MatchEngine;
using NUnit.Framework;
using UnityEngine;

namespace FStudio.FootballWorld.Editor.Tests
{
    public sealed class MatchPlayerAttributeTests
    {
        private PlayerEntry entry;
        [SetUp] public void SetUp() => entry = ScriptableObject.CreateInstance<PlayerEntry>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(entry);
        private MatchPlayer Create() => new MatchPlayer(1, entry, Positions.ST);

        [Test]
        public void MoreSpeedAndAccelerationIncreaseMovementWithoutChangingFinishing()
        {
            entry.topSpeed = entry.acceleration = 10; entry.shooting = 50;
            var slow = Create();
            entry.topSpeed = entry.acceleration = 90;
            var fast = Create();
            Assert.That(fast.GetTopSpeed(), Is.GreaterThan(slow.GetTopSpeed()));
            Assert.That(fast.GetAcceleration(), Is.GreaterThan(slow.GetAcceleration()));
            Assert.That(fast.ActualShooting, Is.EqualTo(slow.ActualShooting));
        }

        [Test]
        public void PowerAndFinishingAreIndependent()
        {
            entry.shooting = 40; entry.shootPower = 10;
            var weak = Create(); entry.shootPower = 90;
            var strong = Create();
            Assert.That(strong.GetShootPower(), Is.GreaterThan(weak.GetShootPower()));
            Assert.That(strong.GetShooting(), Is.EqualTo(weak.GetShooting()));
            entry.shooting = 90;
            var accurate = Create();
            Assert.That(accurate.GetShooting(), Is.GreaterThan(strong.GetShooting()));
            Assert.That(accurate.GetShootPower(), Is.EqualTo(strong.GetShootPower()));
        }

        [Test]
        public void ReactionShortensDecisionDelayIndependentlyOfPositioning()
        {
            entry.positioning = 50; entry.reaction = 10;
            var slow = Create(); entry.reaction = 90;
            var fast = Create();
            Assert.That(slow.GetReactionDelay(), Is.GreaterThan(0));
            Assert.That(fast.GetReactionDelay(), Is.LessThan(slow.GetReactionDelay()));
            Assert.That(fast.ActualPositioning, Is.EqualTo(slow.ActualPositioning));
            entry.positioning = 90;
            Assert.That(Create().GetReactionDelay(), Is.EqualTo(fast.GetReactionDelay()));
        }

        [Test]
        public void ShortPassUsesPassingAndLongPassUsesLongBallWithContinuousBlend()
        {
            entry.passing = 90; entry.longBall = 10;
            var player = Create();
            var min = EngineSettings.Current.LongBallSkillActivationDistanceMin;
            var max = EngineSettings.Current.LongBallSkillActivationDistanceMax;
            Assert.That(player.GetPassingAccuracy(min), Is.EqualTo(player.ActualPassing));
            Assert.That(player.GetPassingAccuracy(max), Is.EqualTo(player.ActualLongBall));
            Assert.That(player.GetPassingAccuracy((min + max) / 2),
                Is.EqualTo((player.ActualPassing + player.ActualLongBall) / 2f).Within(.001f));
            entry.longBall = 90;
            Assert.That(Create().GetPassingAccuracy(min), Is.EqualTo(player.GetPassingAccuracy(min)));
            Assert.That(Create().GetPassingAccuracy(max), Is.GreaterThan(player.GetPassingAccuracy(max)));
        }
    }
}
#endif
