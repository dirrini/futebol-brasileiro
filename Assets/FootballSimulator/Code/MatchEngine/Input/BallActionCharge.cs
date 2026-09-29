using System;

namespace FStudio.MatchEngine.Input {
    public enum ChargedBallAction { None, ShortPass, ThroughPass, Cross, Shot }

    // A hold belongs to one action and one player instance, never to another
    // button or to the player selected after a control switch.
    public sealed class BallActionCharge {
        public ChargedBallAction Action { get; private set; }
        public object Owner { get; private set; }
        public bool BeganWithoutPossession { get; private set; }
        private float startedAt;
        public bool IsActive => Action != ChargedBallAction.None;
        public bool TryBegin(ChargedBallAction action, object owner, float now, bool beganWithoutPossession = false) {
            if (IsActive || action == ChargedBallAction.None || !Enum.IsDefined(typeof(ChargedBallAction), action) || owner == null || !Finite(now)) return false;
            Action = action; Owner = owner; startedAt = now; BeganWithoutPossession = beganWithoutPossession; return true;
        }
        public bool MatchesPossession(object holder) => IsActive &&
            (BeganWithoutPossession ? holder == null : ReferenceEquals(holder, Owner));
        public float Power(float now, float duration) {
            if (!IsActive || !Finite(now) || !Finite(duration) || duration <= 0) return 0;
            return Math.Max(0, Math.Min(1, (now - startedAt) / duration));
        }
        public bool TryRelease(ChargedBallAction action, object owner, float now, float duration, out float power) {
            power = 0;
            if (!IsActive || Action != action) return false;
            var valid = ReferenceEquals(Owner, owner);
            if (valid) power = Power(now, duration);
            Cancel(); return valid;
        }
        public void Cancel() { Action = ChargedBallAction.None; Owner = null; startedAt = 0; BeganWithoutPossession = false; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
