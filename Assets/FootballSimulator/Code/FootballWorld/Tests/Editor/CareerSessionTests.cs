using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;
using NUnit.Framework;

namespace FStudio.FootballWorld.Tests
{
    public sealed class CareerSessionTests
    {
        [Test]
        public void DailyAdvanceStopsAtControlledMatchAndNeverCompletesItSilently()
        {
            var career = Create();
            Assert.That(career.AdvanceToNextFixture(), Is.EqualTo(9));
            Assert.That(career.CurrentDate, Is.EqualTo(new GameDate(2026, 1, 10)));
            Assert.That(career.IsMatchDay, Is.True);
            Assert.That(career.AdvanceDay(), Is.False);
            Assert.That(career.AdvanceToNextFixture(), Is.Zero);
            Assert.That(career.Competition.GetResult(career.Competition.NextFixture.Id), Is.Null);
            Assert.That(career.Competition.Results.All(value => value.IsSimulated), Is.True);
        }

        [Test]
        public void TrainingHasNoInstantBonusAndAffectsOnlyOvernightManagement()
        {
            var balanced = Create();
            var intense = Create();
            var original = intense.Condition;
            intense.SetTraining(CareerTraining.Intensive);
            intense.SetTraining(CareerTraining.Recovery);
            intense.SetTraining(CareerTraining.Intensive);
            Assert.That(intense.Condition, Is.EqualTo(original));
            Assert.That(intense.Preparation, Is.EqualTo(50));
            intense.AdvanceDay();
            balanced.AdvanceDay();
            Assert.That(intense.Condition, Is.EqualTo(97));
            Assert.That(intense.Preparation, Is.EqualTo(53));
            Assert.That(balanced.Condition, Is.EqualTo(100));
            Assert.That(balanced.Preparation, Is.EqualTo(51));
            intense.SetTraining(CareerTraining.Recovery);
            intense.AdvanceDay();
            Assert.That(intense.Condition, Is.EqualTo(100));
            Assert.That(intense.Preparation, Is.EqualTo(51));
            Assert.That(intense.MatchPerformancePercent, Is.InRange(90, 110));
            Assert.That(intense.CaptureSnapshot().TrainingChanges.Count, Is.EqualTo(2));
        }

        [Test]
        public void SourceBudgetIsStartingCashAndExplicitZeroDoesNotUseDefaults()
        {
            var catalog = Catalog(0, 0);
            var career = CareerSession.Create(catalog, "edition", "season", "club-1", new GameDate(2026, 1, 1));
            Assert.That(career.FinanceBalance, Is.Zero);
            Assert.That(career.MonthlyWages, Is.Zero);
            Assert.That(career.UsesDefaultInitialBalance, Is.False);
            Assert.That(career.UsesDefaultMonthlyWages, Is.False);
            career.AdvanceToNextFixture();
            career.SimulateNextFixture();
            career.AdvanceToNextFixture();
            Assert.That(career.Ledger.Single(value => value.EventKey == "monthly-wages").Amount, Is.Zero);
            Assert.That(catalog.GetClub("club-1").TransferBudget, Is.Zero);
        }

        [Test]
        public void DuplicateAndStaleResultsCannotDuplicateIncomeConditionOrNews()
        {
            var career = Create();
            career.AdvanceToNextFixture();
            var fixture = career.Competition.NextFixture;
            var abandoned = career.BeginFixture("abandoned");
            career.Competition.AbortFixture(fixture.Id, abandoned.ExecutionId);
            var execution = career.BeginFixture("played");
            var before = career.Condition;
            Assert.That(career.CompleteFixture(Result(fixture, abandoned.ExecutionId)), Is.EqualTo(FixtureCompletion.IgnoredStale));
            Assert.That(career.Condition, Is.EqualTo(before));
            var result = Result(fixture, execution.ExecutionId);
            career.CompleteFixture(result);
            var balance = career.FinanceBalance;
            var newsCount = career.News.Count;
            var condition = career.Condition;
            var accepted = career.Competition.GetResult(fixture.Id);
            Assert.That(career.CompleteFixture(accepted), Is.EqualTo(FixtureCompletion.AlreadyApplied));
            career.ReconcileResults();
            Assert.That(career.FinanceBalance, Is.EqualTo(balance));
            Assert.That(career.News.Count, Is.EqualTo(newsCount));
            Assert.That(career.Condition, Is.EqualTo(condition));
            Assert.That(career.News.Count(value => value.FixtureId == fixture.Id), Is.EqualTo(1));
        }

