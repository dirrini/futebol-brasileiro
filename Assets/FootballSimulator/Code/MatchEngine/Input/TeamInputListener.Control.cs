using System.Linq;
using FStudio.Events;
using FStudio.MatchEngine.Balls;
using FStudio.MatchEngine.Enums;
using FStudio.MatchEngine.Events;
using FStudio.MatchEngine.Players;
using FStudio.MatchEngine.Players.InputBehaviours;
using UnityEngine;

namespace FStudio.MatchEngine.Input
{
    public partial class TeamInputListener
    {
        private readonly AutomaticReceiverControl<PlayerBase> receiverControl = new AutomaticReceiverControl<PlayerBase>();
        private bool receiverFromCross;
        private PlayerBase recentPasser;

        private void InitializeControlSwitching()
        {
            EventManager.Subscribe<PlayerPassEvent>(OnControlledPass);
            EventManager.Subscribe<PlayerCrossEvent>(OnControlledCross);
            EventManager.Subscribe<PlayerShootEvent>(OnControlledShot);
        }

        private void ClearControlSwitching()
        {
            EventManager.UnSubscribe<PlayerPassEvent>(OnControlledPass);
            EventManager.UnSubscribe<PlayerCrossEvent>(OnControlledCross);
            EventManager.UnSubscribe<PlayerShootEvent>(OnControlledShot);
            receiverControl.Cancel();
            recentPasser = null;
        }

        private void OnControlledPass(PlayerPassEvent value) => QueueReceiver(value.Player, false);
        private void OnControlledCross(PlayerCrossEvent value) => QueueReceiver(value.Player, true);
        private void OnControlledShot(PlayerShootEvent value)
        {
            if (value.Player == ActivePlayer) receiverControl.Cancel();
        }

        private void QueueReceiver(PlayerBase passer, bool cross)
        {
            if (passer == null || passer != ActivePlayer || passer.GameTeam != gameTeam) return;
            recentPasser = passer;
            receiverFromCross = cross;
            receiverControl.Queue(passer, passer.PassingTarget, Time.unscaledTime);
        }

        private bool EligibleControlledPlayer(PlayerBase player)
            => player != null && player.GameTeam == gameTeam && player.PlayerController.IsPhysicsEnabled;

        private void UpdateAutomaticControl()
        {
            if (Ball.Current == null || MatchManager.Current == null) return;
            if (MatchManager.Current.MatchFlags == MatchStatus.NotPlaying || MatchManager.Current.MatchFlags == MatchStatus.Freeze)
            {
                receiverControl.Cancel();
                return;
            }
            var ball = Ball.Current;
            var selected = receiverControl.Resolve(ball.HolderPlayer, ball.LastHolder, Time.unscaledTime,
                EligibleControlledPlayer, SelectReceiverNearDestination);
            if (selected != null && selected != ActivePlayer) ActivePlayer = selected;
            if (selected != null && receiverControl.AssistedReceiver == selected)
                selected.ActivateBehaviour("BallChasingWithoutCondition");
            if (direction.magnitude >= MOVE_DEADZONE && receiverControl.AssistedReceiver != null)
            {
                var assisted = receiverControl.AssistedReceiver;
                receiverControl.ReleaseAssist();
                // The direct receiving chase has NextBehaviour=-1. End it explicitly
                // so it cannot override the user's movement later in this frame.
                if (assisted.ActiveBehaviour is not IInputBehaviour) assisted.ResetBehaviours();
            }
        }

        private PlayerBase SelectReceiverNearDestination()
        {
            var ball = Ball.Current;
            var destination = receiverFromCross ? ball.CrossTarget : ball.transform.position + ball.Velocity;
            return gameTeam.GamePlayers.Where(player => player != recentPasser && EligibleControlledPlayer(player) && !player.CaughtInOffside)
                .OrderBy(player => Vector3.SqrMagnitude(player.Position - destination)).FirstOrDefault();
        }

        public bool IsAssistingReceiver(PlayerBase player)
            => player != null && player == ActivePlayer && receiverControl.AssistedReceiver == player &&
                direction.magnitude < MOVE_DEADZONE && Ball.Current != null && Ball.Current.HolderPlayer == null &&
                player.ActiveBehaviour is not IInputBehaviour && MatchManager.Current != null &&
                MatchManager.Current.MatchFlags == MatchStatus.Playing;

        private bool CanChargeGoalkeeperClearance(PlayerBase player)
        {
            if (player == null || !player.IsGK || player != ActivePlayer || !EligibleControlledPlayer(player) ||
                Ball.Current == null || Ball.Current.HolderPlayer != player || MatchManager.Current == null ||
                player.ActiveBehaviour is IInputBehaviour || player.IsThrowHolder || player.IsCornerHolder) return false;
            var status = MatchManager.Current.MatchFlags;
            return status == MatchStatus.Playing || status == MatchStatus.WaitingForKickOff;
        }

        private bool TryActivateGoalkeeperClearance(float charge)
        {
            if (!CanChargeGoalkeeperClearance(ActivePlayer)) return false;
            var behaviour = ActivePlayer.Behaviours.OfType<InputGoalkeeperClearanceBehaviour>().FirstOrDefault();
            if (behaviour == null) return false;
            behaviour.SetCharge(charge);
            ActivatePlayerBehaviour<InputGoalkeeperClearanceBehaviour>();
            return true;
        }
    }
}
