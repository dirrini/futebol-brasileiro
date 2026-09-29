using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;
using NUnit.Framework;

namespace FStudio.FootballWorld.Tests
{
    public sealed class CareerTransferTests
    {
        [Test]
        public void AcceptedOfferUpdatesCareerMembershipOnlyAndChargesOnceAcrossRestores()
        {
            var career = Create();
            var source = career.Competition.Catalog;
            var offer = career.SubmitOffer("club-2-p1", career.EstimateTransferValue("club-2-p1"));
            Assert.That(career.AvailableTransferBudget, Is.EqualTo(4750000));
            Assert.That(career.GetCurrentClubId(offer.PlayerId), Is.EqualTo("club-2"));
            career.AdvanceDay();
            Assert.That(career.Offers.Single().Status, Is.EqualTo(CareerTransferStatus.Accepted));
            Assert.That(career.FinanceBalance, Is.EqualTo(4750000));
            Assert.That(career.GetRoster().Any(value => value.Id == offer.PlayerId), Is.True);
            Assert.That(career.GetCurrentClubId(offer.PlayerId), Is.EqualTo("club-1"));
            Assert.That(source.GetRoster("club-2").Any(value => value.Id == offer.PlayerId), Is.True);
            Assert.That(career.Competition.Catalog, Is.SameAs(source));
            for (var i = 0; i < 3; i++) career = CareerSession.Restore(source, career.CaptureSnapshot());
            Assert.That(career.FinanceBalance, Is.EqualTo(4750000));
            Assert.That(career.Ledger.Count(value => value.EventKey == "transfer-fee"), Is.EqualTo(1));
            Assert.That(career.News.Single(value => value.EventKey == "transfer-accepted").PlayerId, Is.EqualTo(offer.PlayerId));
            Assert.That(career.News.Single(value => value.EventKey == "transfer-accepted").FixtureId, Is.Null);
        }

        [Test]
        public void ReservationsCancellationAndSameDayResubmissionSurviveRestore()
        {
            var career = Create(initial: 500000);
            var first = career.SubmitOffer("club-2-p1", 500000);
            Assert.Throws<InvalidOperationException>(() => career.SubmitOffer("club-2-p2", 1));
            Assert.Throws<InvalidOperationException>(() => career.SubmitOffer(first.PlayerId, 250000));
            Assert.That(career.CancelOffer(first.Id), Is.True);
            Assert.That(career.CancelOffer(first.Id), Is.False);
            var second = career.SubmitOffer(first.PlayerId, 250000);
            var third = career.SubmitOffer("club-2-p2", 250000);
            Assert.That(career.ReservedTransferBudget, Is.EqualTo(500000));
            career = CareerSession.Restore(career.Competition.Catalog, career.CaptureSnapshot());
            Assert.That(career.Offers.Select(value => value.SubmissionOrder), Is.EqualTo(new[] {1, 3, 4}));
            Assert.That(career.AvailableTransferBudget, Is.Zero);
            career.AdvanceDay();
            Assert.That(career.Offers.Count(value => value.Status == CareerTransferStatus.Accepted), Is.EqualTo(2));
            Assert.That(career.FinanceBalance, Is.Zero);
            Assert.That(career.CancelOffer(second.Id), Is.False);
            Assert.That(career.Offers.Single(value => value.Id == third.Id).DecisionDate, Is.EqualTo(new GameDate(2026, 1, 2)));
            Assert.DoesNotThrow(() => CareerSession.Restore(career.Competition.Catalog, career.CaptureSnapshot()));
        }

