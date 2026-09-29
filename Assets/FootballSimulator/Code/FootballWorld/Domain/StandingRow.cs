namespace FStudio.FootballWorld.Domain
{
    public sealed class StandingRow
    {
        public string ClubId { get; }
        public int Rank { get; }
        public int Played => Wins + Draws + Losses;
        public int Wins { get; }
        public int Draws { get; }
        public int Losses { get; }
        public int GoalsFor { get; }
        public int GoalsAgainst { get; }
        public int GoalDifference => GoalsFor - GoalsAgainst;
        public int Points { get; }
        public int YellowCards { get; }
        public int RedCards { get; }
        public StandingRow(string clubId, int rank, int wins, int draws, int losses,
            int goalsFor, int goalsAgainst, int points, int yellowCards = 0, int redCards = 0)
        {
            ClubId = DomainValidation.Id(clubId, nameof(clubId));
            Rank = DomainValidation.InRange(rank, 1, 64, nameof(rank));
            Wins = DomainValidation.InRange(wins, 0, 126, nameof(wins));
            Draws = DomainValidation.InRange(draws, 0, 126, nameof(draws));
            Losses = DomainValidation.InRange(losses, 0, 126, nameof(losses));
            GoalsFor = DomainValidation.InRange(goalsFor, 0, 126 * 999, nameof(goalsFor));
            GoalsAgainst = DomainValidation.InRange(goalsAgainst, 0, 126 * 999, nameof(goalsAgainst));
            Points = DomainValidation.InRange(points, 0, 12600, nameof(points));
            YellowCards = DomainValidation.InRange(yellowCards, 0, 126 * 99, nameof(yellowCards));
            RedCards = DomainValidation.InRange(redCards, 0, 126 * 22, nameof(redCards));
        }
    }
}