        [Test]
        public void RestoreReplaysManagementWithoutApplyingAnotherMonthlyPaymentOrGate()
        {
            var career = Create();
            career.SetTraining(CareerTraining.Intensive);
            career.AdvanceToNextFixture();
            career.SimulateNextFixture();
            career.SetTraining(CareerTraining.Recovery);
            career.AdvanceToNextFixture();
            career.SimulateNextFixture();
            career.SetTraining(CareerTraining.Balanced);
            var before = career.CaptureSnapshot();
            var restored = CareerSession.Restore(career.Competition.Catalog, before);
            var restoredTwice = CareerSession.Restore(career.Competition.Catalog, restored.CaptureSnapshot());
            Assert.That(restoredTwice.CurrentDate, Is.EqualTo(career.CurrentDate));
            Assert.That(restoredTwice.FinanceBalance, Is.EqualTo(career.FinanceBalance));
            Assert.That(restoredTwice.Condition, Is.EqualTo(career.Condition));
            Assert.That(restoredTwice.Preparation, Is.EqualTo(career.Preparation));
            Assert.That(restoredTwice.News.Count, Is.EqualTo(career.News.Count));
            Assert.That(restoredTwice.Ledger.Count(value => value.EventKey == "monthly-wages"), Is.EqualTo(1));
            Assert.That(restoredTwice.News.Select(value => value.OutletId).Distinct().Count(), Is.EqualTo(2));
            Assert.That(restoredTwice.News.Select(value => value.Id).Distinct().Count(), Is.EqualTo(restoredTwice.News.Count));
        }

        [Test]
        public void TamperedLedgerTrainingConditionNewsOrProcessedFixturesAreRejected()
        {
            var career = Create();
            career.AdvanceToNextFixture();
            career.SimulateNextFixture();
            var valid = career.CaptureSnapshot();
            var ledger = valid.Ledger.ToArray();
            ledger[0] = new CareerLedgerEntry("opening", valid.StartDate, "opening-balance", 999999);
            Assert.Throws<ArgumentException>(() => CareerSession.Restore(career.Competition.Catalog, Copy(valid, ledger: ledger)));
            Assert.Throws<ArgumentException>(() => CareerSession.Restore(career.Competition.Catalog, Copy(valid, condition: 100)));
            Assert.Throws<ArgumentException>(() => CareerSession.Restore(career.Competition.Catalog, Copy(valid, processed: new string[0])));
            Assert.Throws<ArgumentException>(() => CareerSession.Restore(career.Competition.Catalog, Copy(valid, news: valid.News.Skip(1))));
            Assert.Throws<ArgumentException>(() => CareerSession.Restore(career.Competition.Catalog,
                Copy(valid, changes: new[] {new CareerTrainingChange(new GameDate(2027, 1, 1), CareerTraining.Intensive)})));
        }

        [Test]
        public void CompleteCalendarFinishesRemainingAiGamesAndCannotAdvanceBeyondEdition()
        {
            var career = Create();
            var guard = 0;
            while (!career.IsComplete && guard++ < 20)
            {
                career.AdvanceToNextFixture();
                if (career.IsMatchDay) career.SimulateNextFixture();
            }
            Assert.That(career.IsComplete, Is.True);
            Assert.That(career.CurrentDate, Is.EqualTo(new GameDate(2026, 3, 10)));
            Assert.That(career.AdvanceDay(), Is.False);
            Assert.That(career.AdvanceToNextFixture(), Is.Zero);
            Assert.That(career.Competition.Results.Count, Is.EqualTo(career.Competition.Fixtures.Count));
            Assert.That(career.Ledger.Count(value => value.EventKey == "monthly-wages"), Is.EqualTo(2));
            Assert.That(career.News.Count(value => value.EventKey == "season-complete"), Is.EqualTo(1));
            Assert.DoesNotThrow(() => CareerSession.Restore(career.Competition.Catalog, career.CaptureSnapshot()));
        }

