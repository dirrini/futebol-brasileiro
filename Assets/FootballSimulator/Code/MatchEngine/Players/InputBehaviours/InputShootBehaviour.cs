using FStudio.MatchEngine.Enums;
using UnityEngine;
using FStudio.MatchEngine.EngineOptions;
using FStudio.MatchEngine.Players.InputBehaviours;

namespace FStudio.MatchEngine.Players.Behaviours {
    public class InputShootBehaviour : BaseBehaviour, IInputBehaviour {
        private (Transform point, float angleFree) shootingTarget;

        private Vector3 shootingDir;
        private float shotPower = 1f;

        public bool IsTriggered { private get; set; }
        public Vector3 InputDirection { set; private get; }

        public void SetCharge(float normalizedCharge) {
            // A tap still kicks the ball; a full charge preserves the original maximum.
            shotPower = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(normalizedCharge));
            shootingTarget = default;
        }

        public override bool Behave(bool isAlreadyActive) {
            if (!IsTriggered && !isAlreadyActive) {
                return false;
            }

            if (!Player.isInputControlled) {
                IsTriggered = false;
                shootingTarget = default;
                return false;
            }

            if (Player.IsThrowHolder) {
                IsTriggered = false;
                shootingTarget = default;
                return false;
            }

            if (Player.IsCornerHolder) {
                IsTriggered = false;
                shootingTarget = default;
                return false;
            }

            if (ball.HolderPlayer != Player) {
                IsTriggered = false;
                shootingTarget = default;
                return false;
            }

            if (shootingTarget.point == null) {
                shootingTarget = targetGoalNet.GetShootingVector(
                    Player, opponents);

                shootingDir = shootingTarget.point.position - Player.Position;

                Debug.Log($"[SHOULD SHOOT] {shootingTarget}");

                isAlreadyActive = true;
            }

            if (isAlreadyActive) {
                Player.GameTeam.KeepPlayerBehavioursForAShortTime();

                Player.CurrentAct = Acts.InputShoot;

                Debug.Log($"Shooting => {shootingTarget}");

                Player.Stop(in deltaTime);

                if (Player.LookTo(in deltaTime, shootingDir)) {
                    var shootPowerByAngleFree = EngineOptions_ShootingSettings.Current.shootPowerModByAngleFree.Evaluate(shootingTarget.angleFree);

                    var target = targetGoalNet.
                        GetShootingVectorFromPoint(Player, shootingTarget.point) * shootPowerByAngleFree;

                    Player.Shoot(target * shotPower);

                    shootingTarget = default;
                }

                return true;
            }

            return false;
        }
    }
}
