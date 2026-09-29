using UnityEngine;
using FStudio.MatchEngine.Balls;
using FStudio.MatchEngine.EngineOptions;
using FStudio.MatchEngine.Enums;
using FStudio.Utilities;

namespace FStudio.MatchEngine.Players {
    public abstract partial class PlayerBase {
        private AerialShotBinding pendingAerialBinding;
        private Ball pendingAerialBall;
        private Vector3 pendingAerialVelocity;
        private float pendingAerialDeadline;

        public bool HasPendingAerialShot => pendingAerialBinding != null && Time.time <= pendingAerialDeadline;

        public bool TryShootAerial(Vector3 velocity, AerialShotBinding binding) {
            var ball = Ball.Current;
            if (binding == null || !binding.Enabled || HasPendingAerialShot || !isInputControlled || IsGK ||
                CaughtInOffside || MatchPause.IsPaused || !Application.isFocused || MatchManager.Current == null ||
                MatchManager.Current.MatchFlags != MatchStatus.Playing || ball == null || ball.HolderPlayer != null ||
                !PlayerController.IsPhysicsEnabled || !AerialShotRules.IsFinite(velocity)) return false;
            if (!PlayerController.Animator.PlayAerialShotAnimation(in velocity, binding.Animation)) return false;
            pendingAerialBinding = binding;
            pendingAerialBall = ball;
            pendingAerialVelocity = velocity * binding.VelocityMultiplier;
            pendingAerialDeadline = Time.time + binding.ContactDelay +
                EngineOptions_BallHitAnimations.Current.AerialContactGraceSeconds;
            // The animation event is the only operation allowed to strike the free ball.
            ballHitAnimationEvent = BallHitAnimationEvent.None;
            CurrentAct = Acts.InputShoot;
            var delta = Time.deltaTime;
            Stop(in delta);
            RecoverFromAerialShot(Mathf.Max(binding.RecoverySeconds,
                binding.ContactDelay + EngineOptions_BallHitAnimations.Current.AerialContactGraceSeconds));
            return true;
        }

        public void CancelPendingAerialShot() {
            pendingAerialBinding = null;
            pendingAerialBall = null;
            pendingAerialDeadline = 0;
        }

        private async void RecoverFromAerialShot(float duration) {
            var recoveryEnd = Time.time + duration;
            var controllerObject = PlayerController.UnityObject;
            PlayerController.IsPhysicsEnabled = false;
            // Scaled match time freezes during pause; UnityAsync keeps the wait on the supported WebGL path.
            while (Time.time < recoveryEnd) {
                await UnityAsync.Delay(16);
                if (controllerObject == null) return;
            }
            if (controllerObject != null) PlayerController.IsPhysicsEnabled = true;
        }

        private void CompleteAerialShotContact() {
            var binding = pendingAerialBinding;
            var ball = Ball.Current;
            var velocity = pendingAerialVelocity;
            var deadline = pendingAerialDeadline;
            var sameBall = ball != null && ball == pendingAerialBall;
            CancelPendingAerialShot(); // Consume before callbacks; duplicated animation events cannot hit twice.
            if (ball == null || MatchManager.Current == null || CaughtInOffside) return;
            if (!AerialShotRules.CanContact(binding, Position, ball.transform.position,
                AerialShotRules.FacingDot(PlayerController.Forward, TargetGoalNet - Position),
                sameBall, isInputControlled, ball.HolderPlayer == null,
                MatchManager.Current.MatchFlags == MatchStatus.Playing, MatchPause.IsPaused, Application.isFocused,
                Time.time, deadline, PlayerController.Forward)) return;
            ball.Shoot(velocity, this);
        }
    }
}
