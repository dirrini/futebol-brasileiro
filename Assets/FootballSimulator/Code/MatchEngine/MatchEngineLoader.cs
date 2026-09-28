using FStudio.Events;
using FStudio.Graphics;
using FStudio.Loaders;
using FStudio.Graphics.TimeOfDay;
using FStudio.UI;
using FStudio.UI.Events;
using FStudio.UI.GamepadInput;
using FStudio.UI.MatchThemes;
using FStudio.Utilities;
using Shared.Responses;
using FStudio.Data;
using System.Threading.Tasks;
using UnityEngine;
using FStudio.MatchEngine.Graphics.GraphicsModes;
using FStudio.Graphics.Cameras;
using FStudio.UI.MatchThemes.MatchEvents;
using FStudio.MatchEngine.Enums;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using System;

namespace FStudio.MatchEngine {
    public class MatchEngineLoader : SceneObjectSingleton<MatchEngineLoader> {
        [SerializeField] private SingleAddressableLoader loader;

        private bool isLoading;
        private bool isLoaded;
        private bool isUnloading;
        private int loadGeneration;
        private Task engineLoadTask;
        private Task unloadTask;

        public static async Task CreateMatch(MatchCreateRequest matchData) {
            // close all UI.
            EventManager.Trigger(new CloseAllPanelsEvent());

            // clear all snap history.
            SnapManager.Clear();

            // load the match UI
            await UILoader.Current.MatchUILoader.Load();

            // unload the general UI
            UILoader.Current.GeneralUILoader.Unload();

            var upcomingMatchEvent = new UpcomingMatchEvent(matchData);

            EventManager.Trigger(upcomingMatchEvent);
        }

        public async Task StartMatchEngine (
            UpcomingMatchEvent matchEvent,
            bool homeKit,
            bool awayKit) {

            if (isLoading || isLoaded || isUnloading) {
                return;
            }
            isLoading = true;
            var generation = ++loadGeneration;
            var session = FriendlyMatchSession.Current;
            var lease = session.ActiveMatch;
            // Unload awaits the creation work, not this wrapper: failure recovery
            // may itself call UnloadMatch and must not wait for its own completion.
            engineLoadTask = LoadMatchEngine(matchEvent, homeKit, awayKit, generation);
            try {
                await engineLoadTask;
            } catch (Exception exception) {
                if (IsCurrentLoad(generation) && session != null &&
                    ReferenceEquals(session.ActiveMatch, lease)) {
                    await session.ReportMatchFailure(exception);
                }
            } finally {
                if (generation == loadGeneration) {
                    isLoading = false;
                    EventManager.Trigger<BigLoadingEvent>(null);
                }
            }
        }

        private bool IsCurrentLoad(int generation) {
            return this != null && generation == loadGeneration && !isUnloading;
        }

        private async Task LoadMatchEngine(UpcomingMatchEvent matchEvent,
            bool homeKit, bool awayKit, int generation) {
            // match kits.
            EventManager.Trigger(
                new MatchKitsEvent(
                homeKit ? matchEvent.details.homeTeam.AwayKit : matchEvent.details.homeTeam.HomeKit,
                awayKit ? matchEvent.details.awayTeam.AwayKit : matchEvent.details.awayTeam.HomeKit));
            //

            // close all UI.
            EventManager.Trigger(new CloseAllPanelsEvent());

            // Big loading.
            EventManager.Trigger(new BigLoadingEvent());

            var template = GraphicLoaders.Current;

            // load stadium scene
            StadiumType stadium = StadiumType.SmallStadium;
            await template.stadiumLoader.LoadStadium(stadium);
            if (!IsCurrentLoad(generation)) return;
            //

            await loader.Load(); // load match prefab.
            if (!IsCurrentLoad(generation)) return;

            if (TimeOfDaySystem.Current != null) {
                // load time of day.
                await TimeOfDaySystem.Current.LoadTemplate(matchEvent.details.dayTime);
                if (!IsCurrentLoad(generation)) return;
            }

            MainCamera.Current.Camera.cullingMask = template.renderLayer;

            // skybox mode on.
            MainCamera.Current.Camera.clearFlags = CameraClearFlags.Skybox;

            Debug.Log("Creating core match...");

            await MatchManager.CreateMatch(
                new MatchManager.MatchDetails(
                    matchEvent,
                    homeKit,
                    awayKit)
                );
            if (!IsCurrentLoad(generation)) return;

            Debug.Log("Loading ball...");

            // load random ball.
            await template.ballLoader.LoadRandomBall();
            if (!IsCurrentLoad(generation)) return;
            
            isLoaded = true;

            Debug.Log("Done...");
            var catalogMatch = FriendlyMatchSession.Current.ActiveMatch;
            if (catalogMatch != null) {
                Debug.Log("[FootballWorld] 3D friendly started with " + catalogMatch.Players.Count +
                    " imported players from database revision " + catalogMatch.Catalog.DatabaseRevision + ".");
            }
        }

        public Task UnloadMatch () {
            if (unloadTask != null && !unloadTask.IsCompleted) return unloadTask;
            isUnloading = true;
            ++loadGeneration;
            unloadTask = UnloadMatchCore(engineLoadTask);
            return unloadTask;
        }

        private async Task UnloadMatchCore(Task pendingCreation) {
            // Publish the shared unload task before events can reenter UnloadMatch.
            // Yield stays on Unity's synchronization context, including WebGL.
            await Task.Yield();
            try {
                EventManager.Trigger(new CloseAllPanelsEvent());
                SnapManager.Clear();

                if (pendingCreation != null) {
                    try {
                        // Addressables and player creation are not cancellable here.
                        // Keep their consumers and database lease alive until they settle.
                        await pendingCreation;
                    } catch (Exception) {
                        // The start wrapper reports an active failure. A cancelled
                        // generation must only finish cleanup, never reopen recovery.
                    }
                }

                if (MainCamera.Current != null)
                    MainCamera.Current.Camera.clearFlags = CameraClearFlags.SolidColor;

                // This also handles cancelling the preparation screen, before a
                // MatchManager exists. Release consumers before their database lease.
                if (MatchManager.Current != null) MatchManager.Current.ClearMatch();
                UILoader.Current.MatchUILoader.Unload();
                loader.Unload();

                var template = GraphicLoaders.Current;
                if (template != null) {
                    template.ballLoader.UnloadBall();
                    template.stadiumLoader.Unload();
                }

                await UnityAsync.Delay(1);
                FriendlyMatchSession.ReleaseActiveMatch();
                if (UILoader.Current.GeneralUILoader.CurrentInstantiated == null)
                    await UILoader.Current.GeneralUILoader.Load();
                GameInput.SwitchToUI();
            } finally {
                engineLoadTask = null;
                isLoaded = false;
                isLoading = false;
                isUnloading = false;
                EventManager.Trigger<BigLoadingEvent>(null);
            }
        }
    }
}
