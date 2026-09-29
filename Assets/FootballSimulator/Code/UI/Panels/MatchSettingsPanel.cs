using FStudio.Data;
using FStudio.MatchEngine.Enums;
using TMPro;
using UnityEngine;
using FStudio.FootballWorld.Infrastructure.GameModes;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;

namespace FStudio.UI.Panels {
    internal class MatchSettingsPanel : MonoBehaviour {
        private const string SETTING_AILEVEL = "SETTING_AILEVEL";
        private const string SETTING_DAYTIME = "SETTING_DAYTIME";
        private const string SETTING_SIDE = "SETTING_SIDE";

        public static DayTimes DAYTIMES { private set; get; }
        public static AILevel AILEVEL => GameUserSettings.Current.Difficulty;
        public static Shared.Responses.MatchCreateRequest.UserTeam SIDE {  private set; get; }


        [SerializeField] private TextMeshProUGUI 
            dayTimeText,
            sideText,
            difficultyText;

        [SerializeField] private Selector 
            dayTimeSelector, 
            sideSelector, 
            aiLevelSelector;

        private InteractiveUIElement[] sideArrows;
        private bool[] sideArrowVisibility;

        private void Awake() {
            dayTimeSelector.Max = 3;
            aiLevelSelector.Max = 4;
            InitializeSideSelection();

            dayTimeSelector.OnSelectionUpdate += (val) => {
                DAYTIMES = (DayTimes)val;
                dayTimeText.text = GameText.Get("daytime." + DAYTIMES);
                PlayerPrefs.SetInt(SETTING_DAYTIME, val);
            };

            aiLevelSelector.Max = (int)AILevel.Legendary + 1;
            aiLevelSelector.OnSelectionUpdate = (int val) => {
                GameUserSettings.Current.SetDifficulty((AILevel)val);
                difficultyText.text = GameText.Get("difficulty." + AILEVEL);
            };

            var aiLevelSetting = (int)GameUserSettings.Current.Difficulty;
            var dayTimeSetting = Mathf.Clamp(PlayerPrefs.GetInt(SETTING_DAYTIME, 2), 0, 2);

            aiLevelSelector.SetSelected(aiLevelSetting);
            dayTimeSelector.SetSelected(dayTimeSetting);

            gameObject.SetActive(false);
        }

        private void OnEnable() {
            GameText.EnsureLoaded();
            GameText.Changed += RefreshLabels;
            RefreshLabels();
        }

        private void OnDisable() { GameText.Changed -= RefreshLabels; }

        private void InitializeSideSelection() {
            sideSelector.Max = 3;
            SIDE = (Shared.Responses.MatchCreateRequest.UserTeam)
                Mathf.Clamp(PlayerPrefs.GetInt(SETTING_SIDE, 0), 0, 2);
            sideArrows = sideSelector.transform.parent.GetComponentsInChildren<InteractiveUIElement>(true);
            sideArrowVisibility = new bool[sideArrows.Length];
            for (var index = 0; index < sideArrows.Length; index++)
                sideArrowVisibility[index] = sideArrows[index].gameObject.activeSelf;
            sideSelector.OnSelectionUpdate += OnSideSelectionChanged;
            RefreshSideSelection();
        }

        private void OnSideSelectionChanged(int value) {
            if (!FriendlyMatchSession.Current.LockedUserSide.HasValue) {
                SIDE = (Shared.Responses.MatchCreateRequest.UserTeam)value;
                PlayerPrefs.SetInt(SETTING_SIDE, value);
            }
            RefreshSideSelection();
        }

        private void RefreshSideSelection() {
            var lockedSide = FriendlyMatchSession.Current.LockedUserSide;
            var displayedSide = lockedSide ?? SIDE;
            if (sideText != null) sideText.text = GameText.Get("side." + displayedSide);
            if (sideSelector != null) sideSelector.SetSelectedSilent((int)displayedSide);
            if (sideArrows == null) return;
            // The arrows have fixed authored RectTransforms; hiding them leaves the row intact.
            for (var index = 0; index < sideArrows.Length; index++)
                sideArrows[index].gameObject.SetActive(!lockedSide.HasValue && sideArrowVisibility[index]);
        }

        private void RefreshLabels() {
            if (dayTimeText != null) dayTimeText.text = GameText.Get("daytime." + DAYTIMES);
            RefreshSideSelection();
            if (difficultyText != null) difficultyText.text = GameText.Get("difficulty." + AILEVEL);
            if (aiLevelSelector != null && aiLevelSelector.CurrentSelected != (int)AILEVEL)
                aiLevelSelector.SetSelected((int)AILEVEL);
        }
    }
}
