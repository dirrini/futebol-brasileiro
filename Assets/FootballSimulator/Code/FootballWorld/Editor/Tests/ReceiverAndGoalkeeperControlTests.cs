#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using FStudio.MatchEngine.Input;
using FStudio.MatchEngine.Players.Behaviours;
using FStudio.MatchEngine.Players.InputBehaviours;
using NUnit.Framework;
using UnityEngine;

namespace FStudio.FootballWorld.Editor.Tests
{
    public sealed class ReceiverAndGoalkeeperControlTests
    {
        private sealed class Actor { public bool OurTeam = true; public bool Enabled = true; }
        private static bool Eligible(Actor value) => value.OurTeam && value.Enabled;

        [Test]
        public void PassSelectionWaitsForActualReleaseAndUsesTheIntendedReceiverOnlyOnce()
        {
            var state = new AutomaticReceiverControl<Actor>();
            var passer = new Actor(); var receiver = new Actor();
            state.Queue(passer, receiver, 10);
            Assert.That(state.Resolve(passer, null, 10, Eligible, null), Is.SameAs(passer));
            Assert.That(state.AssistedReceiver, Is.Null);
            Assert.That(state.Resolve(null, passer, 10.1f, Eligible, null), Is.SameAs(receiver));
            Assert.That(state.AssistedReceiver, Is.SameAs(receiver));
            Assert.That(state.Resolve(null, passer, 10.2f, Eligible, null), Is.Null);
        }

        [Test]
        public void ActualPossessionOverridesThePlannedRecipientIncludingGoalkeeper()
        {
            var state = new AutomaticReceiverControl<Actor>();
            var passer = new Actor(); var receiver = new Actor(); var goalkeeper = new Actor();
            state.Queue(passer, receiver, 0);
            Assert.That(state.Resolve(goalkeeper, passer, .1f, Eligible, null), Is.SameAs(goalkeeper));
            Assert.That(state.AssistedReceiver, Is.Null);
            Assert.That(state.Resolve(null, passer, .2f, Eligible, null), Is.Null);
        }

        [Test]
        public void InterceptionAndManualCancellationClearQueuedSwitchAndReceiverAssist()
        {
            var state = new AutomaticReceiverControl<Actor>();
            var passer = new Actor(); var receiver = new Actor(); var opponent = new Actor { OurTeam = false };
            state.Queue(passer, receiver, 0);
            Assert.That(state.Resolve(opponent, passer, .1f, Eligible, null), Is.Null);
            Assert.That(state.Resolve(null, passer, .2f, Eligible, null), Is.Null);
            state.Queue(passer, receiver, 1);
            state.Resolve(null, passer, 1.1f, Eligible, null);
            state.ReleaseAssist();
            Assert.That(state.AssistedReceiver, Is.Null);
            state.Queue(passer, receiver, 2);
            state.Cancel();
            Assert.That(state.Resolve(null, passer, 2.1f, Eligible, null), Is.Null);
        }

        [Test]
        public void MissingOrDisabledTargetUsesEligibleLandingReceiverButNeverTheOpponent()
        {
            var state = new AutomaticReceiverControl<Actor>();
            var passer = new Actor(); var disabled = new Actor { Enabled = false }; var landingReceiver = new Actor();
            state.Queue(passer, disabled, 0);
            Assert.That(state.Resolve(null, passer, .1f, Eligible, () => landingReceiver), Is.SameAs(landingReceiver));
            state.Queue(passer, null, 1);
            Assert.That(state.Resolve(null, passer, 1.1f, Eligible, () => new Actor { OurTeam = false }), Is.Null);
        }

        [Test]
        public void StaleOrUnrelatedReleaseCannotTriggerDelayedControlChange()
        {
            var state = new AutomaticReceiverControl<Actor>();
            var passer = new Actor(); var receiver = new Actor();
            state.Queue(passer, receiver, 0);
            Assert.That(state.Resolve(null, passer, 2, Eligible, null), Is.Null);
            state.Queue(passer, receiver, 3);
            Assert.That(state.Resolve(null, new Actor(), 3.1f, Eligible, null), Is.Null);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void GoalkeeperClearanceChargeIncreasesRangeAndRespectsAttackingDirection(int direction)
        {
            var origin = new Vector3(direction > 0 ? 5 : 95, 0, 30);
            var tap = InputGoalkeeperClearanceBehaviour.TargetPoint(origin, Vector3.zero, Vector3.right * direction, new Vector2(100, 60), 0, 18, 70);
            var full = InputGoalkeeperClearanceBehaviour.TargetPoint(origin, Vector3.zero, Vector3.right * direction, new Vector2(100, 60), 1, 18, 70);
            Assert.That((tap.x - origin.x) * direction, Is.EqualTo(18).Within(.0001));
            Assert.That((full.x - origin.x) * direction, Is.EqualTo(70).Within(.0001));
            Assert.That(full.z, Is.EqualTo(30));
        }

        [Test]
        public void CompletingKeeperPossessionClearsTheDiveWithoutAnotherAiTick()
        {
            var shield = new GKShieldBehaviour { ForceBehaviour = true };
            typeof(GKShieldBehaviour).GetProperty(nameof(GKShieldBehaviour.IsOnJump)).GetSetMethod(true).Invoke(shield, new object[] { true });
            var deadline = typeof(GKShieldBehaviour).GetField("expireJump", BindingFlags.Instance | BindingFlags.NonPublic);
            var target = typeof(GKShieldBehaviour).GetField("jumpTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            deadline.SetValue(shield, 100f);
            target.SetValue(shield, new Vector3(3, 0, 28));

            // Manual possession blocks AI Behave; completion must clear the
            // previous save immediately so a quick pass cannot resume its dive.
            shield.ResetJump();

            Assert.That(shield.IsOnJump, Is.False);
            Assert.That(shield.ForceBehaviour, Is.False);
            Assert.That((float)deadline.GetValue(shield), Is.Zero);
            Assert.That((Vector3)target.GetValue(shield), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void GoalkeeperClearanceAimingStaysInsidePitchAndOnGround()
        {
            var point = InputGoalkeeperClearanceBehaviour.TargetPoint(new Vector3(98, 2, 59), new Vector3(1, 10, 1), Vector3.left, new Vector2(100, 60), 1, 18, 70);
            Assert.That(point.x, Is.EqualTo(99));
            Assert.That(point.z, Is.EqualTo(59));
            Assert.That(point.y, Is.Zero);
        }
    }
}
#endif
