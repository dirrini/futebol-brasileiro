using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    // Owns mutable management progress. Authored clubs, budgets and rosters stay immutable.
    public sealed partial class CareerSession
    {
        private readonly List<CareerLedgerEntry> ledger = new List<CareerLedgerEntry>();
        private readonly List<CareerNewsItem> news = new List<CareerNewsItem>();
        private readonly List<CareerTrainingChange> trainingChanges = new List<CareerTrainingChange>();
        private readonly HashSet<string> processedFixtureIds = new HashSet<string>(StringComparer.Ordinal);
        private GameDate managementDate;

        public CompetitionSession Competition { get; }
        public GameDate StartDate { get; }
        public GameDate CurrentDate => Competition.CurrentDate.Value;
        public GameDate EndDate => Competition.Edition.EndDate;
        public CareerManagementRules Rules { get; }
        public CareerTraining Training { get; private set; } = CareerTraining.Balanced;
        public int Condition { get; private set; } = 100;
        public int Preparation { get; private set; } = 50;
        public long FinanceBalance => ledger.Sum(value => value.Amount);
        public long MonthlyIncome => Rules.MonthlyIncome;
        public long MonthlyWages { get; }
        public string Currency { get; }
        public bool UsesDefaultInitialBalance { get; }
        public bool UsesDefaultMonthlyWages { get; }
        public IReadOnlyList<CareerLedgerEntry> Ledger { get; }
        public IReadOnlyList<CareerNewsItem> News { get; }
        public bool IsMatchDay => Competition.NextFixture != null && Competition.NextFixture.Date.CompareTo(CurrentDate) <= 0;
        public bool CanAdvance => Competition.ActiveExecution == null && !IsMatchDay && CurrentDate.CompareTo(EndDate) < 0;
        public bool IsComplete => Competition.IsComplete && CurrentDate.CompareTo(EndDate) >= 0;
        // An adapter may scale temporary match attributes; authored player data never changes.
        public int MatchPerformancePercent => Math.Max(90, Math.Min(110, 90 + Condition / 10 + Preparation / 10));

        private CareerSession(CompetitionSession competition, GameDate startDate, CareerManagementRules rules)
        {
            Competition = competition ?? throw new ArgumentNullException(nameof(competition));
            if (!competition.DailyProgress || !competition.CurrentDate.HasValue)
                throw new ArgumentException("A career requires a competition with daily progress.");
            ValidateStart(competition.Edition, startDate);
            if (CurrentDate.CompareTo(startDate) < 0 || CurrentDate.CompareTo(EndDate) > 0)
                throw new ArgumentException("The career date must belong to the selected calendar.");
            StartDate = managementDate = startDate;
            Rules = rules ?? new CareerManagementRules();
            var club = competition.Catalog.GetClub(competition.ControlledClubId);
            MonthlyWages = club.MonthlyWageBudget ?? Rules.DefaultMonthlyWages;
            Currency = club.Currency ?? "BRL";
            UsesDefaultInitialBalance = !club.TransferBudget.HasValue;
            UsesDefaultMonthlyWages = !club.MonthlyWageBudget.HasValue;
            Ledger = ledger.AsReadOnly();
            News = news.AsReadOnly();
            Offers = offers.AsReadOnly();
            ledger.Add(new CareerLedgerEntry("opening", startDate, "opening-balance", club.TransferBudget ?? Rules.DefaultInitialBalance));
            news.Add(new CareerNewsItem("welcome", startDate, "gazeta-da-bola", "welcome", amount: FinanceBalance));
        }

        public static CareerSession Create(DatabaseCatalog catalog, string editionId, string seasonId,
            string controlledClubId, GameDate startDate, CareerManagementRules rules = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            ValidateStart(catalog.GetCompetitionEdition(editionId), startDate);
            return new CareerSession(CompetitionSession.CreateDaily(catalog, editionId, seasonId, controlledClubId, startDate), startDate, rules);
        }

        private static void ValidateStart(CompetitionEditionDefinition edition, GameDate startDate)
        {
            if (startDate.Day != 1 || startDate.CompareTo(edition.StartDate) > 0)
                throw new ArgumentException("Start the career on the first day of a month at or before the edition's first match.", nameof(startDate));
            if ((edition.EndDate.ToDateTime() - startDate.ToDateTime()).TotalDays > 3660)
                throw new ArgumentException("The prototype supports calendars up to ten years from the starting month.", nameof(startDate));
        }

        public void SetTraining(CareerTraining training)
        {
            if (!Enum.IsDefined(typeof(CareerTraining), training)) throw new ArgumentException("Unknown training mode.");
            if (Competition.ActiveExecution != null) throw new InvalidOperationException("Training cannot change during a match.");
            if (Training == training) return;
            Training = training;
            // Changes take effect overnight; editing repeatedly never grants instant improvements.
            trainingChanges.RemoveAll(value => value.Date.Equals(CurrentDate));
            trainingChanges.Add(new CareerTrainingChange(CurrentDate, training));
        }

        public bool AdvanceDay()
        {
            ReconcileResults();
            if (!CanAdvance) return false;
            var next = AddDays(CurrentDate, 1);
            Competition.SimulateThrough(next);
            if (!CurrentDate.Equals(next)) return false;
            ProcessNewDay(next);
            ReconcileResults();
            return true;
        }

        public int AdvanceToNextFixture()
        {
            var days = 0;
            while (CanAdvance && AdvanceDay()) days++;
            return days;
        }

        public FixtureExecution BeginFixture(string executionId = null)
        {
            if (!IsMatchDay) throw new InvalidOperationException("Advance to the controlled club's match day first.");
            return Competition.BeginFixture(Competition.NextFixture.Id, executionId);
        }

        public FixtureCompletion CompleteFixture(FixtureResult result)
        {
            var completion = Competition.CompleteFixture(result);
            ReconcileResults();
            return completion;
        }

        public FixtureCompletion SimulateNextFixture()
        {
            if (!IsMatchDay) throw new InvalidOperationException("Advance to the controlled club's match day first.");
            var completion = Competition.SimulateFixture(Competition.NextFixture.Id);
            ReconcileResults();
            return completion;
        }

        public void ReconcileResults()
        {
            if (!CurrentDate.Equals(managementDate))
                throw new InvalidOperationException("Advance the calendar through the career application operation.");
            if (Competition.ActiveExecution == null) Competition.SimulateThrough(CurrentDate);
            ApplyResultsThrough(managementDate);
        }

        private void ProcessNewDay(GameDate date)
        {
            switch (Training)
            {
                case CareerTraining.Recovery: Condition = Clamp(Condition + 4); Preparation = Clamp(Preparation - 2); break;
                case CareerTraining.Intensive: Condition = Clamp(Condition - 3); Preparation = Clamp(Preparation + 3); break;
                default: Condition = Clamp(Condition + 1); Preparation = Clamp(Preparation + 1); break;
            }
            managementDate = date;
            if (date.Day == 1)
            {
                var month = date.ToString().Substring(0, 7);
                ledger.Add(new CareerLedgerEntry("income-" + month, date, "monthly-income", MonthlyIncome));
                ledger.Add(new CareerLedgerEntry("wages-" + month, date, "monthly-wages", -MonthlyWages));
                news.Add(new CareerNewsItem("finance-" + month, date, "diario-da-arquibancada", "monthly-finances", amount: MonthlyIncome - MonthlyWages));
            }
            ResolveOffers(date);
            var elapsed = (int)(date.ToDateTime() - StartDate.ToDateTime()).TotalDays;
            if (elapsed % 7 == 0)
                news.Add(new CareerNewsItem("week-" + date, date,
                    elapsed % 14 == 0 ? "diario-da-arquibancada" : "gazeta-da-bola", "training-report",
                    amount: FinanceBalance, condition: Condition, preparation: Preparation));
        }

        private void ApplyResultsThrough(GameDate date)
        {
            var fixtures = Competition.Fixtures.ToDictionary(value => value.Id, StringComparer.Ordinal);
            foreach (var result in Competition.Results.OrderBy(value => fixtures[value.FixtureId].Date).ThenBy(value => value.FixtureId, StringComparer.Ordinal))
            {
                var fixture = fixtures[result.FixtureId];
                if (!fixture.IncludesClub(Competition.ControlledClubId) || fixture.Date.CompareTo(date) > 0 || !processedFixtureIds.Add(fixture.Id)) continue;
                Condition = Clamp(Condition - 12);
                Preparation = Clamp(Preparation - 3);
                if (fixture.HomeClubId == Competition.ControlledClubId)
                    ledger.Add(new CareerLedgerEntry(Key("gate", fixture.Id), fixture.Date, "home-match-income", Rules.HomeMatchIncome, fixture.Id));
                news.Add(new CareerNewsItem(Key("match", fixture.Id), fixture.Date,
                    fixture.HomeClubId == Competition.ControlledClubId ? "gazeta-da-bola" : "diario-da-arquibancada",
                    "match-result", fixture.Id, condition: Condition, preparation: Preparation));
            }
            if (Competition.IsComplete && Competition.Results.All(value => fixtures[value.FixtureId].Date.CompareTo(date) <= 0) &&
                news.All(value => value.Id != "season-complete"))
                news.Add(new CareerNewsItem("season-complete", date, "gazeta-da-bola", "season-complete"));
        }

        public CareerSnapshot CaptureSnapshot()
        {
            ReconcileResults();
            return new CareerSnapshot(Competition.CaptureSnapshot(), StartDate, Rules, Training, Condition, Preparation,
                trainingChanges, ledger, news, processedFixtureIds.OrderBy(value => value, StringComparer.Ordinal), Formation, Mentality, offers);
        }

        public static CareerSession Restore(DatabaseCatalog catalog, CareerSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var competition = CompetitionSession.Restore(catalog, snapshot.Competition);
            var session = new CareerSession(competition, snapshot.StartDate, snapshot.Rules);
            if (snapshot.TrainingChanges.Count > 3661 || snapshot.Ledger.Count > 5000 || snapshot.News.Count > 5000)
                throw new ArgumentException("Career history exceeds its bounded calendar.");
            ValidateTactics(snapshot.Formation, snapshot.Mentality);
            session.Formation = snapshot.Formation;
            session.Mentality = snapshot.Mentality;
            if (snapshot.Offers.Count > MaximumOffers || snapshot.Offers.Any(value => value == null ||
                value.SubmittedDate.CompareTo(session.StartDate) < 0 || value.SubmittedDate.CompareTo(session.CurrentDate) > 0 ||
                (value.DecisionDate.HasValue && value.DecisionDate.Value.CompareTo(session.CurrentDate) > 0)))
                throw new ArgumentException("Transfer history is outside the saved career calendar.");
            var changes = new Dictionary<GameDate, CareerTraining>();
            foreach (var change in snapshot.TrainingChanges)
            {
                if (change == null || change.Date.CompareTo(session.StartDate) < 0 || change.Date.CompareTo(session.CurrentDate) > 0 ||
                    changes.ContainsKey(change.Date)) throw new ArgumentException("Training changes require distinct dates in the saved calendar.");
                changes.Add(change.Date, change.Training);
            }
            // Replay management only. The restored competition already validates every fixture/result;
            // it is not simulated again, and source budgets are read from the pinned catalog.
            for (var date = session.StartDate; date.CompareTo(session.CurrentDate) <= 0; date = AddDays(date, 1))
            {
                if (!date.Equals(session.StartDate)) session.ProcessNewDay(date);
                session.ApplyResultsThrough(date);
                session.ReplayTransferCommands(date, snapshot.Offers);
                if (changes.TryGetValue(date, out var training))
                {
                    session.Training = training;
                    session.trainingChanges.Add(new CareerTrainingChange(date, training));
                }
                if (date.Equals(session.CurrentDate)) break;
            }
            if (session.Training != snapshot.Training || session.Condition != snapshot.Condition || session.Preparation != snapshot.Preparation ||
                !session.processedFixtureIds.SetEquals(snapshot.ProcessedFixtureIds) ||
                snapshot.ProcessedFixtureIds.Count != session.processedFixtureIds.Count ||
                !LedgerEquals(session.ledger, snapshot.Ledger) || !NewsEquals(session.news, snapshot.News) || !OffersEqual(session.offers, snapshot.Offers))
                throw new ArgumentException("Career management history is inconsistent with its calendar, training and accepted results.");
            return session;
        }

        private static bool LedgerEquals(IReadOnlyList<CareerLedgerEntry> expected, IReadOnlyList<CareerLedgerEntry> actual)
        {
            if (expected.Count != actual.Count) return false;
            for (var i = 0; i < expected.Count; i++)
                if (actual[i] == null || expected[i].Id != actual[i].Id || !expected[i].Date.Equals(actual[i].Date) ||
                    expected[i].EventKey != actual[i].EventKey || expected[i].Amount != actual[i].Amount || expected[i].FixtureId != actual[i].FixtureId) return false;
            return true;
        }
        private static bool NewsEquals(IReadOnlyList<CareerNewsItem> expected, IReadOnlyList<CareerNewsItem> actual)
        {
            if (expected.Count != actual.Count) return false;
            for (var i = 0; i < expected.Count; i++)
                if (actual[i] == null || expected[i].Id != actual[i].Id || !expected[i].Date.Equals(actual[i].Date) ||
                    expected[i].OutletId != actual[i].OutletId || expected[i].EventKey != actual[i].EventKey || expected[i].FixtureId != actual[i].FixtureId ||
                    expected[i].Amount != actual[i].Amount || expected[i].Condition != actual[i].Condition || expected[i].Preparation != actual[i].Preparation ||
                    expected[i].PlayerId != actual[i].PlayerId) return false;
            return true;
        }
        private static int Clamp(int value) => Math.Max(0, Math.Min(100, value));
        private static GameDate AddDays(GameDate date, int days)
        {
            var value = date.ToDateTime().AddDays(days);
            return new GameDate(value.Year, value.Month, value.Day);
        }
        private static string Key(string prefix, string fixtureId)
        {
            using (var hash = SHA256.Create())
                return prefix + "-" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(fixtureId))).Replace("-", "").Substring(0, 48).ToLowerInvariant();
        }
    }
}
