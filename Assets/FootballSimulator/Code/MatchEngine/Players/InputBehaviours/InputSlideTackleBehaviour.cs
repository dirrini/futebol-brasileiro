using FStudio.MatchEngine.Enums;
using UnityEngine;

namespace FStudio.MatchEngine.Players.Behaviours {
    public class InputSlideTackleBehaviour : BaseBehaviour, InputBehaviours.IInputBehaviour {
        public bool IsTriggered { private get; set; }
        public Vector3 InputDirection { private get; set; }
        public override bool Behave(bool isAlreadyActive) {
            if (!IsTriggered) return false;
            IsTriggered = false;
            if (!Player.isInputControlled || !Player.PlayerController.IsPhysicsEnabled ||
                matchStatus != MatchStatus.Playing || Player.IsHoldingBall || Player.IsThrowHolder || Player.IsCornerHolder ||
                ball.HolderPlayer?.GameTeam == Player.GameTeam) return false;
            PerformSlideTackle();
            return true;
        }
        protected virtual void PerformSlideTackle() => Player.DoTackle(ball);
    }
}
