using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Bootstrap;
using FStudio.FootballWorld.DataContracts;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using UnityEngine;

namespace FStudio.FootballWorld.Infrastructure.GameModes
{
    public sealed partial class GameHubSession
    {
        private CareerSession careerSession;
        private CatalogMatchAdapter careerAdapter;
        private string careerDatabaseJson;
        private IReadOnlyList<VisualProfileData> careerVisualProfiles;
        private bool viewingCareerCompetition;
        private CompetitionSession DisplayedSeason => viewingCareerCompetition ? careerSession?.Competition : season;
        private CompetitionSession ActiveSeason => matchOrigin == HubPage.CareerOffice ? careerSession?.Competition : season;
        public bool IsCareerCalendar => viewingCareerCompetition;
        public bool IsCareerMatch => matchOrigin == HubPage.CareerOffice;
        public bool HasDailyCareer => careerSession != null;
        public CareerSession CareerProgress => careerSession;
        public bool CareerCanPlay => careerSession != null && careerSession.IsMatchDay && careerSession.Competition.ActiveExecution == null && !IsBusy;

        public void OpenCareer() => Navigate(HasDailyCareer ? HubPage.CareerOffice : HubPage.Career);
        public void OpenCareerCalendar()
        {
            if (!HasDailyCareer || IsBusy) return;
            viewingCareerCompetition = true;
            Navigate(HubPage.Championship);
        }
        public void OpenCareerDraft() => Navigate(HubPage.Career);
        public void ContinueCareer() => Navigate(HubPage.CareerOffice);

        private bool CreateDailyCareer(string name, string avatarId, int month, int year, string clubId)
        {
            var source = FootballDatabaseBootstrap.Current;
            var catalog = source.Session.ActiveCatalog;
            var club = catalog.GetClub(clubId);
            var profile = new HubCareerProfile(name, avatarId, month, year, clubId, club.Name, catalog.DatabaseId, catalog.DatabaseRevision);
            var start = new GameDate(year, month, 1);
            var edition = catalog.CompetitionEditions.Where(item => item.ParticipantClubIds.Contains(clubId)
                && item.StartDate.Year == year && item.StartDate.Month == month)
                .OrderBy(item => item.StartDate).ThenBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault();
            if (edition == null) throw new InvalidOperationException("No competition covers this club and starting month.");
            var next = CareerSession.Create(catalog, edition.Id, "career-" + Guid.NewGuid().ToString("N"), clubId, start);
            CatalogMatchAdapter replacement = null;
            try
            {
                replacement = CreateCareerAdapter(next, source.VisualProfiles);
                if (replacement.Teams.Any(team => edition.ParticipantClubIds.Contains(team.ClubId) && !team.CanPlay))
                    throw new InvalidOperationException("A participating club cannot field a team.");
                if (!saves.Write(CareerSaveKey, GameSaveCodec.DailyCareer(profile, next, source.ActiveSourceJson), out var error))
                { AddSaveWarning(error); return false; }
                careerAdapter?.Dispose(); careerAdapter = replacement; replacement = null;
                careerSession = next; careerDatabaseJson = source.ActiveSourceJson;
                careerVisualProfiles = source.VisualProfiles;
                RememberCareerAdapter();
                Career = profile; HasCareerSave = true;
                Navigate(HubPage.CareerOffice);
                return true;
            }
            finally { replacement?.Dispose(); }
        }

        private void RestoreDailyCareer(string json)
        {
            var restored = GameSaveCodec.RestoreDailyCareer(json);
            Career = restored.Profile;
            if (restored.Session == null) return; // Preserve old profile-only saves until an explicit new career.
            var adapter = CreateCareerAdapter(restored.Session, restored.Profiles);
            if (adapter.Teams.Any(team => restored.Session.Competition.Edition.ParticipantClubIds.Contains(team.ClubId) && !team.CanPlay))
            { adapter.Dispose(); throw new InvalidOperationException("Saved career participant visuals are unavailable."); }
            careerSession = restored.Session; careerDatabaseJson = restored.DatabaseJson; careerAdapter = adapter;
            careerVisualProfiles = restored.Profiles;
            RememberCareerAdapter();
        }

        public void AdvanceCareerDay() => ChangeCareer(() => careerSession.AdvanceDay());
        public void AdvanceCareerToFixture() => ChangeCareer(() => careerSession.AdvanceToNextFixture());
        public void SetCareerTraining(int index) => ChangeCareer(() => careerSession.SetTraining((CareerTraining)index));
        public void SimulateCareerFixture() => ChangeCareer(() => careerSession.SimulateNextFixture());
        public System.Threading.Tasks.Task PlayCareerFixture()
        {
            viewingCareerCompetition = true;
            return PlayNextFixture();
        }

