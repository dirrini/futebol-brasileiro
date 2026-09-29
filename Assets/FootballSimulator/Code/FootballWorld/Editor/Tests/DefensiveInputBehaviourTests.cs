#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using System.Runtime.Serialization;
using FStudio.MatchEngine;
using FStudio.MatchEngine.Balls;
using FStudio.MatchEngine.Enums;
using FStudio.MatchEngine.Input;
using FStudio.MatchEngine.Players;
using FStudio.MatchEngine.Players.Behaviours;
using FStudio.MatchEngine.Players.PlayerController;
using FStudio.MatchEngine.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace FStudio.FootballWorld.Editor.Tests {
    public sealed class DefensiveInputBehaviourTests {
        private GameObject host;
        private Ball ball;
        private PlayerBase defender, holder;
        private RecordingController defenderController, holderController;
        private GameTeam ourTeam, theirTeam;

        [SetUp] public void SetUp() {
            host = new GameObject("Isolated defensive input"); host.SetActive(false);
            ourTeam = Child("Our team").AddComponent<GameTeam>();
            theirTeam = Child("Opponents").AddComponent<GameTeam>();
            ball = Child("Ball").AddComponent<Ball>();
            defender = Actor(ourTeam, new Vector3(20, 0, 20), out defenderController);
            holder = Actor(theirTeam, new Vector3(30, 0, 20), out holderController);
            SetHolder(holder);
        }
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test]
        public void HoldingShortPassPursuesTheCarrierSprintChangesMovementAndReleaseStopsRequests() {
            var pursuit = new RecordingPursuit { IsTriggered = true };
            Prepare(pursuit, 1);
            Assert.That(pursuit.Behave(false), Is.True);
            Assert.That(defenderController.MoveRequests, Is.EqualTo(1));
            Assert.That(defenderController.LastTarget, Is.EqualTo(holderController.Position));
            Assert.That(defenderController.LastMovement, Is.EqualTo(MovementType.Normal));
            Assert.That(pursuit.Challenges, Is.Zero, "A distant carrier cannot be tackled just by pressing pass.");
            pursuit.SprintHeld = true;
            Prepare(pursuit, 1.02f);
            Assert.That(pursuit.Behave(true), Is.True);
            Assert.That(defenderController.LastMovement, Is.EqualTo(MovementType.BestHeCanDo));
            pursuit.IsTriggered = false;
            var beforeRelease = defenderController.MoveRequests;
            Assert.That(pursuit.Behave(true), Is.False);
            Assert.That(defenderController.MoveRequests, Is.EqualTo(beforeRelease));
        }

        [Test]
        public void PursuitAttemptsStandingChallengeOnlyInReachAndRespectsCooldownWhileHeld() {
            var pursuit = new RecordingPursuit { IsTriggered = true };
            var tuning = MatchControlSettings.Current;
            holderController.CurrentPosition = defenderController.Position + Vector3.right * (tuning.StandingTackleDistance - .1f);
            Prepare(pursuit, 2); pursuit.Behave(false);
            Assert.That(pursuit.Challenges, Is.EqualTo(1));
            Assert.That(pursuit.LastChallenged, Is.SameAs(holder));
            Prepare(pursuit, 2 + tuning.StandingTackleCooldown / 2); pursuit.Behave(true);
            Assert.That(pursuit.Challenges, Is.EqualTo(1));
            Prepare(pursuit, 2 + tuning.StandingTackleCooldown + .01f); pursuit.Behave(true);
            Assert.That(pursuit.Challenges, Is.EqualTo(2));
            holderController.CurrentPosition += Vector3.right * 10;
            Prepare(pursuit, 10); pursuit.Behave(true);
            Assert.That(pursuit.Challenges, Is.EqualTo(2));
        }

        [Test]
        public void PossessionRecoveryAndProtectedOrFriendlyCarrierDoNotTriggerChallengesOrMovement() {
            var pursuit = new RecordingPursuit { IsTriggered = true };
            SetHolder(defender);
            Prepare(pursuit, 1);
            Assert.That(pursuit.Behave(false), Is.False);
            Assert.That(defenderController.MoveRequests, Is.Zero);
            SetHolder(holder); Property(holder, "IsGKUntouchable", true);
            pursuit.IsTriggered = true; Prepare(pursuit, 2);
            Assert.That(pursuit.Behave(false), Is.False);
            Assert.That(pursuit.Challenges, Is.Zero);
            Property(holder, "IsGKUntouchable", false);
            Property(defender, "IsHoldingBall", true);
            pursuit.IsTriggered = true; Prepare(pursuit, 3);
            Assert.That(pursuit.Behave(false), Is.False);
            Assert.That(pursuit.IsTriggered, Is.False, "Holding X while recovering the ball cannot leave a stale defensive command.");
        }

        [Test]
        public void CircleRequestsOneImmediateSlideEvenBeforeReachingTheCarrierAndAlsoOnLooseBall() {
            var slide = new RecordingSlide { IsTriggered = true };
            Prepare(slide, 1);
            Assert.That(slide.Behave(false), Is.True);
            Assert.That(slide.Slides, Is.EqualTo(1), "The slide command must not become a pursuit that waits for close range.");
            Assert.That(defenderController.MoveRequests, Is.Zero);
            Assert.That(slide.Behave(true), Is.False);
            Assert.That(slide.Slides, Is.EqualTo(1));
            SetHolder(null); slide.IsTriggered = true;
            Assert.That(slide.Behave(false), Is.True);
            Assert.That(slide.Slides, Is.EqualTo(2));
        }

        [Test]
        public void SlideCannotFireDuringStoppageWhileStunnedOrAgainstAnOwnedBall() {
            var slide = new RecordingSlide { IsTriggered = true };
            Prepare(slide, 1, MatchStatus.WaitingForKickOff);
            Assert.That(slide.Behave(false), Is.False);
            slide.IsTriggered = true; defenderController.IsPhysicsEnabled = false;
            Prepare(slide, 2);
            Assert.That(slide.Behave(false), Is.False);
            defenderController.IsPhysicsEnabled = true; SetHolder(defender); slide.IsTriggered = true;
            Assert.That(slide.Behave(false), Is.False);
            Assert.That(slide.Slides, Is.Zero);
        }

        private void Prepare(BaseBehaviour behaviour, float time, MatchStatus status = MatchStatus.Playing)
            => behaviour.SetBehaviour(defender, true, time, .02f, 100, 60, status, TeamBehaviour.Defending,
                80, 20, ball, null, null, new[] {defender}, new[] {holder});
        private void SetHolder(PlayerBase player) => typeof(Ball).GetProperty("HolderPlayer").SetValue(ball, player);
        private GameObject Child(string name) { var child = new GameObject(name); child.transform.SetParent(host.transform); return child; }
        private static void Property(PlayerBase player, string name, object value)
            => typeof(PlayerBase).GetProperty(name).SetValue(player, value);
        private static PlayerBase Actor(GameTeam team, Vector3 position, out RecordingController controller) {
            // Skip only prefab/renderer construction. Behave, pursuit prediction,
            // ownership guards and movement requests are the real engine code.
            var player = (PlayerBase)FormatterServices.GetUninitializedObject(typeof(Defender));
            controller = new RecordingController { CurrentPosition = position, BasePlayer = player };
            typeof(PlayerBase).GetField("PlayerController").SetValue(player, controller);
            typeof(PlayerBase).GetField("GameTeam").SetValue(player, team);
            Property(player, "isInputControlled", true);
            return player;
        }
        private sealed class RecordingPursuit : InputTackleBehaviour {
            public int Challenges; public PlayerBase LastChallenged;
            protected override void AttemptStandingChallenge(PlayerBase target) { Challenges++; LastChallenged = target; }
        }
        private sealed class RecordingSlide : InputSlideTackleBehaviour {
            public int Slides;
            protected override void PerformSlideTackle() => Slides++;
        }
        private sealed class RecordingController : IPlayerController {
            public Vector3 CurrentPosition, LastTarget;
            public int MoveRequests;
            public MovementType LastMovement;
            public GameObject UnityObject => null;
            public CapsuleCollider UnityCollider => null;
            public PlayerBase BasePlayer { get; set; }
            public Vector3 Position => CurrentPosition;
            public Action<Collision> CollisionEnterEvent { get; set; }
            public Vector3 Forward => Vector3.right;
            public bool IsPhysicsEnabled { get; set; } = true;
            public Quaternion Rotation => Quaternion.identity;
            public Vector3 Direction => Vector3.right;
            public bool IsDebuggerEnabled => false;
            public float MoveSpeed => 0;
            public float TargetMoveSpeed => 0;
            public PlayerAnimator Animator => null;
            public PlayerUI UI => null;
            public void SetOffside(bool value) { }
            public void SetUI(bool value) { }
            public bool IsAnimationABlocker(in string[] clips) => false;
            public void SetPlayer(int number, PlayerBase player, Material kit) { }
            public void SetAsLineReferee() { }
            public bool MoveTo(in float dt, Vector3 to, bool faceTowards = true, MovementType movementType = MovementType.BestHeCanDo) {
                MoveRequests++; LastTarget = to; LastMovement = movementType; return false;
            }
            public void Stop(in float dt) { }
            public void SetInstantPosition(Vector3 position) => CurrentPosition = position;
            public void SetInstantRotation(Quaternion rotation) { }
            public bool LookTo(in float dt, Vector3 to) => true;
            public void Up(in float dt, MatchStatus status, Ball value) { }
            public void SetHeadLook(in float dt, Vector3 target, float weight) { }
            public bool HitBall(in Vector3 velocity, PlayerAnimatorVariable variable, out PlayerAnimatorVariable result, in float holdTime, bool disableVolley = false) { result = variable; return false; }
            public void ProcessMovement(in float time, in float dt) { }
            public void BallHitEvent() { }
        }
    }
}
#endif
