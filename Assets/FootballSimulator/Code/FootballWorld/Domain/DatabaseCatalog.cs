using System;
using System.Collections.Generic;

namespace FStudio.FootballWorld.Domain
{
    public sealed class DatabaseCatalog
    {
        private readonly Dictionary<string, ClubDefinition> clubsById;
        private readonly Dictionary<string, PlayerDefinition> playersById;
        private readonly Dictionary<string, IReadOnlyList<PlayerDefinition>> rostersByClubId;
        private readonly Dictionary<string, CompetitionEditionDefinition> editionsById;

        public string DatabaseId { get; }
        public int DatabaseRevision { get; }
        public IReadOnlyList<ClubDefinition> Clubs { get; }
        public IReadOnlyList<PlayerDefinition> Players { get; }
        public IReadOnlyList<RosterMembership> Memberships { get; }
        public IReadOnlyList<CompetitionDefinition> Competitions { get; }
        public IReadOnlyList<CompetitionEditionDefinition> CompetitionEditions { get; }

        public DatabaseCatalog(
            string databaseId,
            int databaseRevision,
            IEnumerable<ClubDefinition> clubs,
            IEnumerable<PlayerDefinition> players,
            IEnumerable<RosterMembership> memberships,
            IEnumerable<CompetitionDefinition> competitions = null,
            IEnumerable<CompetitionEditionDefinition> competitionEditions = null)
        {
            DatabaseId = DomainValidation.Id(databaseId, nameof(databaseId));
            DatabaseRevision = DomainValidation.InRange(databaseRevision, 1, int.MaxValue, nameof(databaseRevision));

            if (clubs == null)
            {
                throw new ArgumentNullException(nameof(clubs));
            }

            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            if (memberships == null)
            {
                throw new ArgumentNullException(nameof(memberships));
            }

            clubsById = new Dictionary<string, ClubDefinition>(StringComparer.Ordinal);
            playersById = new Dictionary<string, PlayerDefinition>(StringComparer.Ordinal);
            var clubSnapshot = new List<ClubDefinition>();
            var playerSnapshot = new List<PlayerDefinition>();
            var membershipSnapshot = new List<RosterMembership>();
            var rosterLists = new Dictionary<string, List<PlayerDefinition>>(StringComparer.Ordinal);

            foreach (var club in clubs)
            {
                if (club == null)
                {
                    throw new ArgumentException("The clubs collection cannot contain null entries.", nameof(clubs));
                }

                if (clubsById.ContainsKey(club.Id))
                {
                    throw new ArgumentException($"Duplicate club ID: {club.Id}.", nameof(clubs));
                }

                clubsById.Add(club.Id, club);
                clubSnapshot.Add(club);
                rosterLists.Add(club.Id, new List<PlayerDefinition>());
            }

            foreach (var player in players)
            {
                if (player == null)
                {
                    throw new ArgumentException("The players collection cannot contain null entries.", nameof(players));
                }

                if (playersById.ContainsKey(player.Id))
                {
                    throw new ArgumentException($"Duplicate player ID: {player.Id}.", nameof(players));
                }

                playersById.Add(player.Id, player);
                playerSnapshot.Add(player);
            }

            var assignedPlayerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var membership in memberships)
            {
                if (membership == null)
                {
                    throw new ArgumentException("The memberships collection cannot contain null entries.", nameof(memberships));
                }

                if (!rosterLists.TryGetValue(membership.ClubId, out var roster))
                {
                    throw new ArgumentException($"Unknown club ID in membership: {membership.ClubId}.", nameof(memberships));
                }

                if (!playersById.TryGetValue(membership.PlayerId, out var player))
                {
                    throw new ArgumentException($"Unknown player ID in membership: {membership.PlayerId}.", nameof(memberships));
                }

                if (!assignedPlayerIds.Add(membership.PlayerId))
                {
                    throw new ArgumentException($"A player can have only one initial club membership: {membership.PlayerId}.", nameof(memberships));
                }

                membershipSnapshot.Add(membership);
                roster.Add(player);
            }

            rostersByClubId = new Dictionary<string, IReadOnlyList<PlayerDefinition>>(StringComparer.Ordinal);
            foreach (var pair in rosterLists)
            {
                rostersByClubId.Add(pair.Key, pair.Value.AsReadOnly());
            }

            Clubs = clubSnapshot.AsReadOnly();
            Players = playerSnapshot.AsReadOnly();
            Memberships = membershipSnapshot.AsReadOnly();
            var competitionSnapshot = new List<CompetitionDefinition>(competitions ?? Array.Empty<CompetitionDefinition>());
            var editionSnapshot = new List<CompetitionEditionDefinition>(competitionEditions ?? Array.Empty<CompetitionEditionDefinition>());
            DomainValidation.InRange(competitionSnapshot.Count, 0, 128, nameof(competitions));
            DomainValidation.InRange(editionSnapshot.Count, 0, 128, nameof(competitionEditions));
            var competitionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var competition in competitionSnapshot)
                if (competition == null || !competitionIds.Add(competition.Id))
                    throw new ArgumentException("Competitions must be non-null with unique IDs.", nameof(competitions));
            editionsById = new Dictionary<string, CompetitionEditionDefinition>(StringComparer.Ordinal);
            foreach (var edition in editionSnapshot)
            {
                if (edition == null || editionsById.ContainsKey(edition.Id))
                    throw new ArgumentException("Editions must be non-null with unique IDs.", nameof(competitionEditions));
                if (!competitionIds.Contains(edition.CompetitionId))
                    throw new ArgumentException("Edition references an unknown competition.", nameof(competitionEditions));
                foreach (var clubId in edition.ParticipantClubIds)
                    if (!clubsById.ContainsKey(clubId))
                        throw new ArgumentException("Edition references an unknown club.", nameof(competitionEditions));
                editionsById.Add(edition.Id, edition);
            }
            Competitions = competitionSnapshot.AsReadOnly();
            CompetitionEditions = editionSnapshot.AsReadOnly();
        }

        public CompetitionEditionDefinition GetCompetitionEdition(string editionId)
        {
            DomainValidation.Id(editionId, nameof(editionId));
            if (!editionsById.TryGetValue(editionId, out var edition))
                throw new KeyNotFoundException($"Edition '{editionId}' does not exist in this catalog.");
            return edition;
        }

        public ClubDefinition GetClub(string clubId)
        {
            DomainValidation.Id(clubId, nameof(clubId));
            if (!clubsById.TryGetValue(clubId, out var club))
            {
                throw new KeyNotFoundException($"Club '{clubId}' does not exist in this catalog.");
            }

            return club;
        }

        public PlayerDefinition GetPlayer(string playerId)
        {
            DomainValidation.Id(playerId, nameof(playerId));
            if (!playersById.TryGetValue(playerId, out var player))
            {
                throw new KeyNotFoundException($"Player '{playerId}' does not exist in this catalog.");
            }

            return player;
        }

        public IReadOnlyList<PlayerDefinition> GetRoster(string clubId)
        {
            DomainValidation.Id(clubId, nameof(clubId));
            if (!rostersByClubId.TryGetValue(clubId, out var roster))
            {
                throw new KeyNotFoundException($"Club '{clubId}' does not exist in this catalog.");
            }

            return roster;
        }
    }
}
