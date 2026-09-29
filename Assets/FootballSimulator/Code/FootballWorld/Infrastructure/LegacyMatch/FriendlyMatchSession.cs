using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FStudio.Events;
using FStudio.FootballWorld.Bootstrap;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.GameModes;
using FStudio.MatchEngine;
using FStudio.UI.Events;
using UnityEngine;
using Shared.Responses;

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
        private string observedLanguage;

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
        public IReadOnlyList<CatalogCountryOption> Countries => CatalogCountryFilter.Countries(adapter?.Catalog, Teams);
        public string SelectedHomeCountryCode { get; private set; }
        public string SelectedAwayCountryCode { get; private set; }
        public IReadOnlyList<CatalogTeamOption> HomeTeams => CatalogCountryFilter.Teams(Teams, SelectedHomeCountryCode);
        public IReadOnlyList<CatalogTeamOption> AwayTeams => CatalogCountryFilter.Teams(Teams, SelectedAwayCountryCode);
        public string SelectedHomeClubId { get; private set; }
        public string SelectedAwayClubId { get; private set; }
        public CatalogMatchLease ActiveMatch { get; private set; }
        public MatchCreateRequest.UserTeam? LockedUserSide { get; private set; }
        public bool CanPlay => State == FriendlyMatchState.Ready && !isStarting &&
            ActiveMatch == null && SelectionError() == null;

        public string StatusMessage
        {
            get
            {
                if (isStarting) return GameText.Get("hub.busy");
                if (ActiveMatch != null) return GameText.Get("match.inProgress");
                if (State == FriendlyMatchState.Loading) return GameText.Get("hub.loading");
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
            if (Teams.Count < 2) return GameText.Get("match.needsTwoClubs");
            var home = Find(SelectedHomeClubId);
            var away = Find(SelectedAwayClubId);
            if (home == null || away == null) return GameText.Get("match.selectTeams");
            if (home.ClubId == away.ClubId) return GameText.Get("match.differentClubs");
            if (!home.CanPlay) return home.Name + ": " + home.Error;
            if (!away.CanPlay) return away.Name + ": " + away.Error;
            return null;
        }

        private void Refresh()
        {
            // A reload must not replace objects used by the preparation screen or match.
            if (isStarting || ActiveMatch != null) return;
            var source = FootballDatabaseBootstrap.Current;
            var catalog = source == null ? null : source.Session.ActiveCatalog;
            var state = source == null ? (FootballDatabaseLoadState?)null : source.State;
            var errors = source == null ? null : source.Errors;
            var language = GameUserSettings.Current.Language;
            var languageChanged = observedLanguage != language;
            if (!refreshRequested && ReferenceEquals(observedCatalog, catalog) &&
                observedState == state && ReferenceEquals(observedErrors, errors) && !languageChanged) return;
            refreshRequested = false;
            observedCatalog = catalog;
            observedState = state;
            observedErrors = errors;
            observedLanguage = language;

            if (source == null || state == FootballDatabaseLoadState.Failed)
            {
                State = FriendlyMatchState.Failed;
                sourceError = GameText.Get("session.databaseFailed");
            }
            else if (state == FootballDatabaseLoadState.Loading)
            {
                State = FriendlyMatchState.Loading;
            }
            else
            {
                try
                {
                    if (adapter == null || !ReferenceEquals(adapter.Catalog, catalog) || languageChanged)
                    {
                        var bindings = Resources.Load<LegacyMatchBindings>("FootballWorld/LegacyMatchBindings");
                        if (bindings == null) throw new InvalidOperationException("LegacyMatchBindings resource is missing.");
                        var replacement = new CatalogMatchAdapter(catalog, source.VisualProfiles, bindings);
                        var previous = adapter;
                        adapter = replacement;
                        var countries = Countries;
                        SelectedHomeCountryCode = CatalogCountryFilter.RetainCountry(countries, SelectedHomeCountryCode, Teams, SelectedHomeClubId);
                        SelectedAwayCountryCode = CatalogCountryFilter.RetainCountry(countries, SelectedAwayCountryCode, Teams, SelectedAwayClubId);
                        SelectedHomeClubId = CatalogCountryFilter.RetainClub(HomeTeams, SelectedHomeClubId, SelectedAwayClubId);
                        SelectedAwayClubId = CatalogCountryFilter.RetainClub(AwayTeams, SelectedAwayClubId, SelectedHomeClubId);
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
                    sourceError = GameText.Get("session.visualsFailed");
                }
            }
            Changed?.Invoke();
        }

        public void Select(bool away, string clubId)
        {
            if (State != FriendlyMatchState.Ready || isStarting || ActiveMatch != null || Find(clubId) == null) return;
            if (Find(clubId).CountryCode != (away ? SelectedAwayCountryCode : SelectedHomeCountryCode)) return;
            if (away) SelectedAwayClubId = clubId;
            else SelectedHomeClubId = clubId;
            launchError = null;
            Changed?.Invoke();
        }

        public void SelectCountry(bool away, string countryCode)
        {
            if (State != FriendlyMatchState.Ready || isStarting || ActiveMatch != null ||
                !Countries.Any(country => country.Code == countryCode)) return;
            if (away)
            {
                SelectedAwayCountryCode = countryCode;
                SelectedAwayClubId = CatalogCountryFilter.RetainClub(AwayTeams, SelectedAwayClubId, SelectedHomeClubId);
            }
            else
            {
                SelectedHomeCountryCode = countryCode;
                SelectedHomeClubId = CatalogCountryFilter.RetainClub(HomeTeams, SelectedHomeClubId, SelectedAwayClubId);
            }
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
            GameHubSession.Current.NotifyQuickMatchStarting();
            await StartLease(lease, null);
        }

        public Task StartPreparedMatch(CatalogMatchLease lease, MatchCreateRequest.UserTeam side)
        {
            if (lease == null) throw new ArgumentNullException(nameof(lease));
            if (isStarting || ActiveMatch != null) throw new InvalidOperationException("A match is already active.");
            return StartLease(lease, side);
        }

        private async Task StartLease(CatalogMatchLease lease, MatchCreateRequest.UserTeam? side)
        {
            ActiveMatch = lease;
            LockedUserSide = side;
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
            launchError = GameText.Get("session.matchFailed");
            try
            {
                if (MatchEngineLoader.Current != null) await MatchEngineLoader.Current.UnloadMatch();
                else { GameHubSession.Current.NotifyMatchUnloaded(); ReleaseActiveMatch(); }
                EventManager.Trigger(new CloseAllPanelsEvent());
                GameHubSession.Current.ReturnToMatchOrigin();
            }
            catch (Exception recoveryError)
            {
                Debug.LogException(recoveryError);
                State = FriendlyMatchState.Failed;
                sourceError = GameText.Get("session.recoveryFailed");
            }
            Changed?.Invoke();
        }

        // Called only after the match UI/engine have released their consumers.
        public static void ReleaseActiveMatch()
        {
            if (current == null) return;
            var lease = current.ActiveMatch;
            current.ActiveMatch = null;
            current.LockedUserSide = null;
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