        [Test]
        public void EveryPaulistaClubCanFinishItsCareerAndRestoreAcrossEliminationAndFinals()
        {
            var catalog = PaulistaCompetitionTests.Catalog();
            foreach (var club in catalog.Clubs)
            {
                var career = CareerSession.Create(catalog, "edition", "same-calendar", club.Id, new GameDate(2026, 1, 1));
                var guard = 0;
                while (!career.IsComplete && guard++ < 20)
                {
                    career.AdvanceToNextFixture();
                    if (career.IsMatchDay) career.SimulateNextFixture();
                    career = CareerSession.Restore(catalog, career.CaptureSnapshot());
                }
                Assert.That(career.IsComplete, Is.True, club.Id);
                Assert.That(career.Competition.Results.Count, Is.EqualTo(72), club.Id);
                Assert.That(career.CurrentDate, Is.EqualTo(new GameDate(2026, 3, 8)), club.Id);
                Assert.That(career.Competition.RelegatedClubIds.Count, Is.EqualTo(2));
                Assert.That(career.News.Count(value => value.EventKey == "season-complete"), Is.EqualTo(1));
            }
        }

        [Test]
        public void NoLateStartOrUndatedMatchAndNoChangesDuringActiveMatch()
        {
            Assert.Throws<ArgumentException>(() => CareerSession.Create(Catalog(), "edition", "season", "club-1", new GameDate(2026, 2, 1)));
            Assert.Throws<ArgumentException>(() => CareerSession.Create(Catalog(), "edition", "season", "club-1", new GameDate(2026, 1, 2)));
            var career = Create();
            Assert.Throws<InvalidOperationException>(() => career.BeginFixture());
            Assert.Throws<InvalidOperationException>(() => career.SimulateNextFixture());
            career.AdvanceToNextFixture();
            career.BeginFixture();
            Assert.That(career.AdvanceDay(), Is.False);
            Assert.Throws<InvalidOperationException>(() => career.SetTraining(CareerTraining.Recovery));
        }

        private static CareerSnapshot Copy(CareerSnapshot value, IEnumerable<CareerLedgerEntry> ledger = null,
            int? condition = null, IEnumerable<CareerNewsItem> news = null, IEnumerable<string> processed = null,
            IEnumerable<CareerTrainingChange> changes = null)
            => new CareerSnapshot(value.Competition, value.StartDate, value.Rules, value.Training, condition ?? value.Condition,
                value.Preparation, changes ?? value.TrainingChanges, ledger ?? value.Ledger, news ?? value.News, processed ?? value.ProcessedFixtureIds);

        private static FixtureResult Result(FixtureDefinition fixture, string executionId)
            => new FixtureResult(fixture.Id, executionId, fixture.HomeClubId, fixture.AwayClubId, 2, 1);

        private static CareerSession Create() => CareerSession.Create(Catalog(), "edition", "season", "club-1", new GameDate(2026, 1, 1));

        private static DatabaseCatalog Catalog(int? initial = null, int? wages = null)
        {
            var clubs = Enumerable.Range(1, 4).Select(value => new ClubDefinition("club-" + value, "Club " + value,
                transferBudget: initial, monthlyWageBudget: wages, currency: initial.HasValue || wages.HasValue ? "BRL" : null)).ToArray();
            var edition = new CompetitionEditionDefinition("edition", "competition", "Test season", clubs.Select(value => value.Id),
                new[] {new GameDate(2026, 1, 10), new GameDate(2026, 2, 10), new GameDate(2026, 3, 10)}, new LeagueRules(1, 3, 1, 0));
            return new DatabaseCatalog("database", 1, clubs, new PlayerDefinition[0], new RosterMembership[0],
                new[] {new CompetitionDefinition("competition", "Test league")}, new[] {edition});
        }
    }
}
