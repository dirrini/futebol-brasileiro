using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public sealed class CompetitionStageState
    {
        public string StageId { get; }
        public string Name { get; }
        public string Kind { get; }
        public bool IsActive { get; }
        public bool IsComplete { get; }
        public IReadOnlyList<string> ParticipantClubIds { get; }
        public IReadOnlyList<CompetitionGroupDefinition> Groups { get; }
        internal CompetitionStageState(CompetitionStageDefinition stage, IEnumerable<string> participants,
            IEnumerable<CompetitionGroupDefinition> groups, bool active, bool complete)
        {
            StageId = stage.Id; Name = stage.Name; Kind = stage.Kind; IsActive = active; IsComplete = complete;
            ParticipantClubIds = participants.ToList().AsReadOnly(); Groups = groups.ToList().AsReadOnly();
        }
    }

    public sealed class CompetitionOutcomeAward
    {
        public string OutcomeId { get; }
        public string Label { get; }
        public string Kind { get; }
        public string StageId { get; }
        public string ClubId { get; }
        public int Rank { get; }
        public string GroupId { get; }
        internal CompetitionOutcomeAward(CompetitionOutcomeDefinition definition, StandingRow row, string groupId)
        {
            OutcomeId = definition.Id; Label = definition.Label; Kind = definition.Kind; StageId = definition.StageId;
            ClubId = row.ClubId; Rank = row.Rank; GroupId = groupId;
        }
    }
}
