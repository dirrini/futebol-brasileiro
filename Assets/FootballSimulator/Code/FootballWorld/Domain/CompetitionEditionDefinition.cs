using System;
using System.Collections.Generic;

namespace FStudio.FootballWorld.Domain
{
    public sealed class CompetitionEditionDefinition
    {
        public string Id { get; }
        public string CompetitionId { get; }
        public string Name { get; }
        public IReadOnlyList<string> ParticipantClubIds { get; }
        public IReadOnlyList<GameDate> RoundDates { get; }
        public LeagueRules Rules { get; }
        public GameDate StartDate => RoundDates[0];
        public GameDate EndDate => RoundDates[RoundDates.Count - 1];

        public CompetitionEditionDefinition(string id, string competitionId, string name,
            IEnumerable<string> participantClubIds, IEnumerable<GameDate> roundDates, LeagueRules rules)
        {
            Id = DomainValidation.Id(id, nameof(id));
            CompetitionId = DomainValidation.Id(competitionId, nameof(competitionId));
            Name = DomainValidation.Name(name, nameof(name));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            var clubs = new List<string>(participantClubIds ?? throw new ArgumentNullException(nameof(participantClubIds)));
            DomainValidation.InRange(clubs.Count, 2, 64, nameof(participantClubIds));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var club in clubs)
                if (!seen.Add(DomainValidation.Id(club, nameof(participantClubIds))))
                    throw new ArgumentException("Edition participants must be unique.", nameof(participantClubIds));
            var dates = new List<GameDate>(roundDates ?? throw new ArgumentNullException(nameof(roundDates)));
            if (dates.Count != rules.RoundCount(clubs.Count))
                throw new ArgumentException("The edition must supply one date per round.", nameof(roundDates));
            for (var i = 1; i < dates.Count; i++)
                if (dates[i].CompareTo(dates[i - 1]) <= 0)
                    throw new ArgumentException("Round dates must be strictly increasing.", nameof(roundDates));
            ParticipantClubIds = clubs.AsReadOnly();
            RoundDates = dates.AsReadOnly();
        }
    }
}
