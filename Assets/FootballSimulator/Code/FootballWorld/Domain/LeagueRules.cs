using System;

namespace FStudio.FootballWorld.Domain
{
    // Supported rule profiles are deliberately explicit, not executable content.
    public sealed class LeagueRules
    {
        public int Legs { get; }
        public int WinPoints { get; }
        public int DrawPoints { get; }
        public int LossPoints { get; }
        public bool IsPaulista2026 { get; }
        public LeagueRules(int legs, int winPoints, int drawPoints, int lossPoints, bool paulista2026 = false)
        {
            Legs = DomainValidation.InRange(legs, 1, 2, nameof(legs));
            WinPoints = DomainValidation.InRange(winPoints, 0, 100, nameof(winPoints));
            DrawPoints = DomainValidation.InRange(drawPoints, 0, 100, nameof(drawPoints));
            LossPoints = DomainValidation.InRange(lossPoints, 0, 100, nameof(lossPoints));
            if (WinPoints <= DrawPoints || DrawPoints < LossPoints)
                throw new ArgumentException("Points must satisfy win > draw >= loss.");
            IsPaulista2026 = paulista2026;
            if (paulista2026 && (legs != 1 || winPoints != 3 || drawPoints != 1 || lossPoints != 0))
                throw new ArgumentException("Paulista 2026 requires one authored league phase and 3/1/0 points.");
        }
        public int RoundCount(int clubCount)
        {
            DomainValidation.InRange(clubCount, 2, 64, nameof(clubCount));
            if (IsPaulista2026)
            {
                if (clubCount != 16) throw new ArgumentException("Paulista 2026 requires sixteen participants.", nameof(clubCount));
                return 8;
            }
            return (clubCount % 2 == 0 ? clubCount - 1 : clubCount) * Legs;
        }
    }
}
