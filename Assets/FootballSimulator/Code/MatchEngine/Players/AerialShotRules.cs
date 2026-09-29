using System;
using UnityEngine;
using FStudio.MatchEngine.Players.PlayerController;

namespace FStudio.MatchEngine.Players {
    public enum AerialShotKind { Header, DivingHeader, Volley, Bicycle }

    [Serializable]
    public sealed class AerialShotBinding {
        public AerialShotKind Kind;
        public bool Enabled = true;
        [Tooltip("Trigger of an authored R/L state in PlayerLocomotion. Edit the state motion in that controller to replace its clip.")]
        public PlayerAnimatorVariable Animation = PlayerAnimatorVariable.Header_R;
        [Min(0)] public float MinimumHeight = 1.65f;
        [Min(0)] public float MaximumHeight = 2.5f;
        [Range(-1, 1)] public float MinimumFacingDot = -0.2f;
        [Range(-1, 1)] public float MaximumFacingDot = 1;
        [Min(0.1f)] public float ContactRadius = 1.15f;
        [Min(0)] public float MinimumContactDistance;
        public float MinimumForwardOffset = -0.25f;
        public float MaximumForwardOffset = 1.4f;
        [Min(0)] public float MaximumLateralOffset = 0.55f;
        [Tooltip("Seconds from the animation trigger to BallHitEvent, including the authored state speed and transition.")]
        [Min(0)] public float ContactDelay = 0.16f;
        [Min(0.05f)] public float RecoverySeconds = 0.5f;
        [Range(0.1f, 1.5f)] public float VelocityMultiplier = 1;

        public bool Accepts(Vector3 playerPosition, Vector3 ballPosition, float facingDot, Vector3 playerForward = default) {
            if (!Enabled || !AerialShotRules.IsFinite(ballPosition) || float.IsNaN(facingDot)) return false;
            var relative = ballPosition - playerPosition;
            var height = relative.y;
            relative.y = 0;
            playerForward.y = 0;
            var forwardOffset = Vector3.Dot(relative, playerForward.normalized);
            var lateralOffset = Mathf.Abs(Vector3.Dot(relative, Vector3.Cross(Vector3.up, playerForward.normalized)));
            return height >= MinimumHeight && height <= MaximumHeight &&
                facingDot >= MinimumFacingDot && facingDot <= MaximumFacingDot &&
                relative.sqrMagnitude <= ContactRadius * ContactRadius &&
                relative.sqrMagnitude >= MinimumContactDistance * MinimumContactDistance &&
                (playerForward.sqrMagnitude < 0.01f || (forwardOffset >= MinimumForwardOffset && forwardOffset <= MaximumForwardOffset &&
                    lateralOffset <= MaximumLateralOffset));
        }
    }

    /// <summary>Geometry-only policy. Possession, input and the contact event remain runtime responsibilities.</summary>
    public static class AerialShotRules {
        public static bool IsFinite(Vector3 value) =>
            !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
              float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));

        public static float FacingDot(Vector3 forward, Vector3 goalDirection) {
            forward.y = goalDirection.y = 0;
            return Vector3.Dot(forward.normalized, goalDirection.normalized);
        }

        public static AerialShotBinding Select(AerialShotBinding[] bindings, Vector3 playerPosition,
            Vector3 forward, Vector3 goalDirection, Vector3 ballPosition, Vector3 ballVelocity, Vector3 gravity) {
            if (bindings == null || !IsFinite(ballVelocity)) return null;
            var facingDot = FacingDot(forward, goalDirection);
            foreach (var binding in bindings) {
                if (binding == null || !binding.Enabled) continue;
                var delay = Mathf.Max(0, binding.ContactDelay);
                var predicted = ballPosition + ballVelocity * delay + gravity * (0.5f * delay * delay);
                if (binding.Accepts(playerPosition, predicted, facingDot, forward)) return binding;
            }
            return null;
        }

        public static bool CanContact(AerialShotBinding binding, Vector3 playerPosition, Vector3 ballPosition,
            float facingDot, bool isSameBall, bool isStillControlled, bool isFree, bool isPlaying,
            bool isPaused, bool hasFocus, float now, float deadline, Vector3 playerForward = default) =>
            binding != null && isSameBall && isStillControlled && isFree && isPlaying && !isPaused && hasFocus &&
            now <= deadline && binding.Accepts(playerPosition, ballPosition, facingDot, playerForward);

        public static AerialShotBinding[] Defaults() => new[] {
            new AerialShotBinding { Kind = AerialShotKind.DivingHeader, Animation = PlayerAnimatorVariable.DivingHeader_R,
                MinimumHeight = 0.6f, MaximumHeight = 1.25f, MinimumFacingDot = 0.25f,
                MinimumContactDistance = 0.6f, ContactRadius = 1.35f,
                MinimumForwardOffset = 0.5f, MaximumForwardOffset = 1.3f, MaximumLateralOffset = 0.5f,
                ContactDelay = 0.4066667f, RecoverySeconds = 2.4266667f, VelocityMultiplier = 0.75f },
            new AerialShotBinding { Kind = AerialShotKind.Bicycle, Animation = PlayerAnimatorVariable.AerialBicycle_R,
                MinimumHeight = 1.2f, MaximumHeight = 1.9f, MinimumFacingDot = -1, MaximumFacingDot = -0.35f,
                MinimumForwardOffset = -0.9f, MaximumForwardOffset = 0.55f, MaximumLateralOffset = 0.7f,
                ContactDelay = 0.34f, ContactRadius = 1.05f, RecoverySeconds = 1.5f, VelocityMultiplier = 0.9f },
            new AerialShotBinding { Kind = AerialShotKind.Header, Animation = PlayerAnimatorVariable.AerialHeader_R,
                MinimumHeight = 1.5f, MaximumHeight = 2.05f, ContactDelay = 0.185f, RecoverySeconds = 0.6f,
                MinimumForwardOffset = -0.2f, MaximumForwardOffset = 0.8f, ContactRadius = 0.9f,
                VelocityMultiplier = 0.75f },
            new AerialShotBinding { Kind = AerialShotKind.Header, Animation = PlayerAnimatorVariable.AerialLowHeader_R,
                MinimumHeight = 1.15f, MaximumHeight = 1.65f, ContactDelay = 0.18f, RecoverySeconds = 0.35f,
                MinimumForwardOffset = -0.15f, MaximumForwardOffset = 0.85f, ContactRadius = 0.95f,
                VelocityMultiplier = 0.75f },
            new AerialShotBinding { Kind = AerialShotKind.Volley, Animation = PlayerAnimatorVariable.AerialVolley_R,
                MinimumHeight = 0.65f, MaximumHeight = 1.3f, MinimumForwardOffset = 0.35f,
                ContactDelay = 0.27f, RecoverySeconds = 0.6f }
        };
    }
}
