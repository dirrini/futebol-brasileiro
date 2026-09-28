using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FStudio.Events;
using FStudio.FootballWorld.Bootstrap;
using FStudio.FootballWorld.Domain;
using FStudio.MatchEngine;
using FStudio.UI.Events;
using UnityEngine;

namespace FStudio.FootballWorld.Infrastructure.LegacyMatch
{
    public enum FriendlyMatchState { Loading, Ready, Failed }

    // Unity composition boundary. The catalog stays immutable; previews and each
    // match own separate legacy objects. This host survives destruction of the UI.
    public sealed class FriendlyMatchSession : MonoBehaviour
    {
        private static FriendlyMatchSession current;
        private static readonly IReadOnlyList<CatalogTeamOption> EmptyTeams =
            Array.AsReadOnly(new CatalogTeamOption[0]);
        private CatalogMatchAdapter adapter;
        private DatabaseCatalog observedCatalog;
        private FootballDatabaseLoadState? observedState;
        private object observedErrors;
        private bool refreshRequested = true;
        private bool isStarting;
        private string sourceError;
        private string launchError;

        public static FriendlyMatchSession Current
        {
            get
            {
                if (current == null)
                {
                    current = FindObjectOfType<FriendlyMatchSession>();
                    if (current == null)
                    {
                        var host = new GameObject("FootballWorld Friendly Match Session");
                        current = host.AddComponent<FriendlyMatchSession>();
                    }
                }
                return current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { current = null; }

        public event Action Changed;
        public FriendlyMatchState State { get; private set; } = FriendlyMatchState.Loading;
        public IReadOnlyList<CatalogTeamOption> Teams => adapter == null ? EmptyTeams : adapter.Teams;
        public string SelectedHomeClubId { get; private set; }
        public string SelectedAwayClubId { get; private set; }
        public CatalogMatchLease ActiveMatch { get; private set; }
        public bool CanPlay => State == FriendlyMatchState.Ready && !isStarting &&
            ActiveMatch == null && SelectionError() == null;

        public string StatusMessage
        {
            get
            {
                if (isStarting) return "Preparing match...";
                if (ActiveMatch != null) return "Match in progress.";
                if (State == FriendlyMatchState.Loading) return "Loading database...";
                if (State == FriendlyMatchState.Failed) return sourceError;
                var error = SelectionError();
                if (error != null) return error;
                if (!string.IsNullOrEmpty(launchError)) return launchError;
                var warnings = new[] { Find(SelectedHomeClubId)?.Warning, Find(SelectedAwayClubId)?.Warning };
                return string.Join("\n", warnings.Where(value => !string.IsNullOrEmpty(value)).Distinct());
            }
        }

        private void Awake()
        {
            if (current != null && current != this) { Destroy(gameObject); return; }
            current = this;
            DontDestroyOnLoad(gameObject);
            Refresh();
        }

        private void Update() { Refresh(); }

        private CatalogTeamOption Find(string clubId) => Teams.FirstOrDefault(team => team.ClubId == clubId);

        private string SelectionError()
        {
            if (Teams.Count < 2) return "The database needs at least two clubs to play a friendly match.";
            var home = Find(SelectedHomeClubId);
            var away = Find(SelectedAwayClubId);
            if (home == null || away == null) return "Select both teams.";
            if (home.ClubId == away.ClubId) return "Choose two different clubs.";
            if (!home.CanPlay) return home.Name + ": " + home.Error;
            if (!away.CanPlay) return away.Name + ": " + away.Error;
            return null;
        }

        private string RetainSelection(string previous, string other)
        {
            if (Find(previous) != null) return previous;
            return Teams.FirstOrDefault(team => team.CanPlay && team.ClubId != other)?.ClubId ??
                Teams.FirstOrDefault(team => team.ClubId != other)?.ClubId ?? Teams.FirstOrDefault()?.ClubId;
        }

        private void Refresh()
        {
            // A reload must not replace objects used by the preparation screen or match.
            if (isStarting || ActiveMatch != null) return;
            var source = FootballDatabaseBootstrap.Current;
            var catalog = source == null ? null : source.Session.ActiveCatalog;
            var state = source == null ? (FootballDatabaseLoadState?)null : source.State;
            var errors = source == null ? null : source.Errors;
            if (!refreshRequested && ReferenceEquals(observedCatalog, catalog) &&
                observedState == state && ReferenceEquals(observedErrors, errors)) return;
            refreshRequested = false;
            observedCatalog = catalog;
            observedState = state;
            observedErrors = errors;

            if (source == null || state == FootballDatabaseLoadState.Failed)
            {
                State = FriendlyMatchState.Failed;
                sourceError = "Could not load the database. Check the database file and try again.";
            }
            else if (state == FootballDatabaseLoadState.Loading)
            {
                State = FriendlyMatchState.Loading;
            }
            else
            {
                try
                {
                    if (adapter == null || !ReferenceEquals(adapter.Catalog, catalog))
                    {
                        var bindings = Resources.Load<LegacyMatchBindings>("FootballWorld/LegacyMatchBindings");
                        if (bindings == null) throw new InvalidOperationException("LegacyMatchBindings resource is missing.");
                        var replacement = new CatalogMatchAdapter(catalog, source.VisualProfiles, bindings);
                        var previous = adapter;
                        adapter = replacement;
                        SelectedHomeClubId = RetainSelection(SelectedHomeClubId, SelectedAwayClubId);
                        SelectedAwayClubId = RetainSelection(SelectedAwayClubId, SelectedHomeClubId);
                        previous?.Dispose();
                        Debug.Log("[FootballWorld] Friendly selection uses database " + catalog.DatabaseId +
                            " revision " + catalog.DatabaseRevision + ": " + Teams.Count + " clubs.");
                    }
                    State = FriendlyMatchState.Ready;
                    sourceError = null;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    State = FriendlyMatchState.Failed;
                    sourceError = "Could not prepare the teams. Check the visual configuration and try again.";
                }
            }
            Changed?.Invoke();
        }

        public void Select(bool away, string clubId)
        {
            if (State != FriendlyMatchState.Ready || isStarting || ActiveMatch != null || Find(clubId) == null) return;
            if (away) SelectedAwayClubId = clubId;
            else SelectedHomeClubId = clubId;
            launchError = null;
            Changed?.Invoke();
        }

        public void Retry()
        {
            if (isStarting || ActiveMatch != null) return;
            launchError = null;
            refreshRequested = true;
            FootballDatabaseBootstrap.Current?.Reload();
            Refresh();
        }

        public async Task StartMatch()
        {
            Refresh();
            if (!CanPlay) return;
            if (!adapter.TryCreateMatch(SelectedHomeClubId, SelectedAwayClubId, out var lease, out var error))
            {
                launchError = error;
                Changed?.Invoke();
                return;
            }
            ActiveMatch = lease;
            isStarting = true;
            launchError = null;
            Changed?.Invoke();
            try
            {
                Debug.Log("[FootballWorld] Preparing friendly " + lease.HomeClubId + " vs " + lease.AwayClubId +
                    " from " + lease.Catalog.DatabaseId + " revision " + lease.Catalog.DatabaseRevision +
                    "; " + lease.Players.Count + " stable player identities mapped to match IDs.");
                await MatchEngineLoader.CreateMatch(lease.Request);
            }
            catch (Exception exception)
            {
                await ReportMatchFailure(exception);
            }
            finally
            {
                isStarting = false;
                Changed?.Invoke();
            }
        }

        public async Task ReportMatchFailure(Exception exception)
        {
            Debug.LogException(exception);
            launchError = "Could not open the match. Please try again.";
            try
            {
                if (MatchEngineLoader.Current != null) await MatchEngineLoader.Current.UnloadMatch();
                else ReleaseActiveMatch();
                EventManager.Trigger(new CloseAllPanelsEvent());
                EventManager.Trigger(new MainMenuEvent());
            }
            catch (Exception recoveryError)
            {
                Debug.LogException(recoveryError);
                State = FriendlyMatchState.Failed;
                sourceError = "Could not return to team selection. Reload the game to recover.";
            }
            Changed?.Invoke();
        }

        // Called only after the match UI/engine have released their consumers.
        public static void ReleaseActiveMatch()
        {
            if (current == null) return;
            var lease = current.ActiveMatch;
            current.ActiveMatch = null;
            lease?.Dispose();
            current.isStarting = false;
            current.refreshRequested = true;
            current.Refresh();
            current.Changed?.Invoke();
        }

        private void OnDestroy()
        {
            ActiveMatch?.Dispose();
            adapter?.Dispose();
            if (current == this) current = null;
        }
    }
}
