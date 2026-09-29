using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FStudio.Events;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Bootstrap;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using FStudio.MatchEngine;
using FStudio.MatchEngine.Cameras;
using FStudio.MatchEngine.Enums;
using FStudio.MatchEngine.Events;
using FStudio.UI;
using FStudio.UI.Events;
using FStudio.UI.GamepadInput;
using Shared.Responses;
using UnityEngine;

namespace FStudio.FootballWorld.Infrastructure.GameModes
{
    // The lifetime owner for game modes, navigation and the selected season.
    // Domain/application rules remain in pure C#; this host owns Unity adapters.
    public sealed class GameHubSession : MonoBehaviour
    {
        public const string ChampionshipSaveKey = "FOOTBALL_CHAMPIONSHIP_V1";
        public const string CareerSaveKey = "FOOTBALL_CAREER_V1";
        private static GameHubSession current;
        private readonly LocalGameSaveStore saves = new LocalGameSaveStore(new UnityGamePreferenceStore());
        private readonly List<string> saveWarnings = new List<string>();
        private CompetitionSession season;
        private CatalogMatchAdapter seasonAdapter;
        private string seasonDatabaseJson;
        private FixtureExecution execution;
        private CatalogMatchLease executionLease;
        private bool completedExecution;
        private bool busy;
        private HubPage matchOrigin = HubPage.QuickMatch;
        private string statusKey;
        private string statusDetails;
        private DatabaseCatalog observedCatalog;
        private FootballDatabaseLoadState? observedState;
        private GameObject view;
        private FriendlyMatchSession friendly;

        public static GameHubSession Current
        {
            get
            {
                if (current == null)
                {
                    current = FindObjectOfType<GameHubSession>();
                    if (current == null) current = new GameObject("FootballWorld Game Hub").AddComponent<GameHubSession>();
                }
                return current;
            }
        }
        public event Action Changed;
        public HubPage Page { get; private set; } = HubPage.Home;
        public GameUserSettings Settings => GameUserSettings.Current;
        public bool IsBusy => busy || FriendlyMatchSession.Current.ActiveMatch != null;
        public bool DatabaseReady => FootballDatabaseBootstrap.Current != null && FootballDatabaseBootstrap.Current.State == FootballDatabaseLoadState.Ready;
        public IReadOnlyList<CatalogTeamOption> Teams => FriendlyMatchSession.Current.Teams;
        public IReadOnlyList<CatalogCountryOption> Countries => FriendlyMatchSession.Current.Countries;
        public IReadOnlyList<HubEditionOption> Editions { get; private set; } = Array.Empty<HubEditionOption>();
        public DateTime DefaultCareerDate { get; private set; } = DateTime.Today;
        public HubChampionshipView Championship => season == null ? null : new HubChampionshipView(season, IsBusy);
        public HubCareerProfile Career { get; private set; }
        public bool HasChampionshipSave { get; private set; }
        public bool HasCareerSave { get; private set; }
        public string StatusMessage => !string.IsNullOrEmpty(statusKey) ? GameText.Get(statusKey) + (string.IsNullOrEmpty(statusDetails) ? string.Empty : "\n" + statusDetails)
            : IsBusy ? GameText.Get("hub.busy") : DatabaseReady ? string.Empty
            : GameText.Get(FootballDatabaseBootstrap.Current?.State == FootballDatabaseLoadState.Failed ? "session.databaseFailed" : "hub.loading");
        public string SaveWarning => string.Join("\n", saveWarnings.Select(key => GameText.Get(key)));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { current = null; }

        private void Awake()
        {
            if (current != null && current != this) { Destroy(gameObject); return; }
            current = this;
            DontDestroyOnLoad(gameObject);
            GameText.EnsureLoaded();
            GameText.Changed += OnPreferencesChanged;
            EventManager.Subscribe<MainMenuEvent>(OpenHome);
            EventManager.Subscribe<FinalWhistleEvent>(OnFinalWhistle);
            friendly = FriendlyMatchSession.Current;
            friendly.Changed += OnFriendlyChanged;
            RestoreSaves();
            RefreshCatalog();
        }

        private void Update() { RefreshCatalog(); EnsureView(); }
        private void OpenHome(MainMenuEvent value) { if (value != null) Navigate(HubPage.Home); }
        private void OnFriendlyChanged() { Changed?.Invoke(); }

        // Loading overlays may finish after navigation. Restore the input policy
        // for the destination instead of reviving legacy selection on hub forms.
        public void RestoreMenuInput()
        {
            if (Page == HubPage.QuickMatch || IsBusy) SnapManager.Enable();
            else SnapManager.Disable();
        }
        private void OnPreferencesChanged()
        {
            if (Settings.PersistenceFailed) AddSaveWarning("preferences_failed");
            else saveWarnings.Remove("save.preferences_failed");
            Changed?.Invoke();
        }

