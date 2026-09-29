using FStudio.UI.Events;

using UnityEngine;

using FStudio.Events;
using FStudio.MatchEngine.Events;
using System.Threading.Tasks;
using FStudio.MatchEngine.UI;
using FStudio.Utilities;

namespace FStudio.UI {
    public class FinalWhistlePanel : MonoBehaviour {
        private const int showStatisticsAfterSeconds = 3000;
        private int generation;

        private void OnEnable() {
            EventManager.Subscribe<FinalWhistleEvent>(OnEventCalled);
        }

        private void OnDisable() {
            generation++;
            EventManager.UnSubscribe<FinalWhistleEvent>(OnEventCalled);
        }

        protected async void OnEventCalled(FinalWhistleEvent eventObject) {
            var expectedGeneration = ++generation;
            EventManager.Trigger<ShowScoreboardEvent>(null);

            EventManager.Trigger(new InfoboardEvent());

            // show statistics.

            await UnityAsync.Delay(showStatisticsAfterSeconds);
            if (this == null || !isActiveAndEnabled || generation != expectedGeneration) return;

            // Show statistics.
            EventManager.Trigger(new MatchStatisticsEvent());
        }
    }
}


