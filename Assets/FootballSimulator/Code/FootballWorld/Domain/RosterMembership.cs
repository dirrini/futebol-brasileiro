namespace FStudio.FootballWorld.Domain
{
    public sealed class RosterMembership
    {
        public string ClubId { get; }
        public string PlayerId { get; }

        public RosterMembership(string clubId, string playerId)
        {
            ClubId = DomainValidation.Id(clubId, nameof(clubId));
            PlayerId = DomainValidation.Id(playerId, nameof(playerId));
        }
    }
}