        private void ChangeCareer(Action command)
        {
            if (careerSession == null || IsBusy) return;
            statusKey = null; statusDetails = null;
            try { command(); PersistCareer(); }
            catch (Exception exception)
            {
                Debug.LogWarning("[FootballWorld] Career action rejected: " + exception.Message);
                statusKey = "career.actionUnavailable";
            }
            Changed?.Invoke();
        }

        public void SimulateChampionship()
        {
            if (IsBusy || DisplayedSeason == null || DisplayedSeason.IsComplete) return;
            if (viewingCareerCompetition) { SimulateCareerFixture(); return; }
            try
            {
                if (season.NextFixture != null) season.SimulateFixture(season.NextFixture.Id);
                else season.SimulateNextRound();
                PersistSeason();
            }
            catch (Exception exception)
            { Debug.LogWarning("[FootballWorld] Simulation rejected: " + exception.Message); statusKey = "career.actionUnavailable"; }
            Changed?.Invoke();
        }

        private void PersistActiveSeason()
        {
            if (matchOrigin == HubPage.CareerOffice)
            {
                careerSession?.ReconcileResults();
                PersistCareer();
            }
            else PersistSeason();
        }

        private void PersistCareer()
        {
            if (careerSession == null) return;
            try
            {
                if (!saves.Write(CareerSaveKey, GameSaveCodec.DailyCareer(Career, careerSession, careerDatabaseJson), out var error)) AddSaveWarning(error);
                else HasCareerSave = true;
            }
            catch (Exception exception)
            { Debug.LogWarning("[FootballWorld] Career progress could not be saved: " + exception.Message); AddSaveWarning("save_failed"); }
        }

        public string CareerMoney(long amount)
            => (careerSession?.Currency ?? "BRL") + " " + amount.ToString("N0", CultureInfo.GetCultureInfo(Settings.Language == "en" ? "en-US" : "pt-BR"));

        public string CareerLedgerText()
        {
            if (careerSession == null) return string.Empty;
            return string.Join("\n", careerSession.Ledger.Reverse().Take(40).Select(item =>
                GameText.FormatDate(item.Date.ToDateTime()) + " · " + GameText.Get("ledger." + item.EventKey) + " · " + CareerMoney(item.Amount)));
        }

        public string CareerNewsText()
        {
            if (careerSession == null) return string.Empty;
            return string.Join("\n\n", careerSession.News.Reverse().Take(40).Select(item =>
            {
                var playerName = item.PlayerId == null ? string.Empty : careerSession.Competition.Catalog.GetPlayer(item.PlayerId).DisplayName;
                var body = GameText.Get("news." + item.EventKey, Career.ClubName, item.Condition, item.Preparation, CareerMoney(item.Amount), playerName);
                if (item.FixtureId != null)
                {
                    var result = careerSession.Competition.GetResult(item.FixtureId);
                    if (result != null)
                    {
                        var catalog = careerSession.Competition.Catalog;
                        body = catalog.GetClub(result.HomeClubId).Name + " " + result.HomeGoals + " × " + result.AwayGoals
                            + " " + catalog.GetClub(result.AwayClubId).Name;
                        if (result.HomePenalties.HasValue) body += " · " + GameText.Get("hub.penalties", result.HomePenalties, result.AwayPenalties);
                        body += "\n" + GameText.Get(result.IsSimulated ? "hub.simulated" : "hub.played");
                    }
                }
                return GameText.FormatDate(item.Date.ToDateTime()) + " · " + GameText.Get("outlet." + item.OutletId) + "\n" + body;
            }));
        }

        // Only the lease's temporary players are adjusted; the authored database remains immutable.
        private void ApplyCareerPreparation(CatalogMatchLease lease)
        {
            if (!viewingCareerCompetition || careerSession == null) return;
            var team = lease.HomeClubId == careerSession.Competition.ControlledClubId ? lease.Request.homeTeam : lease.Request.awayTeam;
            var factor = careerSession.MatchPerformancePercent / 100f;
            foreach (var player in team.Players)
            {
                player.acceleration = Mathf.Clamp(Mathf.RoundToInt(player.acceleration * factor), 1, 100);
                player.topSpeed = Mathf.Clamp(Mathf.RoundToInt(player.topSpeed * factor), 1, 100);
                player.passing = Mathf.Clamp(Mathf.RoundToInt(player.passing * factor), 1, 100);
                player.shooting = Mathf.Clamp(Mathf.RoundToInt(player.shooting * factor), 1, 100);
                player.reaction = Mathf.Clamp(Mathf.RoundToInt(player.reaction * factor), 1, 100);
            }
        }
    }
}
