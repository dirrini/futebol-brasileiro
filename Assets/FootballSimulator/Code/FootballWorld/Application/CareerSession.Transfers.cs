using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public sealed partial class CareerSession
    {
        public const int MaximumOffers = 128;
        public const int MaximumPendingOffers = 16;
        private readonly List<CareerTransferOffer> offers = new List<CareerTransferOffer>();
        private int transferCommandOrder;
        private DatabaseCatalog effectiveCatalog;

        public CareerFormation Formation { get; private set; } = CareerFormation.FourFourTwo;
        public CareerMentality Mentality { get; private set; } = CareerMentality.Balanced;
        public DatabaseCatalog EffectiveCatalog => effectiveCatalog ?? Competition.Catalog;
        public IReadOnlyList<CareerTransferOffer> Offers { get; private set; }
        public long ReservedTransferBudget => offers.Where(value => value.Status == CareerTransferStatus.Pending).Sum(value => value.Amount);
        public long AvailableTransferBudget => Math.Max(0, FinanceBalance - ReservedTransferBudget);

        public IReadOnlyList<PlayerDefinition> GetRoster(string clubId = null)
            => EffectiveCatalog.GetRoster(clubId ?? Competition.ControlledClubId);

        public string GetCurrentClubId(string playerId)
        {
            EffectiveCatalog.GetPlayer(playerId);
            return EffectiveCatalog.Memberships.FirstOrDefault(value => value.PlayerId == playerId)?.ClubId;
        }

        public long EstimateTransferValue(string playerId)
        {
            var player = EffectiveCatalog.GetPlayer(playerId);
            if (GetCurrentClubId(playerId) == null) return 0;
            var a = player.Attributes;
            var average = (a.Strength + a.Acceleration + a.TopSpeed + a.DribbleSpeed + a.Jump + a.Tackling +
                a.BallKeeping + a.Passing + a.LongBall + a.Agility + a.Shooting + a.ShootPower +
                a.Positioning + a.Reaction + a.BallControl) / 15;
            return Math.Max(10000L, (long)average * average * 100);
        }

        public void SetTactics(CareerFormation formation, CareerMentality mentality)
        {
            EnsureManagementAvailable();
            ValidateTactics(formation, mentality);
            Formation = formation;
            Mentality = mentality;
        }

        public CareerTransferOffer SubmitOffer(string playerId, long amount)
        {
            EnsureManagementAvailable();
            return SubmitOfferCore(playerId, amount, CurrentDate, ledger.Count);
        }

        public bool CancelOffer(string offerId)
        {
            EnsureManagementAvailable();
            return CancelOfferCore(offerId, CurrentDate);
        }

        private void EnsureManagementAvailable()
        {
            if (Competition.ActiveExecution != null) throw new InvalidOperationException("Management cannot change during a match.");
            if (CurrentDate.CompareTo(EndDate) >= 0) throw new InvalidOperationException("This career calendar has ended.");
        }

        private static void ValidateTactics(CareerFormation formation, CareerMentality mentality)
        {
            if (!Enum.IsDefined(typeof(CareerFormation), formation) || !Enum.IsDefined(typeof(CareerMentality), mentality))
                throw new ArgumentException("Unknown career formation or mentality.");
        }

        private CareerTransferOffer SubmitOfferCore(string playerId, long amount, GameDate date, int submittedLedgerCount)
        {
            if (date.CompareTo(StartDate) < 0 || date.CompareTo(EndDate) >= 0)
                throw new ArgumentException("Offers require an active career date before the calendar ends.");
            if (amount < 0 || amount > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(amount));
            if (offers.Count >= MaximumOffers || offers.Count(value => value.Status == CareerTransferStatus.Pending) >= MaximumPendingOffers)
                throw new InvalidOperationException("The career offer history or pending offer limit has been reached.");
            var seller = GetCurrentClubId(playerId);
            if (seller == Competition.ControlledClubId) throw new InvalidOperationException("This player already belongs to your club.");
            if (seller != null && amount == 0) throw new ArgumentException("A registered player needs a positive transfer offer.");
            if (seller == null && amount != 0) throw new ArgumentException("A free agent has no transfer fee in this prototype.");
            if (offers.Any(value => value.PlayerId == playerId && value.Status == CareerTransferStatus.Pending))
                throw new InvalidOperationException("This player already has a pending offer.");
            if (submittedLedgerCount < 1 || submittedLedgerCount > ledger.Count ||
                ledger.Take(submittedLedgerCount).Any(value => value.Date.CompareTo(date) > 0) ||
                ledger.Skip(submittedLedgerCount).Any(value => value.Date.CompareTo(date) < 0) ||
                (offers.Count > 0 && submittedLedgerCount < offers[offers.Count - 1].SubmittedLedgerCount))
                throw new ArgumentException("An offer references an invalid ledger position.");
            // The prefix preserves whether a same-day offer preceded or followed gate income.
            var available = Math.Max(0, ledger.Take(submittedLedgerCount).Sum(value => value.Amount) - ReservedTransferBudget);
            if (amount > available) throw new InvalidOperationException("The available transfer budget cannot cover this offer.");
            var offer = new CareerTransferOffer("offer-" + (offers.Count + 1).ToString("D4", CultureInfo.InvariantCulture),
                playerId, seller, amount, date, ++transferCommandOrder, submittedLedgerCount);
            offers.Add(offer);
            return offer;
        }

        private bool CancelOfferCore(string offerId, GameDate date)
        {
            var index = offers.FindIndex(value => value.Id == offerId);
            if (index < 0) throw new ArgumentException("Unknown transfer offer.", nameof(offerId));
            if (offers[index].Status != CareerTransferStatus.Pending) return false;
            offers[index] = offers[index].Resolve(CareerTransferStatus.Cancelled, date, CareerTransferReason.Cancelled, ++transferCommandOrder);
            return true;
        }

        private void ResolveOffers(GameDate date)
        {
            for (var i = 0; i < offers.Count; i++)
            {
                var offer = offers[i];
                if (offer.Status != CareerTransferStatus.Pending || offer.SubmittedDate.CompareTo(date) >= 0) continue;
                var reason = EvaluateOffer(offer);
                var status = reason == CareerTransferReason.None ? CareerTransferStatus.Accepted : CareerTransferStatus.Rejected;
                offers[i] = offer.Resolve(status, date, reason);
                if (status == CareerTransferStatus.Accepted)
                {
                    ledger.Add(new CareerLedgerEntry("transfer-" + offer.Id, date, "transfer-fee", -offer.Amount));
                    var source = EffectiveCatalog;
                    var memberships = source.Memberships.Where(value => value.PlayerId != offer.PlayerId).ToList();
                    memberships.Add(new RosterMembership(Competition.ControlledClubId, offer.PlayerId));
                    effectiveCatalog = new DatabaseCatalog(source.DatabaseId, source.DatabaseRevision, source.Clubs, source.Players,
                        memberships, source.Competitions, source.CompetitionEditions, source.Countries, source.Stadiums, source.Snapshot);
                }
                news.Add(new CareerNewsItem("transfer-" + offer.Id, date, "gazeta-da-bola",
                    status == CareerTransferStatus.Accepted ? "transfer-accepted" : "transfer-rejected",
                    amount: offer.Amount, playerId: offer.PlayerId));
            }
        }

        private CareerTransferReason EvaluateOffer(CareerTransferOffer offer)
        {
            if (GetCurrentClubId(offer.PlayerId) != offer.SellerClubId || offer.SellerClubId == Competition.ControlledClubId)
                return CareerTransferReason.PlayerUnavailable;
            if (offer.Amount < EstimateTransferValue(offer.PlayerId)) return CareerTransferReason.BelowValuation;
            if (FinanceBalance < offer.Amount) return CareerTransferReason.InsufficientFunds;
            if (offer.SellerClubId != null)
            {
                var remaining = GetRoster(offer.SellerClubId).Where(value => value.Id != offer.PlayerId).ToArray();
                if (remaining.Length < 11) return CareerTransferReason.SellerSquadTooSmall;
                var keeper = remaining.Where(value => value.NaturalPositions.Contains(PlayerPosition.GK))
                    .OrderBy(value => value.NaturalPositions.Count == 1 ? 0 : 1).ThenBy(value => value.Id, StringComparer.Ordinal).FirstOrDefault();
                if (keeper == null) return CareerTransferReason.SellerNeedsGoalkeeper;
                if (remaining.Count(value => value.Id != keeper.Id && value.NaturalPositions.Any(position => position != PlayerPosition.GK)) < 10)
                    return CareerTransferReason.SellerSquadTooSmall;
            }
            return CareerTransferReason.None;
        }

        private void ReplayTransferCommands(GameDate date, IReadOnlyList<CareerTransferOffer> savedOffers)
        {
            var commands = new List<KeyValuePair<int, CareerTransferOffer>>();
            foreach (var offer in savedOffers)
            {
                if (offer.SubmittedDate.Equals(date)) commands.Add(new KeyValuePair<int, CareerTransferOffer>(offer.SubmissionOrder, offer));
                if (offer.Status == CareerTransferStatus.Cancelled && offer.DecisionDate.Value.Equals(date))
                    commands.Add(new KeyValuePair<int, CareerTransferOffer>(offer.CancellationOrder, offer));
            }
            foreach (var command in commands.OrderBy(value => value.Key))
            {
                if (command.Key != transferCommandOrder + 1) throw new ArgumentException("Transfer command order is inconsistent.");
                var expected = command.Value;
                try
                {
                    if (command.Key == expected.SubmissionOrder)
                    {
                        var actual = SubmitOfferCore(expected.PlayerId, expected.Amount, date, expected.SubmittedLedgerCount);
                        if (actual.Id != expected.Id || actual.SellerClubId != expected.SellerClubId)
                            throw new ArgumentException("Transfer identity or selling club is inconsistent.");
                    }
                    else if (!CancelOfferCore(expected.Id, date)) throw new ArgumentException("Only pending offers can be cancelled.");
                }
                catch (InvalidOperationException exception) { throw new ArgumentException("Transfer command history is inconsistent.", exception); }
                catch (KeyNotFoundException exception) { throw new ArgumentException("Transfer history references an unknown player.", exception); }
            }
        }

        private static bool OffersEqual(IReadOnlyList<CareerTransferOffer> expected, IReadOnlyList<CareerTransferOffer> actual)
        {
            if (expected.Count != actual.Count) return false;
            for (var i = 0; i < expected.Count; i++)
            {
                var a = expected[i]; var b = actual[i];
                if (b == null || a.Id != b.Id || a.PlayerId != b.PlayerId || a.SellerClubId != b.SellerClubId || a.Amount != b.Amount ||
                    !a.SubmittedDate.Equals(b.SubmittedDate) || a.SubmissionOrder != b.SubmissionOrder || a.SubmittedLedgerCount != b.SubmittedLedgerCount ||
                    a.Status != b.Status || !Nullable.Equals(a.DecisionDate, b.DecisionDate) || a.Reason != b.Reason || a.CancellationOrder != b.CancellationOrder) return false;
            }
            return true;
        }
    }
}
