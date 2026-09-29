using FStudio.Events;
using FStudio.MatchEngine.Events;
using UnityEngine;

namespace FStudio.MatchEngine {
    public class MatchPause {
        public static bool IsPaused { private set; get; }

        public static void Resume() {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1.5f;
            EventManager.Trigger<MatchPauseEvent>(null);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { IsPaused = false; }

        public static void Pause() {
            IsPaused = !IsPaused;

            if (!IsPaused) {
                Time.timeScale = 1.5f;
                EventManager.Trigger<MatchPauseEvent> (null);
            } else {
                EventManager.Trigger(new MatchPauseEvent());
                Time.timeScale = 0;
            }
        }
    }
}