        private void EnsureView()
        {
            if (view != null || UILoader.Current == null) return;
            var general = UILoader.Current.GeneralUILoader.CurrentInstantiated;
            if (general == null) return;
            var prefab = Resources.Load<GameObject>("FootballWorld/GameHub");
            if (prefab == null) return;
            var parent = general.transform.Find("UI");
            if (parent == null) parent = general.GetComponentInChildren<Canvas>(true)?.transform;
            if (parent == null) return;
            view = Instantiate(prefab, parent, false);
        }

        private void RefreshCatalog()
        {
            var bootstrap = FootballDatabaseBootstrap.Current;
            var catalog = bootstrap?.Session.ActiveCatalog;
            var state = bootstrap == null ? (FootballDatabaseLoadState?)null : bootstrap.State;
            if (ReferenceEquals(catalog, observedCatalog) && state == observedState) return;
            observedCatalog = catalog; observedState = state;
            Editions = catalog == null ? Array.Empty<HubEditionOption>() : catalog.CompetitionEditions.Select(edition =>
                new HubEditionOption(edition, catalog.Competitions.First(competition => competition.Id == edition.CompetitionId).Name)).ToArray();
            DefaultCareerDate = catalog?.Snapshot != null ? catalog.Snapshot.Date.ToDateTime()
                : Editions.Count > 0 ? Editions[0].FirstDate : DateTime.Today;
            Changed?.Invoke();
        }

        public void Navigate(HubPage page)
        {
            if (IsBusy) return;
            if (page == HubPage.Championship && season == null) page = HubPage.Championships;
            statusKey = null;
            statusDetails = null;
            Page = page;
            EventManager.Trigger(new CloseAllPanelsEvent());
            GameInput.SwitchToUI();
            if (page == HubPage.QuickMatch)
            {
                matchOrigin = HubPage.QuickMatch;
                SnapManager.Enable();
                EventManager.Trigger(new QuickMatchEvent());
            }
            else SnapManager.Disable();
            Changed?.Invoke();
        }

        public bool StartChampionship(string editionId, string userClubId)
        {
            if (!DatabaseReady || IsBusy) return false;
            CatalogMatchAdapter replacement = null;
            try
            {
                var source = FootballDatabaseBootstrap.Current;
                var next = CompetitionSession.Create(source.Session.ActiveCatalog, editionId,
                    "season-" + Guid.NewGuid().ToString("N"), userClubId);
                replacement = new CatalogMatchAdapter(next.Catalog, source.VisualProfiles, LoadBindings());
                foreach (var clubId in next.Edition.ParticipantClubIds)
                {
                    var team = replacement.Teams.First(item => item.ClubId == clubId);
                    if (!team.CanPlay)
                    {
                        statusKey = "session.cannotStartChampionship";
                        statusDetails = team.Name + ": " + team.Error;
                        Changed?.Invoke();
                        return false;
                    }
                }
                var json = GameSaveCodec.Championship(next, source.ActiveSourceJson);
                if (!saves.Write(ChampionshipSaveKey, json, out var saveError))
                { AddSaveWarning(saveError); return false; }
                seasonAdapter?.Dispose(); seasonAdapter = replacement; replacement = null;
                season = next; seasonDatabaseJson = source.ActiveSourceJson; HasChampionshipSave = true;
                Navigate(HubPage.Championship);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[FootballWorld] Championship could not start: " + exception.Message);
                statusKey = "session.cannotStartChampionship"; Changed?.Invoke(); return false;
            }
            finally { replacement?.Dispose(); }
        }

        public async Task PlayNextFixture()
        {
            if (IsBusy || season == null || season.NextFixture == null) return;
            busy = true; statusKey = null; Changed?.Invoke();
            try
            {
                var fixture = season.NextFixture;
                if (!seasonAdapter.TryCreateMatch(fixture.HomeClubId, fixture.AwayClubId, out var lease, out var error))
                    throw new InvalidOperationException(error);
                try { execution = season.BeginFixture(fixture.Id); }
                catch { lease.Dispose(); throw; }
                executionLease = lease; completedExecution = false; matchOrigin = HubPage.Championship;
                PersistSeason(); // Saves consumed execution IDs; a refresh restores this fixture as pending.
                var side = fixture.HomeClubId == season.ControlledClubId ? MatchCreateRequest.UserTeam.Home : MatchCreateRequest.UserTeam.Away;
                await FriendlyMatchSession.Current.StartPreparedMatch(lease, side);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[FootballWorld] Fixture could not start: " + exception.Message);
                var orphanedLease = executionLease;
                NotifyMatchUnloaded();
                if (orphanedLease != null && !ReferenceEquals(orphanedLease, FriendlyMatchSession.Current.ActiveMatch))
                    orphanedLease.Dispose();
                statusKey = "session.matchFailed";
            }
            finally { busy = false; Changed?.Invoke(); }
        }

        private void OnFinalWhistle(FinalWhistleEvent result)
        {
            if (result == null || execution == null || completedExecution || season == null || executionLease == null) return;
            if (!ReferenceEquals(result.HomeTeam, executionLease.Request.homeTeam) ||
                !ReferenceEquals(result.AwayTeam, executionLease.Request.awayTeam)) return;
            var accepted = season.CompleteFixture(new FixtureResult(execution.FixtureId, execution.ExecutionId,
                executionLease.HomeClubId, executionLease.AwayClubId, result.HomeGoals, result.AwayGoals));
            if (accepted == FixtureCompletion.Applied || accepted == FixtureCompletion.AlreadyApplied)
            {
                completedExecution = true;
                PersistSeason();
                Changed?.Invoke();
            }
        }

