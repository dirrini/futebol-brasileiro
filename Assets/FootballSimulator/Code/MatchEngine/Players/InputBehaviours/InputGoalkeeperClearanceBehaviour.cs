using System.Linq;
using FStudio.MatchEngine.Enums;
using FStudio.MatchEngine.Input;
using FStudio.MatchEngine.Players.Behaviours;
using UnityEngine;

namespace FStudio.MatchEngine.Players.InputBehaviours
{
    // Reuses the existing long-ball / goalkeeper-degage animation and its ball event.
    public sealed class InputGoalkeeperClearanceBehaviour : BaseBehaviour, IInputBehaviour
    {
        public bool IsTriggered { private get; set; }
        public Vector3 InputDirection { private get; set; }
        private float charge;
        private bool hasTarget;
        private Vector3 target;

        public void SetCharge(float value)
        {
            charge = Mathf.Clamp01(value);
            hasTarget = false;
        }

        public override bool Behave(bool isAlreadyActive)
        {
            if (!IsTriggered && !isAlreadyActive) return false;
            if (!Player.isInputControlled || !Player.IsGK || ball.HolderPlayer != Player)
            {
                IsTriggered = false;
                hasTarget = false;
                return false;
            }
            if (!hasTarget)
            {
                var settings = MatchControlSettings.Current;
                target = TargetPoint(Player.Position, InputDirection, Player.GoalDirection, new Vector2(fieldEndX, fieldEndY),
                    charge, settings.GoalkeeperClearanceMinRange, settings.GoalkeeperClearanceMaxRange);
                hasTarget = true;
            }
            IsTriggered = false;
            Player.CurrentAct = Acts.InputPass;
            Player.Stop(in deltaTime);
            if (Player.LookTo(in deltaTime, target - Player.Position))
            {
                Player.PassingTarget = teammates.Where(player => player != Player && player.PlayerController.IsPhysicsEnabled && !player.CaughtInOffside)
                    .OrderBy(player => Vector3.SqrMagnitude(player.Position - target)).FirstOrDefault();
                Player.Cross(target);
            }
            return true;
        }

        public static Vector3 TargetPoint(Vector3 position, Vector3 aim, Vector3 forward, Vector2 pitch,
            float normalizedCharge, float minimumRange, float maximumRange)
        {
            aim.y = 0;
            forward.y = 0;
            if (aim.sqrMagnitude < .01f) aim = forward;
            if (aim.sqrMagnitude < .01f) aim = Vector3.right;
            var distance = Mathf.Lerp(minimumRange, maximumRange, Mathf.Clamp01(normalizedCharge));
            var target = position + aim.normalized * distance;
            target.y = 0;
            target.x = Mathf.Clamp(target.x, 1, Mathf.Max(1, pitch.x - 1));
            target.z = Mathf.Clamp(target.z, 1, Mathf.Max(1, pitch.y - 1));
            return target;
        }
    }
}
