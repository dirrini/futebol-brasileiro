namespace FStudio.FootballWorld.Domain
{
    public sealed class FixtureResult
    {
        public string FixtureId { get; }
        public string ExecutionId { get; }
        public string HomeClubId { get; }
        public string AwayClubId { get; }
        public int HomeGoals { get; }
        public int AwayGoals { get; }
        public bool IsSimulated { get; }
        public FixtureResult(string fixtureId, string executionId, string homeClubId, string awayClubId,
            int homeGoals, int awayGoals, bool isSimulated = false)
        {
            FixtureId = DomainValidation.Id(fixtureId, nameof(fixtureId));
            ExecutionId = DomainValidation.Id(executionId, nameof(executionId));
            HomeClubId = DomainValidation.Id(homeClubId, nameof(homeClubId));
            AwayClubId = DomainValidation.Id(awayClubId, nameof(awayClubId));
            HomeGoals = DomainValidation.InRange(homeGoals, 0, 999, nameof(homeGoals));
            AwayGoals = DomainValidation.InRange(awayGoals, 0, 999, nameof(awayGoals));
            IsSimulated = isSimulated;
        }
        public bool SameOutcome(FixtureResult other) => other != null && FixtureId == other.FixtureId &&
            ExecutionId == other.ExecutionId && HomeClubId == other.HomeClubId && AwayClubId == other.AwayClubId &&
            HomeGoals == other.HomeGoals && AwayGoals == other.AwayGoals && IsSimulated == other.IsSimulated;
    }
}
