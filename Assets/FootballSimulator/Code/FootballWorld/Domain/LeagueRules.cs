using System;

namespace FStudio.FootballWorld.Domain
{
    // The implemented rule set is round-robin v1. Sporting tie-break order is
    // points, wins, goal difference, goals for; a complete tie stays a shared rank.
    public sealed class LeagueRules
    {
        public int Legs { get; }
        public int WinPoints { get; }
        public int DrawPoints { get; }
        public int LossPoints { get; }
        public LeagueRules(int legs, int winPoints, int drawPoints, int lossPoints)
        {
            Legs = DomainValidation.InRange(legs, 1, 2, nameof(legs));
            WinPoints = DomainValidation.InRange(winPoints, 0, 100, nameof(winPoints));
            DrawPoints = DomainValidation.InRange(drawPoints, 0, 100, nameof(drawPoints));
            LossPoints = DomainValidation.InRange(lossPoints, 0, 100, nameof(lossPoints));
            if (WinPoints <= DrawPoints || DrawPoints < LossPoints)
                throw new ArgumentException("Points must satisfy win > draw >= loss.");
        }
        public int RoundCount(int clubCount)
        {
            DomainValidation.InRange(clubCount, 2, 64, nameof(clubCount));
            return (clubCount % 2 == 0 ? clubCount - 1 : clubCount) * Legs;
        }
    }
}
