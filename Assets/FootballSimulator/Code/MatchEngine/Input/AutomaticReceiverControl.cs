using System;

namespace FStudio.MatchEngine.Input
{
    // A release-correlated handoff: intention alone never changes control.
    public sealed class AutomaticReceiverControl<T> where T : class
    {
        private T passer;
        private T receiver;
        private float deadline;
        public T AssistedReceiver { get; private set; }

        public void Queue(T source, T target, float now)
        {
            passer = source;
            receiver = target;
            deadline = now + 1f;
            AssistedReceiver = null;
        }

        public T Resolve(T holder, T lastHolder, float now, Func<T, bool> eligible, Func<T> fallback)
        {
            if (holder != null)
            {
                if (!ReferenceEquals(holder, passer) || now > deadline) Cancel();
                return eligible(holder) ? holder : null;
            }
            if (passer == null) return null;
            if (now > deadline || !ReferenceEquals(lastHolder, passer)) { Cancel(); return null; }
            var selected = receiver != null && eligible(receiver) ? receiver : fallback?.Invoke();
            passer = null;
            receiver = null;
            AssistedReceiver = selected != null && eligible(selected) ? selected : null;
            return AssistedReceiver;
        }

        public void ReleaseAssist() => AssistedReceiver = null;
        public void Cancel() { passer = null; receiver = null; AssistedReceiver = null; }
    }
}
