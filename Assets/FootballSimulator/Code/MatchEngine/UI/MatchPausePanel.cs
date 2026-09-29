using FStudio.Events;
using FStudio.MatchEngine.Cameras;
using FStudio.MatchEngine.Events;
using FStudio.UI;
using FStudio.UI.Events;
using TMPro;
using UnityEngine;
using FStudio.FootballWorld.Infrastructure.GameModes;

namespace FStudio.MatchEngine.UI {
    internal class MatchPausePanel : EventPanel<MatchPauseEvent> {
        private const string SETTING_QUALITY = "SETTING_QUALITY";

        private const int DEFAULT_QUALIY =
#if UNITY_STANDALONE
2
#else
            1
#endif
;



        [SerializeField] private Selector qualitySelector;
        [SerializeField] private Selector matchCameraSelector;
        [SerializeField] private TextMeshProUGUI qualityText, cameraText;

        private readonly string[] cameraTypes = new string[4] {
            "Broadcast",
            "Stadium",
            "Tele",
            "StadiumHigh"
        };

        private void Awake() {
            var qNames = QualitySettings.names;

            qualitySelector.Max = qNames.Length;
            matchCameraSelector.Max = cameraTypes.Length;

            matchCameraSelector.OnSelectionUpdate += async (val) => {
                var cam = cameraTypes[val];
                GameUserSettings.Current.SetCamera(cam);
                if (CameraSystem.Current != null) await CameraSystem.Current.SwitchCamera(cam);

                if (this != null) cameraText.text = GameText.Get("camera." + cam);

            };

            qualitySelector.OnSelectionUpdate += (val) => {
                QualitySettings.SetQualityLevel(val);

                qualityText.text = QualitySettings.names[val];

                PlayerPrefs.SetInt(SETTING_QUALITY, val);

                Shader.SetGlobalFloat("_SHADER_LAYER_COUNT", 2 + val * 3 + val);
            };

            var qSetting = Mathf.Clamp(PlayerPrefs.GetInt(SETTING_QUALITY, DEFAULT_QUALIY), 0, qNames.Length - 1);
            var cSetting = System.Array.IndexOf(cameraTypes, GameUserSettings.Current.CameraId);

            qualitySelector.SetSelected(qSetting);
            matchCameraSelector.SetSelected(cSetting);
        }

        protected override void OnDisappearing() {
            base.OnDisappearing();
            GameInput.SwitchToMatchEngine();
            MatchPause.Resume();
        }

        protected override void OnEventCalled(MatchPauseEvent eventObject) {
            if (eventObject == null) {
                Disappear();
                return;
            }

            GameInput.SwitchToUI();

            Appear();
        }

        public async void LeaveMatch() {
            EventManager.Trigger(new LoadingEvent());

            if (MatchEngineLoader.Current != null) {
                await MatchEngineLoader.Current.UnloadMatch();
            }

            EventManager.Trigger(new CloseAllPanelsEvent());
            GameHubSession.Current.ReturnToMatchOrigin();
        }
    }
}
