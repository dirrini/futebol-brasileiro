using FStudio.Events;
using FStudio.MatchEngine.Enums;
using FStudio.MatchEngine.Events;
using FStudio.MatchEngine.Input;
using FStudio.MatchEngine.Players.PlayerController;
using FStudio.MatchEngine.Players.InputBehaviours;
using UnityEngine;

namespace FStudio.MatchEngine.Players.Behaviours {
    // Hold short pass without possession: pursue and attempt a standing challenge.
    // Release stops pursuit; recovering possession never queues an accidental pass.
    public class InputTackleBehaviour : BaseBehaviour, IInputBehaviour {
        public MovementType MovementType { get; private set; }
        public bool IsTriggered { get; set; }
        public bool SprintHeld { get; set; }
        public Vector3 InputDirection { set; private get; }
        private float nextStandingAttempt;
        public override bool Behave(bool isAlreadyActive) {
            if (!IsTriggered || !Player.isInputControlled || !Player.PlayerController.IsPhysicsEnabled ||
                matchStatus != MatchStatus.Playing || Player.IsThrowHolder || Player.IsCornerHolder || Player.IsHoldingBall) {
                IsTriggered = false; return false;
            }
            var holder = ball.HolderPlayer;
            if (holder != null && (holder.GameTeam == Player.GameTeam || holder.IsGKUntouchable)) return false;
            var target = holder == null ? ball.BallPosition(Player) : PlayerBase.Predicter(Player, holder);
            var settings = MatchControlSettings.Current;
            if (holder != null && time >= nextStandingAttempt &&
                Vector3.Distance(holder.Position, Player.Position) <= settings.StandingTackleDistance) {
                nextStandingAttempt = time + settings.StandingTackleCooldown;
                AttemptStandingChallenge(holder);
            }
            Player.CurrentAct = Acts.GoingToTackle;
            MovementType = SprintHeld ? MovementType.BestHeCanDo : MovementType.Normal;
            Player.MoveTo(deltaTime, target, true, MovementType);
            return true;
        }

        // Isolate the physical contact from the hold/distance/cooldown decision.
        protected virtual void AttemptStandingChallenge(PlayerBase holder) {
            var keeping = holder.MatchPlayer.ActualBallKeeping;
            var tackling = Player.MatchPlayer.ActualTackling;
            if (Random.Range(keeping * .5f, keeping) < Random.Range(tackling / EngineSettings.Current.Tackling_Difficulty, tackling)) {
                EventManager.Trigger(new PlayerWinTheBallEvent(Player));
                EventManager.Trigger(new PlayerLossTheBallEvent(holder));
                holder.Struggle();
            }
        }
    }
}
