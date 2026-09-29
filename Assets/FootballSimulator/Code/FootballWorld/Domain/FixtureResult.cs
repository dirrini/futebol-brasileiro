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
        public int HomeYellowCards { get; }
        public int AwayYellowCards { get; }
        public int HomeRedCards { get; }
        public int AwayRedCards { get; }
        public int? HomePenalties { get; }
        public int? AwayPenalties { get; }
        public bool HasSimulatedSupplement { get; }
        public FixtureResult(string fixtureId, string executionId, string homeClubId, string awayClubId,
            int homeGoals, int awayGoals, bool isSimulated = false, int homeYellowCards = 0, int awayYellowCards = 0,
            int homeRedCards = 0, int awayRedCards = 0, int? homePenalties = null, int? awayPenalties = null,
            bool hasSimulatedSupplement = false)
        {
            FixtureId = DomainValidation.Id(fixtureId, nameof(fixtureId));
            ExecutionId = DomainValidation.Id(executionId, nameof(executionId));
            HomeClubId = DomainValidation.Id(homeClubId, nameof(homeClubId));
            AwayClubId = DomainValidation.Id(awayClubId, nameof(awayClubId));
            HomeGoals = DomainValidation.InRange(homeGoals, 0, 999, nameof(homeGoals));
            AwayGoals = DomainValidation.InRange(awayGoals, 0, 999, nameof(awayGoals));
            IsSimulated = isSimulated;
            HomeYellowCards = DomainValidation.InRange(homeYellowCards, 0, 99, nameof(homeYellowCards));
            AwayYellowCards = DomainValidation.InRange(awayYellowCards, 0, 99, nameof(awayYellowCards));
            HomeRedCards = DomainValidation.InRange(homeRedCards, 0, 22, nameof(homeRedCards));
            AwayRedCards = DomainValidation.InRange(awayRedCards, 0, 22, nameof(awayRedCards));
            if (homePenalties.HasValue != awayPenalties.HasValue || (homePenalties.HasValue && homePenalties == awayPenalties))
                throw new System.ArgumentException("A shootout requires two different scores.");
            HomePenalties = homePenalties.HasValue ? DomainValidation.InRange(homePenalties.Value, 0, 999, nameof(homePenalties)) : (int?)null;
            AwayPenalties = awayPenalties.HasValue ? DomainValidation.InRange(awayPenalties.Value, 0, 999, nameof(awayPenalties)) : (int?)null;
            HasSimulatedSupplement = hasSimulatedSupplement;
        }
        public bool SameOutcome(FixtureResult other) => other != null && FixtureId == other.FixtureId &&
            ExecutionId == other.ExecutionId && HomeClubId == other.HomeClubId && AwayClubId == other.AwayClubId &&
            HomeGoals == other.HomeGoals && AwayGoals == other.AwayGoals && IsSimulated == other.IsSimulated &&
            HomeYellowCards == other.HomeYellowCards && AwayYellowCards == other.AwayYellowCards &&
            HomeRedCards == other.HomeRedCards && AwayRedCards == other.AwayRedCards &&
            HomePenalties == other.HomePenalties && AwayPenalties == other.AwayPenalties &&
            HasSimulatedSupplement == other.HasSimulatedSupplement;
    }
}
