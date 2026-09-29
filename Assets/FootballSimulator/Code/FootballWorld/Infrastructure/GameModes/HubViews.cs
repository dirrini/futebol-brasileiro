using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Infrastructure.GameModes
{
    public enum HubPage { Home, QuickMatch, Championships, Championship, Career, Options }

    public sealed class HubEditionOption
    {
        public string Id { get; }
        public string Name { get; }
        public string CompetitionName { get; }
        public IReadOnlyList<string> ParticipantClubIds { get; }
        public DateTime FirstDate { get; }
        public DateTime LastDate { get; }
        internal HubEditionOption(CompetitionEditionDefinition edition, string competitionName)
        {
            Id = edition.Id; Name = edition.Name; CompetitionName = competitionName;
            ParticipantClubIds = edition.ParticipantClubIds;
            FirstDate = edition.StartDate.ToDateTime(); LastDate = edition.EndDate.ToDateTime();
        }
    }

    public sealed class HubFixtureView
    {
        public string Id { get; }
        public int Round { get; }
        public DateTime Date { get; }
        public string HomeName { get; }
        public string AwayName { get; }
        public int? HomeGoals { get; }
        public int? AwayGoals { get; }
        public bool IsCompleted => HomeGoals.HasValue;
        public bool IsUserFixture { get; }
        public bool IsSimulated { get; }
        internal HubFixtureView(FixtureDefinition fixture, FixtureResult result, CompetitionSession season)
        {
            Id = fixture.Id; Round = fixture.Round; Date = fixture.Date.ToDateTime();
            HomeName = season.Catalog.GetClub(fixture.HomeClubId).Name;
            AwayName = season.Catalog.GetClub(fixture.AwayClubId).Name;
            HomeGoals = result?.HomeGoals; AwayGoals = result?.AwayGoals;
            IsUserFixture = fixture.HomeClubId == season.ControlledClubId || fixture.AwayClubId == season.ControlledClubId;
            IsSimulated = result != null && result.IsSimulated;
        }
    }

    public sealed class HubStandingView
    {
        public string ClubId { get; }
        public string ClubName { get; }
        public int Rank { get; }
        public int Played { get; }
        public int Won { get; }
        public int Drawn { get; }
        public int Lost { get; }
        public int GoalsFor { get; }
        public int GoalsAgainst { get; }
        public int GoalDifference { get; }
        public int Points { get; }
        internal HubStandingView(StandingRow row, DatabaseCatalog catalog)
        {
            ClubId = row.ClubId; ClubName = catalog.GetClub(row.ClubId).Name; Rank = row.Rank;
            Played = row.Played; Won = row.Wins; Drawn = row.Draws; Lost = row.Losses;
            GoalsFor = row.GoalsFor; GoalsAgainst = row.GoalsAgainst;
            GoalDifference = row.GoalDifference; Points = row.Points;
        }
    }

    public sealed class HubChampionshipView
    {
        public string Name { get; }
        public string EditionName { get; }
        public string UserClubId { get; }
        public string UserClubName { get; }
        public string DatabaseId { get; }
        public int DatabaseRevision { get; }
        public bool IsComplete { get; }
        public bool CanPlayNext { get; }
        public HubFixtureView NextFixture { get; }
        public IReadOnlyList<HubFixtureView> Fixtures { get; }
        public IReadOnlyList<HubStandingView> Standings { get; }
        internal HubChampionshipView(CompetitionSession season, bool busy)
        {
            Name = season.Catalog.Competitions.First(item => item.Id == season.Edition.CompetitionId).Name;
            EditionName = season.Edition.Name; UserClubId = season.ControlledClubId;
            UserClubName = season.Catalog.GetClub(UserClubId).Name;
            DatabaseId = season.Catalog.DatabaseId; DatabaseRevision = season.Catalog.DatabaseRevision;
            IsComplete = season.IsComplete; CanPlayNext = !busy && season.NextFixture != null && season.ActiveExecution == null;
            var results = season.Results.ToDictionary(result => result.FixtureId, StringComparer.Ordinal);
            Fixtures = season.Fixtures.Select(fixture => new HubFixtureView(fixture,
                results.TryGetValue(fixture.Id, out var result) ? result : null, season)).ToArray();
            NextFixture = season.NextFixture == null ? null : Fixtures.First(fixture => fixture.Id == season.NextFixture.Id);
            Standings = season.Standings.Select(row => new HubStandingView(row, season.Catalog)).ToArray();
        }
    }

    public sealed class HubCareerProfile
    {
        public string CoachName { get; }
        public string AvatarId { get; }
        public int StartMonth { get; }
        public int StartYear { get; }
        public string ClubId { get; }
        public string ClubName { get; }
        public string DatabaseId { get; }
        public int DatabaseRevision { get; }
        public HubCareerProfile(string coachName, string avatarId, int startMonth, int startYear,
            string clubId, string clubName, string databaseId, int databaseRevision)
        {
            if (string.IsNullOrWhiteSpace(coachName) || new System.Globalization.StringInfo(coachName).LengthInTextElements > 100)
                throw new ArgumentException("Invalid coach name.");
            if (avatarId != "coach-1" && avatarId != "coach-2" && avatarId != "coach-3")
                throw new ArgumentException("Unsupported coach avatar.");
            if (startMonth < 1 || startMonth > 12 || startYear < 1 || startYear > 9999)
                throw new ArgumentException("Unsupported career start date.");
            if (string.IsNullOrWhiteSpace(clubId) || string.IsNullOrWhiteSpace(clubName) ||
                string.IsNullOrWhiteSpace(databaseId) || databaseRevision < 1)
                throw new ArgumentException("Invalid career database reference.");
            CoachName = coachName.Trim(); AvatarId = avatarId; StartMonth = startMonth; StartYear = startYear;
            ClubId = clubId; ClubName = clubName; DatabaseId = databaseId; DatabaseRevision = databaseRevision;
        }
    }
}
