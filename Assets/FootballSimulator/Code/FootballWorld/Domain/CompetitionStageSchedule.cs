using System;
using System.Collections.Generic;
using System.Linq;

namespace FStudio.FootballWorld.Domain
{
    public sealed class CompetitionStageSchedule
    {
        public string StageId { get; }
        public IReadOnlyList<GameDate> RoundDates { get; }
        public IReadOnlyList<CompetitionGroupDefinition> Groups { get; }
        public IReadOnlyList<FixtureDefinition> AuthoredFixtures { get; }
        public string NeutralStadiumId { get; }
        public IReadOnlyList<GameDate> FixtureDates { get; }
        public CompetitionStageSchedule(string stageId, IEnumerable<GameDate> roundDates,
            IEnumerable<CompetitionGroupDefinition> groups = null, IEnumerable<FixtureDefinition> authoredFixtures = null,
            string neutralStadiumId = null, IEnumerable<GameDate> fixtureDates = null)
        {
            StageId = DomainValidation.Id(stageId, nameof(stageId));
            var dates = new List<GameDate>(roundDates ?? throw new ArgumentNullException(nameof(roundDates)));
            DomainValidation.InRange(dates.Count, 1, 128, nameof(roundDates));
            for (var index = 1; index < dates.Count; index++)
                if (dates[index].CompareTo(dates[index - 1]) <= 0) throw new ArgumentException("Round dates must strictly increase.");
            var groupList = new List<CompetitionGroupDefinition>(groups ?? Enumerable.Empty<CompetitionGroupDefinition>());
            if (groupList.Any(value => value == null) || groupList.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != groupList.Count)
                throw new ArgumentException("Group IDs must be distinct and non-null.");
            var fixtures = new List<FixtureDefinition>(authoredFixtures ?? Enumerable.Empty<FixtureDefinition>());
            if (fixtures.Count > 4096 || fixtures.Any(value => value == null) || fixtures.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != fixtures.Count)
                throw new ArgumentException("Authored fixture IDs must be distinct and non-null.");
            RoundDates = dates.AsReadOnly(); Groups = groupList.AsReadOnly(); AuthoredFixtures = fixtures.AsReadOnly();
            NeutralStadiumId = neutralStadiumId == null ? null : DomainValidation.Id(neutralStadiumId, nameof(neutralStadiumId));
            FixtureDates = new List<GameDate>(fixtureDates ?? Enumerable.Empty<GameDate>()).AsReadOnly();
        }
    }

    public sealed class CompetitionGroupDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<string> ClubIds { get; }
        public IReadOnlyList<int> SeedRanks { get; }
        public CompetitionGroupDefinition(string id, string name, IEnumerable<string> clubIds = null, IEnumerable<int> seedRanks = null)
        {
            Id = DomainValidation.Id(id, nameof(id)); Name = DomainValidation.Name(name, nameof(name));
            ClubIds = CompetitionRuleValidation.Ids(clubIds, nameof(clubIds));
            var ranks = new List<int>(seedRanks ?? Enumerable.Empty<int>());
            foreach (var rank in ranks) DomainValidation.InRange(rank, 1, 64, nameof(seedRanks));
            if (ranks.Distinct().Count() != ranks.Count || (ranks.Count > 0 && ClubIds.Count > 0))
                throw new ArgumentException("Groups use either distinct seed ranks or distinct club IDs.");
            SeedRanks = ranks.AsReadOnly();
        }
    }
}
