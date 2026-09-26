using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FStudio.Utilities {
    public static class UnityAsync {
        /// <summary>
        /// Delay in real milliseconds. Call from Unity's main thread.
        /// WebGL timers advance through the player loop instead of System.Threading.Timer.
        /// </summary>
        public static Task Delay(int milliseconds) {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (milliseconds < -1) {
                throw new ArgumentOutOfRangeException(nameof(milliseconds));
            }
            if (milliseconds == 0) {
                return Task.CompletedTask;
            }
            if (milliseconds == -1) {
                return new TaskCompletionSource<bool>().Task;
            }

            return DelayOnMainThread(milliseconds);
#else
            return Task.Delay(milliseconds);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static async Task DelayOnMainThread(int milliseconds) {
            var deadline = Time.realtimeSinceStartupAsDouble + milliseconds / 1000d;
            while (Time.realtimeSinceStartupAsDouble < deadline) {
                await Task.Yield();
            }
        }
#endif
    }
}
