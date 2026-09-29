using System;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public enum CareerFormation { FourFourTwo, FourThreeThree, FourTwoThreeOne }
    public enum CareerMentality { Defensive, Balanced, Attacking }
    public enum CareerTransferStatus { Pending, Accepted, Rejected, Cancelled }
    public enum CareerTransferReason
    {
        None, BelowValuation, SellerSquadTooSmall, SellerNeedsGoalkeeper,
        InsufficientFunds, PlayerUnavailable, Cancelled
    }

    // Every mutation creates a new immutable offer. Ordering and the ledger anchor
    // preserve same-day submit/cancel behavior when reconstructing a saved career.
    public sealed class CareerTransferOffer
    {
        public string Id { get; }
        public string PlayerId { get; }
        public string SellerClubId { get; }
        public long Amount { get; }
        public GameDate SubmittedDate { get; }
        public int SubmissionOrder { get; }
        public int SubmittedLedgerCount { get; }
        public CareerTransferStatus Status { get; }
        public GameDate? DecisionDate { get; }
        public CareerTransferReason Reason { get; }
        public int CancellationOrder { get; }

        public CareerTransferOffer(string id, string playerId, string sellerClubId, long amount,
            GameDate submittedDate, int submissionOrder, int submittedLedgerCount,
            CareerTransferStatus status = CareerTransferStatus.Pending, GameDate? decisionDate = null,
            CareerTransferReason reason = CareerTransferReason.None, int cancellationOrder = 0)
        {
            Id = CompetitionIdentity.Validate(id);
            PlayerId = CompetitionIdentity.Validate(playerId);
            SellerClubId = sellerClubId == null ? null : CompetitionIdentity.Validate(sellerClubId);
            if (amount < 0 || amount > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(amount));
            if (submissionOrder < 1 || submittedLedgerCount < 1) throw new ArgumentException("An offer needs its submission order and ledger anchor.");
            if (!Enum.IsDefined(typeof(CareerTransferStatus), status) || !Enum.IsDefined(typeof(CareerTransferReason), reason))
                throw new ArgumentException("Unknown offer status or reason.");
            if (status == CareerTransferStatus.Pending && (decisionDate.HasValue || reason != CareerTransferReason.None || cancellationOrder != 0))
                throw new ArgumentException("A pending offer cannot already have a decision.");
            if (status != CareerTransferStatus.Pending && (!decisionDate.HasValue || decisionDate.Value.CompareTo(submittedDate) < 0))
                throw new ArgumentException("A resolved offer needs a decision date at or after submission.");
            if ((status == CareerTransferStatus.Accepted || status == CareerTransferStatus.Rejected) && decisionDate.Value.CompareTo(submittedDate) <= 0)
                throw new ArgumentException("Offers are answered after the day they were submitted.");
            if (status == CareerTransferStatus.Cancelled && (reason != CareerTransferReason.Cancelled || cancellationOrder <= submissionOrder))
                throw new ArgumentException("A cancellation needs its command order.");
            if (status != CareerTransferStatus.Cancelled && cancellationOrder != 0)
                throw new ArgumentException("Only a cancelled offer has a cancellation order.");
            if (status == CareerTransferStatus.Accepted && reason != CareerTransferReason.None)
                throw new ArgumentException("An accepted offer cannot have a rejection reason.");
            if (status == CareerTransferStatus.Rejected && (reason == CareerTransferReason.None || reason == CareerTransferReason.Cancelled))
                throw new ArgumentException("A rejected offer needs a rejection reason.");
            Amount = amount;
            SubmittedDate = submittedDate;
            SubmissionOrder = submissionOrder;
            SubmittedLedgerCount = submittedLedgerCount;
            Status = status;
            DecisionDate = decisionDate;
            Reason = reason;
            CancellationOrder = cancellationOrder;
        }

        internal CareerTransferOffer Resolve(CareerTransferStatus status, GameDate date, CareerTransferReason reason, int cancellationOrder = 0)
            => new CareerTransferOffer(Id, PlayerId, SellerClubId, Amount, SubmittedDate, SubmissionOrder,
                SubmittedLedgerCount, status, date, reason, cancellationOrder);
    }
}
