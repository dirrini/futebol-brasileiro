using System;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using FStudio.FootballWorld.Infrastructure.GameModes;
using FStudio.FootballWorld.Presentation;
using FStudio.UI.Events;
using FStudio.UI.GamepadInput;
using FStudio.UI.Utilities;
using TMPro;
using UnityEngine;

namespace FStudio.UI.Panels {
    public class MainMenuPanel : EventPanel<QuickMatchEvent> {
        [SerializeField] private TeamSelectionTeam homeTeam, awayTeam;
        [SerializeField] private TeamCountrySelection homeCountry, awayCountry;
        [SerializeField] private InteractiveUIElement playButton, retryButton;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField, Range(0f, 1f)] private float disabledPlayAlpha = 0.4f;

        private FriendlyMatchSession session;

        protected override void OnEnable() {
            base.OnEnable();
            homeTeam.SelectionChanged += SelectHome;
            awayTeam.SelectionChanged += SelectAway;
            if (homeCountry != null) homeCountry.SelectionChanged += SelectHomeCountry;
            if (awayCountry != null) awayCountry.SelectionChanged += SelectAwayCountry;
            if (playButton != null) playButton.onAppeared.AddListener(Refresh);
            if (retryButton != null) retryButton.onAppeared.AddListener(Refresh);
            AttachSession();
            Refresh();
        }

        protected override void OnDisable() {
            if (session != null) session.Changed -= Refresh;
            session = null;
            homeTeam.SelectionChanged -= SelectHome;
            awayTeam.SelectionChanged -= SelectAway;
            if (homeCountry != null) homeCountry.SelectionChanged -= SelectHomeCountry;
            if (awayCountry != null) awayCountry.SelectionChanged -= SelectAwayCountry;
            if (playButton != null) playButton.onAppeared.RemoveListener(Refresh);
            if (retryButton != null) retryButton.onAppeared.RemoveListener(Refresh);
            base.OnDisable();
        }

        private void AttachSession() {
            var current = FriendlyMatchSession.Current;
            if (session == current) return;
            if (session != null) session.Changed -= Refresh;
            session = current;
            if (session != null) session.Changed += Refresh;
        }

        protected override void OnEventCalled(QuickMatchEvent eventObject) {
            if (eventObject == null) {
                Disappear();
                return;
            }
            AttachSession();
            Appear();
            SnapManager.Enable();
            GameInput.SwitchToUI();
            Refresh();
        }

        private void Refresh() {
            var ready = session != null && session.State == FriendlyMatchState.Ready;
            homeCountry?.Bind(session?.Countries, session?.SelectedHomeCountryCode, ready);
            awayCountry?.Bind(session?.Countries, session?.SelectedAwayCountryCode, ready);
            homeTeam.Bind(session?.HomeTeams, session?.SelectedHomeClubId, ready);
            awayTeam.Bind(session?.AwayTeams, session?.SelectedAwayClubId, ready);

            if (statusText != null) {
                statusText.richText = false;
                statusText.text = session != null ? session.StatusMessage : GameText.Get("hub.loading");
            }
            var canPlay = session != null && session.CanPlay;
            if (playButton != null) {
                playButton.isInteractionEverEnabled = canPlay;
                playButton.IsInteractable = canPlay;
                playButton.SetRaycast(canPlay);
                var group = playButton.GetComponent<CanvasGroup>();
                if (group != null) group.alpha = canPlay ? 1f : disabledPlayAlpha;
            }
            if (retryButton != null) {
                var canRetry = session != null && session.State == FriendlyMatchState.Failed;
                retryButton.gameObject.SetActive(canRetry);
                retryButton.isInteractionEverEnabled = canRetry;
                retryButton.IsInteractable = canRetry;
                retryButton.SetRaycast(canRetry);
            }
        }

        private void SelectHome(string clubId) => session?.Select(false, clubId);
        private void SelectAway(string clubId) => session?.Select(true, clubId);
        private void SelectHomeCountry(string code) => session?.SelectCountry(false, code);
        private void SelectAwayCountry(string code) => session?.SelectCountry(true, code);

        public void BackToHome() => GameHubSession.Current.Navigate(HubPage.Home);

        public void Retry() {
            if (session == null || session.State != FriendlyMatchState.Failed) return;
            session.Retry();
        }

        public async void Play() {
            if (session == null || !session.CanPlay) return;
            try {
                await session.StartMatch();
            } catch (Exception exception) {
                // The session owns recovery/status. Keep technical details in logs.
                Debug.LogException(exception);
                if (this != null) Refresh();
            }
        }
    }
}
