using System.Collections.Generic;

namespace FStudio.FootballWorld.DataContracts
{
    public sealed class CompetitionData
    {
        public string Id { get; }
        public string Name { get; }
        public CompetitionData(string id, string name) { Id = id; Name = name; }
    }
    public sealed class CompetitionEditionData
    {
        public string Id { get; }
        public string CompetitionId { get; }
        public string Name { get; }
        public IReadOnlyList<string> ParticipantClubIds { get; }
        public IReadOnlyList<string> RoundDates { get; }
        public LeagueRulesData Rules { get; }
        public CompetitionEditionData(string id, string competitionId, string name,
            IEnumerable<string> participantClubIds, IEnumerable<string> roundDates, LeagueRulesData rules)
        {
            Id = id; CompetitionId = competitionId; Name = name;
            ParticipantClubIds = DataSnapshot.Copy(participantClubIds);
            RoundDates = DataSnapshot.Copy(roundDates);
            Rules = rules;
        }
    }
    public sealed class LeagueRulesData
    {
        public string Type { get; }
        public int Version { get; }
        public int Legs { get; }
        public int WinPoints { get; }
        public int DrawPoints { get; }
        public int LossPoints { get; }
        public IReadOnlyList<string> TieBreakers { get; }
        public LeagueRulesData(string type, int version, int legs, int winPoints, int drawPoints,
            int lossPoints, IEnumerable<string> tieBreakers)
        {
            Type = type; Version = version; Legs = legs;
            WinPoints = winPoints; DrawPoints = drawPoints; LossPoints = lossPoints;
            TieBreakers = DataSnapshot.Copy(tieBreakers);
        }
    }
}
