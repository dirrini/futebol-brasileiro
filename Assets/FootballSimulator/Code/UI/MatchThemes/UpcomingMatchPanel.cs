using UnityEngine;
using FStudio.UI.Events;
using FStudio.Events;
using FStudio.UI.MatchThemes.MatchEvents;
using FStudio.MatchEngine;
using FStudio.MatchEngine.Enums;
using TMPro;
using FStudio.UI.Panels;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using FStudio.FootballWorld.Infrastructure.GameModes;
using System;

namespace FStudio.UI.MatchThemes {
    public class UpcomingMatchPanel : EventPanel<UpcomingMatchEvent> {
        [SerializeField] private TeamVisual[] teams = new TeamVisual[2];
        [SerializeField] private TextMeshProUGUI difficultyText;

        private UpcomingMatchEvent eventObject;
        private bool isTransitioning;
        private int preparationGeneration;

        /// <summary>
        /// home kit or away kit.
        /// </summary>
        private bool[] kits = new bool[2];

        protected override async void OnEventCalled(UpcomingMatchEvent eventObject) {
            var generation = ++preparationGeneration;
            this.eventObject = eventObject;

            if (eventObject == null) {
                Disappear();
                return;
            }
            isTransitioning = false;
            try {

            Debug.Log("Upcoming match");

            EventManager.Trigger(new LoadingEvent(GameText.Get("hub.busy")));

            await teams [0].SetTeam(
                eventObject.details.homeTeam,
                eventObject.details.homeTeam.Formation,
                eventObject.details.homeTeam.Players);
            if (!IsCurrentPreparation(generation, eventObject)) return;

            await teams [1].SetTeam(
                eventObject.details.awayTeam,
                eventObject.details.awayTeam.Formation,
                eventObject.details.awayTeam.Players);
            if (!IsCurrentPreparation(generation, eventObject)) return;

            kits[0] = false; // set home teams kit to main kit.
            kits[1] = true; // set away teams kit to side kit.

            UpdateKits();

            if (difficultyText != null) difficultyText.text = GameText.Get("difficulty." + GameUserSettings.Current.Difficulty);
            foreach (var label in GetComponentsInChildren<LocalizedText>(true))
                if (label.Key == "match.backTeams" || label.Key == "match.backChampionship")
                    label.Key = FriendlyMatchSession.Current.LockedUserSide.HasValue ? "match.backChampionship" : "match.backTeams";

            Appear();

            EventManager.Trigger<LoadingEvent>(null);
            } catch (Exception exception) {
                if (IsCurrentPreparation(generation, eventObject))
                    await FriendlyMatchSession.Current.ReportMatchFailure(exception);
            }
        }

        private bool IsCurrentPreparation(int generation, UpcomingMatchEvent expectedEvent) {
            return this != null && generation == preparationGeneration && eventObject == expectedEvent;
        }

        protected override void OnDisable() {
            ++preparationGeneration;
            eventObject = null;
            base.OnDisable();
        }

        private void UpdateKits () {
            var homeKit = !kits[0] ? eventObject.details.homeTeam.HomeKit : eventObject.details.homeTeam.AwayKit;
            var awayKit = !kits[1] ? eventObject.details.awayTeam.HomeKit : eventObject.details.awayTeam.AwayKit;

            for (int i = 0; i < 2; i++) {
                var targetKit = i == 0 ? homeKit : awayKit;
                teams[i].KitSolver.SetKit(
                    targetKit.PreviewTexture,
                    targetKit.Color1,
                    targetKit.Color2);
            }
        }

        public async void StartMatch () {
            if (isTransitioning || eventObject == null || !IsActive) return;
            isTransitioning = true;
            var matchEvent = eventObject;
            var session = FriendlyMatchSession.Current;
            var lease = session.ActiveMatch;
            try {
            // update the details.
            var details = matchEvent.details;
            details.aiLevel = GameUserSettings.Current.Difficulty;
            details.dayTime = MatchSettingsPanel.DAYTIMES;
            details.userTeam = session.LockedUserSide ?? MatchSettingsPanel.SIDE;
            matchEvent.details = details;
            //

            // Disappear may disable this component and invalidate its UI event.
            Disappear();
            await MatchEngineLoader.Current.StartMatchEngine(
                matchEvent,
                kits[0],
                kits[1]);
            } catch (Exception exception) {
                if (session != null && ReferenceEquals(session.ActiveMatch, lease) &&
                    (lease == null || !lease.IsDisposed))
                    await session.ReportMatchFailure(exception);
            }
        }

        public async void BackToTeams() {
            if (isTransitioning || eventObject == null || !IsActive) return;
            isTransitioning = true;
            ++preparationGeneration;
            eventObject = null;
            var session = FriendlyMatchSession.Current;
            var lease = session.ActiveMatch;
            try {
                await MatchEngineLoader.Current.UnloadMatch();
                if (session != null && session.ActiveMatch == null) {
                    EventManager.Trigger(new CloseAllPanelsEvent());
                    GameHubSession.Current.ReturnToMatchOrigin();
                }
            } catch (Exception exception) {
                if (session != null && (session.ActiveMatch == null || ReferenceEquals(session.ActiveMatch, lease)))
                    await session.ReportMatchFailure(exception);
            }
        }

        public void SwitchKit (int index) {
            if (isTransitioning || eventObject == null || index < 0 || index >= kits.Length) return;
            kits[index] = !kits[index];

            UpdateKits();
        }
    }
}

