using System;
using System.Collections.Generic;
using System.Linq;

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
        public IReadOnlyList<FixtureDefinition> ScheduledFixtures { get; }
        public IReadOnlyList<GameDate> PlayoffDates { get; }
        public GameDate StartDate => RoundDates[0];
        public GameDate EndDate => Rules.IsPaulista2026 ? PlayoffDates[7] : RoundDates[RoundDates.Count - 1];

        public CompetitionEditionDefinition(string id, string competitionId, string name,
            IEnumerable<string> participantClubIds, IEnumerable<GameDate> roundDates, LeagueRules rules,
            IEnumerable<FixtureDefinition> scheduledFixtures = null, IEnumerable<GameDate> playoffDates = null)
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
            var fixtures = new List<FixtureDefinition>(scheduledFixtures ?? Enumerable.Empty<FixtureDefinition>());
            var playoffs = new List<GameDate>(playoffDates ?? Enumerable.Empty<GameDate>());
            if (rules.IsPaulista2026) ValidatePaulistaSchedule(clubs, dates, fixtures, playoffs);
            else if (fixtures.Count != 0 || playoffs.Count != 0)
                throw new ArgumentException("Round-robin editions generate their own fixtures and have no playoffs.");
            ScheduledFixtures = fixtures.AsReadOnly();
            PlayoffDates = playoffs.AsReadOnly();
        }

        private static void ValidatePaulistaSchedule(List<string> clubs, List<GameDate> dates,
            List<FixtureDefinition> fixtures, List<GameDate> playoffs)
        {
            if (playoffs.Count != 8)
                throw new ArgumentException("Paulista 2026 requires four quarterfinal, two semifinal and two final dates.");
            var firstQuarter = playoffs.Take(4).Min();
            var lastQuarter = playoffs.Take(4).Max();
            var firstSemi = playoffs.Skip(4).Take(2).Min();
            var lastSemi = playoffs.Skip(4).Take(2).Max();
            if (firstQuarter.CompareTo(dates[7]) <= 0 || firstSemi.CompareTo(lastQuarter) <= 0 ||
                playoffs[6].CompareTo(lastSemi) <= 0 || playoffs[7].CompareTo(playoffs[6]) <= 0)
                throw new ArgumentException("Each playoff stage must follow completion of the preceding stage.");
            if (fixtures.Count != 64) throw new ArgumentException("Paulista 2026 requires 64 authored league fixtures.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var appearances = new HashSet<string>(StringComparer.Ordinal);
            var pairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fixture in fixtures)
            {
                if (fixture == null || !ids.Add(fixture.Id) || fixture.Round > 8 ||
                    !clubs.Contains(fixture.HomeClubId) || !clubs.Contains(fixture.AwayClubId))
                    throw new ArgumentException("Authored fixtures require unique IDs, rounds 1–8 and edition participants.");
                var next = fixture.Round == 8 ? firstQuarter : dates[fixture.Round];
                if (fixture.Date.CompareTo(dates[fixture.Round - 1]) < 0 || fixture.Date.CompareTo(next) >= 0)
                    throw new ArgumentException("A fixture date must belong to its round's authored date window.");
                if (!appearances.Add(fixture.Round + "|" + fixture.HomeClubId) ||
                    !appearances.Add(fixture.Round + "|" + fixture.AwayClubId))
                    throw new ArgumentException("A club cannot play twice in the same round.");
                var pair = string.CompareOrdinal(fixture.HomeClubId, fixture.AwayClubId) < 0
                    ? fixture.HomeClubId + "|" + fixture.AwayClubId : fixture.AwayClubId + "|" + fixture.HomeClubId;
                if (!pairs.Add(pair)) throw new ArgumentException("League opponents must be distinct.");
            }
            foreach (var club in clubs)
                if (fixtures.Count(value => value.HomeClubId == club) != 4 || fixtures.Count(value => value.AwayClubId == club) != 4)
                    throw new ArgumentException("Each participant requires four home and four away league fixtures.");
        }
    }
}
