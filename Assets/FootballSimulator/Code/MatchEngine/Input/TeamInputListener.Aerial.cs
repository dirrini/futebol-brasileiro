using UnityEngine;
using FStudio.MatchEngine.Balls;
using FStudio.MatchEngine.EngineOptions;
using FStudio.MatchEngine.Enums;
using FStudio.MatchEngine.Players;
using FStudio.MatchEngine.Players.Behaviours;
using FStudio.MatchEngine.Players.InputBehaviours;

namespace FStudio.MatchEngine.Input {
    public partial class TeamInputListener {
        private PlayerBase aerialBufferedPlayer;
        private Ball aerialBufferedBall;
        private float aerialBufferDeadline;
        private float aerialBufferedCharge;

        private bool CanChargeAerialShot(PlayerBase player) {
            var ball = Ball.Current;
            if (player == null || player != ActivePlayer || player.IsGK || !player.isInputControlled ||
                player.PlayerController == null || !player.PlayerController.IsPhysicsEnabled ||
                MatchPause.IsPaused || !Application.isFocused || MatchManager.Current == null ||
                MatchManager.Current.MatchFlags != MatchStatus.Playing || ball == null || ball.HolderPlayer != null ||
                player.IsThrowHolder || player.IsCornerHolder || player.CaughtInOffside ||
                player.ActiveBehaviour is IInputBehaviour || player.HasPendingAerialShot) return false;
            var settings = EngineOptions_BallHitAnimations.Current;
            var relative = ball.transform.position - player.Position;
            var height = relative.y;
            relative.y = 0;
            return height > 0.2f && height < 5 &&
                relative.sqrMagnitude <= settings.AerialInputReach * settings.AerialInputReach;
        }

        // True consumes the release as an aerial intention; it must never fall through to a ground shot.
        private bool TryActivateAerialShot(float charge) {
            if (!CanChargeAerialShot(ActivePlayer)) return false;
            aerialBufferedPlayer = ActivePlayer;
            aerialBufferedBall = Ball.Current;
            aerialBufferedCharge = Mathf.Clamp01(charge);
            aerialBufferDeadline = Time.time + EngineOptions_BallHitAnimations.Current.AerialInputBufferSeconds;
            UpdateAerialShotBuffer();
            return true;
        }

        private void UpdateAerialShotBuffer() {
            if (aerialBufferedPlayer == null) return;
            if (Time.time > aerialBufferDeadline || aerialBufferedBall != Ball.Current ||
                !CanChargeAerialShot(aerialBufferedPlayer)) {
                CancelAerialShotBuffer();
                return;
            }
            var player = aerialBufferedPlayer;
            var match = MatchManager.Current;
            var goal = Vector3.SqrMagnitude(match.goalNet1.Position - player.TargetGoalNet) <
                Vector3.SqrMagnitude(match.goalNet2.Position - player.TargetGoalNet) ? match.goalNet1 : match.goalNet2;
            var settings = EngineOptions_BallHitAnimations.Current;
            var binding = AerialShotRules.Select(settings.AerialShots, player.Position,
                player.PlayerController.Forward, goal.Position - player.Position,
                aerialBufferedBall.transform.position, aerialBufferedBall.Velocity, Physics.gravity);
            if (binding == null) return;
            var opponents = player.GameTeam == match.GameTeam1 ? match.GameTeam2 : match.GameTeam1;
            var point = goal.GetShootingVector(player, opponents.GamePlayers).shootPoint;
            if (point == null) return;
            var velocity = goal.GetShootingVectorFromPoint(player, point) * Mathf.Lerp(0.35f, 1, aerialBufferedCharge);
            if (player.TryShootAerial(velocity, binding)) CancelAerialShotBuffer();
        }

        private void CancelAerialShotBuffer() {
            aerialBufferedPlayer = null;
            aerialBufferedBall = null;
            aerialBufferDeadline = 0;
        }

        private void CancelAerialShotInput() {
            CancelAerialShotBuffer();
            ActivePlayer?.CancelPendingAerialShot();
        }
    }
}