        [Test]
        public void LowOffersAreRejectedAndRegisteredPlayersCannotBeAcquiredForFree()
        {
            var career = Create();
            Assert.That(career.EstimateTransferValue("club-2-p1"), Is.EqualTo(250000));
            Assert.Throws<ArgumentException>(() => career.SubmitOffer("club-2-p1", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => career.SubmitOffer("club-2-p1", -1));
            Assert.Throws<InvalidOperationException>(() => career.SubmitOffer("club-1-p1", 250000));
            career.SubmitOffer("club-2-p1", 10000);
            career.AdvanceDay();
            Assert.That(career.Offers.Single().Reason, Is.EqualTo(CareerTransferReason.BelowValuation));
            Assert.That(career.FinanceBalance, Is.EqualTo(5000000));
            Assert.That(career.GetCurrentClubId("club-2-p1"), Is.EqualTo("club-2"));
            Assert.That(career.ReservedTransferBudget, Is.Zero);
        }

        [Test]
        public void SellerRetainsElevenPlayersAndItsOnlyGoalkeeper()
        {
            var small = Create(sellerSize: 11);
            small.SubmitOffer("club-2-p1", 250000);
            small.AdvanceDay();
            Assert.That(small.Offers.Single().Reason, Is.EqualTo(CareerTransferReason.SellerSquadTooSmall));
            var goalkeeper = Create(sellerSize: 12);
            goalkeeper.SubmitOffer("club-2-p0", 250000);
            goalkeeper.AdvanceDay();
            Assert.That(goalkeeper.Offers.Single().Reason, Is.EqualTo(CareerTransferReason.SellerNeedsGoalkeeper));
            var consecutive = Create(sellerSize: 12);
            consecutive.SubmitOffer("club-2-p1", 250000);
            consecutive.SubmitOffer("club-2-p2", 250000);
            consecutive.AdvanceDay();
            Assert.That(consecutive.Offers[0].Status, Is.EqualTo(CareerTransferStatus.Accepted));
            Assert.That(consecutive.Offers[1].Reason, Is.EqualTo(CareerTransferReason.SellerSquadTooSmall));
            Assert.That(consecutive.GetRoster("club-2").Count, Is.EqualTo(11));
        }

        [Test]
        public void MonthlyExpensesCanRejectAnOfferBeforeItIsCharged()
        {
            var career = Create(initial: 500000, wages: 700000, openingMonth: 2,
                rules: new CareerManagementRules(monthlyIncome: 0));
            for (var i = 0; i < 30; i++) career.AdvanceDay();
            Assert.That(career.CurrentDate, Is.EqualTo(new GameDate(2026, 1, 31)));
            career.SubmitOffer("club-2-p1", 250000);
            career.AdvanceDay();
            Assert.That(career.Offers.Single().Reason, Is.EqualTo(CareerTransferReason.InsufficientFunds));
            Assert.That(career.FinanceBalance, Is.EqualTo(-200000));
            Assert.That(career.Ledger.Any(value => value.EventKey == "transfer-fee"), Is.False);
            Assert.DoesNotThrow(() => CareerSession.Restore(career.Competition.Catalog, career.CaptureSnapshot()));
        }

        [Test]
        public void FreeAgentJoinsWithoutTransferFeeAndPinnedMembershipStaysAbsent()
        {
            var career = Create();
            Assert.That(career.GetCurrentClubId("free-agent"), Is.Null);
            Assert.That(career.EstimateTransferValue("free-agent"), Is.Zero);
            Assert.Throws<ArgumentException>(() => career.SubmitOffer("free-agent", 1));
            career.SubmitOffer("free-agent", 0);
            career.AdvanceDay();
            Assert.That(career.GetCurrentClubId("free-agent"), Is.EqualTo("club-1"));
            Assert.That(career.FinanceBalance, Is.EqualTo(5000000));
            Assert.That(career.Competition.Catalog.Memberships.Any(value => value.PlayerId == "free-agent"), Is.False);
            Assert.DoesNotThrow(() => CareerSession.Restore(career.Competition.Catalog, career.CaptureSnapshot()));
        }

        [Test]
        public void SameDayProposalsPreserveLedgerAnchorsAroundPlayedGateIncome()
        {
            var career = Create();
            career.AdvanceToNextFixture();
            var first = career.SubmitOffer("club-2-p1", 250000);
            career.SimulateNextFixture();
            var second = career.SubmitOffer("club-2-p2", 250000);
            Assert.That(second.SubmittedLedgerCount, Is.GreaterThanOrEqualTo(first.SubmittedLedgerCount));
            var restored = CareerSession.Restore(career.Competition.Catalog, career.CaptureSnapshot());
            restored.AdvanceDay();
            Assert.That(restored.Offers.All(value => value.Status == CareerTransferStatus.Accepted), Is.True);
            Assert.DoesNotThrow(() => CareerSession.Restore(career.Competition.Catalog, restored.CaptureSnapshot()));
        }

        [Test]
        public void CancellationAfterGateIncomeCanFundAReplacementOfferAndReplayExactly()
        {
            var career = Create(initial: 250000);
            career.AdvanceToNextFixture();
            Assert.That(career.Competition.NextFixture.HomeClubId, Is.EqualTo("club-1"));
            var first = career.SubmitOffer("club-2-p1", 250000);
            career.SimulateNextFixture();
            career.CancelOffer(first.Id);
            var second = career.SubmitOffer("club-2-p2", 350000);
            Assert.That(second.SubmittedLedgerCount, Is.EqualTo(first.SubmittedLedgerCount + 1));
            var restored = CareerSession.Restore(career.Competition.Catalog, career.CaptureSnapshot());
            Assert.That(restored.AvailableTransferBudget, Is.Zero);
            restored.AdvanceDay();
            Assert.That(restored.Offers[0].Status, Is.EqualTo(CareerTransferStatus.Cancelled));
            Assert.That(restored.Offers[1].Status, Is.EqualTo(CareerTransferStatus.Accepted));
            Assert.That(restored.FinanceBalance, Is.Zero);
            Assert.That(restored.GetCurrentClubId(first.PlayerId), Is.EqualTo("club-2"));
            Assert.DoesNotThrow(() => CareerSession.Restore(career.Competition.Catalog, restored.CaptureSnapshot()));
        }

        [Test]
        public void TamperedDecisionsValuesLedgerAnchorsIdentityAndTacticsAreRejected()
        {
            var career = Create();
            career.SubmitOffer("club-2-p1", 250000);
            career.AdvanceDay();
            var snapshot = career.CaptureSnapshot();
            var offer = snapshot.Offers.Single();
            var altered = new CareerTransferOffer(offer.Id, offer.PlayerId, offer.SellerClubId, 1, offer.SubmittedDate,
                offer.SubmissionOrder, offer.SubmittedLedgerCount, offer.Status, offer.DecisionDate);
            Assert.Throws<ArgumentException>(() => CareerSession.Restore(career.Competition.Catalog, Copy(snapshot, new[] {altered})));
            altered = new CareerTransferOffer(offer.Id, offer.PlayerId, offer.SellerClubId, offer.Amount, offer.SubmittedDate,
                99, offer.SubmittedLedgerCount, offer.Status, offer.DecisionDate);
            Assert.Throws<ArgumentException>(() => CareerSession.Restore(career.Competition.Catalog, Copy(snapshot, new[] {altered})));
            altered = new CareerTransferOffer(offer.Id, offer.PlayerId, "club-3", offer.Amount, offer.SubmittedDate,
                offer.SubmissionOrder, offer.SubmittedLedgerCount, offer.Status, offer.DecisionDate);
            Assert.Throws<ArgumentException>(() => CareerSession.Restore(career.Competition.Catalog, Copy(snapshot, new[] {altered})));
            altered = new CareerTransferOffer(offer.Id, offer.PlayerId, offer.SellerClubId, offer.Amount, offer.SubmittedDate,
                offer.SubmissionOrder, 500, offer.Status, offer.DecisionDate);
            Assert.Throws<ArgumentException>(() => CareerSession.Restore(career.Competition.Catalog, Copy(snapshot, new[] {altered})));
            Assert.Throws<ArgumentException>(() => CareerSession.Restore(career.Competition.Catalog, Copy(snapshot, formation: (CareerFormation)99)));
        }

        [Test]
        public void TacticsPersistAndManagementCommandsAreBlockedDuringMatchOrAfterCalendar()
        {
            var career = Create();
            Assert.That(career.Formation, Is.EqualTo(CareerFormation.FourFourTwo));
            career.SetTactics(CareerFormation.FourTwoThreeOne, CareerMentality.Attacking);
            career = CareerSession.Restore(career.Competition.Catalog, career.CaptureSnapshot());
            Assert.That(career.Formation, Is.EqualTo(CareerFormation.FourTwoThreeOne));
            Assert.That(career.Mentality, Is.EqualTo(CareerMentality.Attacking));
            career.AdvanceToNextFixture();
            var execution = career.BeginFixture();
            Assert.Throws<InvalidOperationException>(() => career.SetTactics(CareerFormation.FourThreeThree, CareerMentality.Defensive));
            Assert.Throws<InvalidOperationException>(() => career.SubmitOffer("club-2-p1", 250000));
            Assert.Throws<InvalidOperationException>(() => career.CancelOffer("missing"));
            career.Competition.AbortFixture(execution.FixtureId, execution.ExecutionId);
            while (!career.IsComplete)
            {
                if (career.IsMatchDay) career.SimulateNextFixture();
                else career.AdvanceToNextFixture();
            }
            Assert.Throws<InvalidOperationException>(() => career.SetTactics(CareerFormation.FourThreeThree, CareerMentality.Defensive));
            Assert.Throws<InvalidOperationException>(() => career.SubmitOffer("club-2-p1", 250000));
        }

        [Test]
        public void OfferHistoryHasBoundedPendingAndTotalCounts()
        {
            var career = Create(sellerSize: 30);
            for (var i = 1; i <= CareerSession.MaximumPendingOffers; i++) career.SubmitOffer("club-2-p" + i, 10000);
            Assert.Throws<InvalidOperationException>(() => career.SubmitOffer("club-2-p17", 10000));
            foreach (var offer in career.Offers.ToArray()) career.CancelOffer(offer.Id);
            while (career.Offers.Count < CareerSession.MaximumOffers)
            {
                var offer = career.SubmitOffer("club-2-p1", 10000);
                career.CancelOffer(offer.Id);
            }
            Assert.Throws<InvalidOperationException>(() => career.SubmitOffer("club-2-p1", 10000));
            Assert.DoesNotThrow(() => CareerSession.Restore(career.Competition.Catalog, career.CaptureSnapshot()));
        }

        private static CareerSnapshot Copy(CareerSnapshot value, IEnumerable<CareerTransferOffer> offers = null, CareerFormation? formation = null)
            => new CareerSnapshot(value.Competition, value.StartDate, value.Rules, value.Training, value.Condition, value.Preparation,
                value.TrainingChanges, value.Ledger, value.News, value.ProcessedFixtureIds, formation ?? value.Formation, value.Mentality, offers ?? value.Offers);

        private static CareerSession Create(int sellerSize = 14, int? initial = null, int? wages = null, int openingMonth = 1, CareerManagementRules rules = null)
        {
            var clubs = Enumerable.Range(1, 4).Select(value => new ClubDefinition("club-" + value, "Club " + value,
                transferBudget: initial, monthlyWageBudget: wages, currency: initial.HasValue || wages.HasValue ? "BRL" : null)).ToArray();
            var players = new List<PlayerDefinition>();
            var memberships = new List<RosterMembership>();
            foreach (var club in clubs)
                for (var i = 0; i < (club.Id == "club-2" ? sellerSize : 14); i++)
                {
                    var player = Player(club.Id + "-p" + i, i == 0);
                    players.Add(player);
                    memberships.Add(new RosterMembership(club.Id, player.Id));
                }
            players.Add(Player("free-agent", false));
            var edition = new CompetitionEditionDefinition("edition", "competition", "Test season", clubs.Select(value => value.Id),
                new[] {new GameDate(2026, openingMonth, 10), new GameDate(2026, openingMonth, 20), new GameDate(2026, openingMonth + 1, 10)}, new LeagueRules(1, 3, 1, 0));
            var catalog = new DatabaseCatalog("database", 1, clubs, players, memberships,
                new[] {new CompetitionDefinition("competition", "Test league")}, new[] {edition});
            return CareerSession.Create(catalog, "edition", "season", "club-1", new GameDate(2026, 1, 1), rules);
        }

        private static PlayerDefinition Player(string id, bool goalkeeper)
            => new PlayerDefinition(id, id, new[] {goalkeeper ? PlayerPosition.GK : PlayerPosition.CM}, 180, 75,
                new PlayerAttributes(50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50));
    }
}
