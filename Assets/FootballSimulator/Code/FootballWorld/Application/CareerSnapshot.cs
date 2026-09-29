using System;
using System.Collections.Generic;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public enum CareerTraining { Balanced, Recovery, Intensive }

    // Versioned prototype management rules, independent of visuals and portable database DTOs.
    public sealed class CareerManagementRules
    {
        public int Version { get; }
        public long DefaultInitialBalance { get; }
        public long DefaultMonthlyWages { get; }
        public long MonthlyIncome { get; }
        public long HomeMatchIncome { get; }
        public CareerManagementRules(int version = 1, long defaultInitialBalance = 5000000,
            long defaultMonthlyWages = 250000, long monthlyIncome = 200000, long homeMatchIncome = 100000)
        {
            if (version != 1) throw new ArgumentException("Unsupported career management rules.", nameof(version));
            Version = version;
            DefaultInitialBalance = Money(defaultInitialBalance);
            DefaultMonthlyWages = Money(defaultMonthlyWages);
            MonthlyIncome = Money(monthlyIncome);
            HomeMatchIncome = Money(homeMatchIncome);
        }
        private static long Money(long value)
        {
            if (value < 0 || value > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));
            return value;
        }
    }

    public sealed class CareerTrainingChange
    {
        public GameDate Date { get; }
        public CareerTraining Training { get; }
        public CareerTrainingChange(GameDate date, CareerTraining training)
        {
            if (!Enum.IsDefined(typeof(CareerTraining), training)) throw new ArgumentException("Unknown training mode.");
            Date = date;
            Training = training;
        }
    }

    public sealed class CareerLedgerEntry
    {
        public string Id { get; }
        public GameDate Date { get; }
        public string EventKey { get; }
        public long Amount { get; }
        public string FixtureId { get; }
        public CareerLedgerEntry(string id, GameDate date, string eventKey, long amount, string fixtureId = null)
        {
            Id = CompetitionIdentity.Validate(id);
            Date = date;
            EventKey = CompetitionIdentity.Validate(eventKey);
            Amount = amount;
            FixtureId = fixtureId == null ? null : CompetitionIdentity.Validate(fixtureId);
        }
    }

    public sealed class CareerNewsItem
    {
        public string Id { get; }
        public GameDate Date { get; }
        public string OutletId { get; }
        public string EventKey { get; }
        public string FixtureId { get; }
        public long Amount { get; }
        public int Condition { get; }
        public int Preparation { get; }
        public string PlayerId { get; }
        public CareerNewsItem(string id, GameDate date, string outletId, string eventKey,
            string fixtureId = null, long amount = 0, int condition = 0, int preparation = 0, string playerId = null)
        {
            Id = CompetitionIdentity.Validate(id);
            Date = date;
            OutletId = CompetitionIdentity.Validate(outletId);
            EventKey = CompetitionIdentity.Validate(eventKey);
            FixtureId = fixtureId == null ? null : CompetitionIdentity.Validate(fixtureId);
            Amount = amount;
            Condition = Range(condition);
            Preparation = Range(preparation);
            PlayerId = playerId == null ? null : CompetitionIdentity.Validate(playerId);
        }
        private static int Range(int value)
        {
            if (value < 0 || value > 100) throw new ArgumentOutOfRangeException(nameof(value));
            return value;
        }
    }

    public sealed class CareerSnapshot
    {
        public CompetitionSnapshot Competition { get; }
        public GameDate StartDate { get; }
        public CareerManagementRules Rules { get; }
        public CareerTraining Training { get; }
        public int Condition { get; }
        public int Preparation { get; }
        public IReadOnlyList<CareerTrainingChange> TrainingChanges { get; }
        public IReadOnlyList<CareerLedgerEntry> Ledger { get; }
        public IReadOnlyList<CareerNewsItem> News { get; }
        public IReadOnlyList<string> ProcessedFixtureIds { get; }
        public CareerFormation Formation { get; }
        public CareerMentality Mentality { get; }
        public IReadOnlyList<CareerTransferOffer> Offers { get; }
        public CareerSnapshot(CompetitionSnapshot competition, GameDate startDate, CareerManagementRules rules,
            CareerTraining training, int condition, int preparation, IEnumerable<CareerTrainingChange> trainingChanges,
            IEnumerable<CareerLedgerEntry> ledger, IEnumerable<CareerNewsItem> news, IEnumerable<string> processedFixtureIds,
            CareerFormation formation = CareerFormation.FourFourTwo, CareerMentality mentality = CareerMentality.Balanced,
            IEnumerable<CareerTransferOffer> offers = null)
        {
            Competition = competition ?? throw new ArgumentNullException(nameof(competition));
            StartDate = startDate;
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Training = training;
            Condition = condition;
            Preparation = preparation;
            TrainingChanges = Copy(trainingChanges);
            Ledger = Copy(ledger);
            News = Copy(news);
            ProcessedFixtureIds = Copy(processedFixtureIds);
            Formation = formation;
            Mentality = mentality;
            Offers = Copy(offers ?? Array.Empty<CareerTransferOffer>());
        }
        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
            => new List<T>(values ?? throw new ArgumentNullException(nameof(values))).AsReadOnly();
    }
}