        public void NotifyQuickMatchStarting()
        {
            matchOrigin = HubPage.QuickMatch;
            execution = null; executionLease = null; completedExecution = false;
        }

        // The loader calls this after pending creation settles, before disposing
        // the lease. Both abandonment and failures leave the fixture replayable.
        public void NotifyMatchUnloaded()
        {
            if (execution != null && season != null && !completedExecution)
            {
                season.AbortFixture(execution.FixtureId, execution.ExecutionId);
                PersistSeason();
            }
            execution = null; executionLease = null; completedExecution = false;
            busy = false;
            Changed?.Invoke();
        }

        public void ReturnToMatchOrigin()
        {
            busy = false;
            Navigate(matchOrigin);
        }

        public bool SaveCareer(string name, string avatarId, int month, int year, string clubId)
        {
            if (!DatabaseReady || IsBusy) return false;
            try
            {
                var catalog = FootballDatabaseBootstrap.Current.Session.ActiveCatalog;
                var club = catalog.GetClub(clubId);
                var profile = new HubCareerProfile(name, avatarId, month, year, clubId, club.Name, catalog.DatabaseId, catalog.DatabaseRevision);
                if (!saves.Write(CareerSaveKey, GameSaveCodec.Career(profile), out var error))
                { AddSaveWarning(error); return false; }
                Career = profile; HasCareerSave = true; statusKey = "career.created";
                Changed?.Invoke(); return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[FootballWorld] Career profile rejected: " + exception.Message);
                statusKey = "career.invalid"; Changed?.Invoke(); return false;
            }
        }

        public void RetryDatabase() { statusKey = null; FriendlyMatchSession.Current.Retry(); RefreshCatalog(); }
        public void DismissSaveWarning() { saveWarnings.Clear(); Changed?.Invoke(); }
        public void SetLanguage(string language) => Settings.SetLanguage(language);
        public void SetDifficulty(AILevel difficulty) => Settings.SetDifficulty(difficulty);
        public void SetCamera(string cameraId) => Settings.SetCamera(cameraId);

        private void RestoreSaves()
        {
            HasChampionshipSave = PlayerPrefs.HasKey(ChampionshipSaveKey + ".active");
            HasCareerSave = PlayerPrefs.HasKey(CareerSaveKey + ".active");
            try
            {
                var json = saves.Read(ChampionshipSaveKey);
                if (json != null)
                {
                    var restored = GameSaveCodec.RestoreChampionship(json);
                    var adapter = new CatalogMatchAdapter(restored.Session.Catalog, restored.Profiles, LoadBindings());
                    if (adapter.Teams.Any(team => restored.Session.Edition.ParticipantClubIds.Contains(team.ClubId) && !team.CanPlay))
                    { adapter.Dispose(); throw new InvalidOperationException("Saved participant visuals are unavailable."); }
                    season = restored.Session; seasonDatabaseJson = restored.DatabaseJson; seasonAdapter = adapter;
                }
            }
            catch (Exception exception)
            { Debug.LogWarning("[FootballWorld] Championship save retained but unavailable: " + exception.Message); AddSaveWarning("championship_restore_failed"); }
            try
            {
                var json = saves.Read(CareerSaveKey);
                if (json != null) Career = GameSaveCodec.RestoreCareer(json);
            }
            catch (Exception exception)
            { Debug.LogWarning("[FootballWorld] Career save retained but unavailable: " + exception.Message); AddSaveWarning("career_restore_failed"); }
        }

        private void PersistSeason()
        {
            if (season == null) return;
            try
            {
                if (!saves.Write(ChampionshipSaveKey, GameSaveCodec.Championship(season, seasonDatabaseJson), out var error)) AddSaveWarning(error);
                else HasChampionshipSave = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[FootballWorld] Championship progress could not be saved: " + exception.Message);
                AddSaveWarning("save_failed");
            }
        }
        private void AddSaveWarning(string key)
        {
            var fullKey = "save." + key;
            if (!saveWarnings.Contains(fullKey)) saveWarnings.Add(fullKey);
            Changed?.Invoke();
        }
        private static LegacyMatchBindings LoadBindings()
            => Resources.Load<LegacyMatchBindings>(LegacyMatchBindings.ResourcePath) ?? throw new InvalidOperationException("Visual bindings are unavailable.");
        private void OnDestroy()
        {
            EventManager.UnSubscribe<MainMenuEvent>(OpenHome);
            EventManager.UnSubscribe<FinalWhistleEvent>(OnFinalWhistle);
            GameText.Changed -= OnPreferencesChanged;
            if (friendly != null) friendly.Changed -= OnFriendlyChanged;
            seasonAdapter?.Dispose();
            if (current == this) current = null;
        }
    }
}
