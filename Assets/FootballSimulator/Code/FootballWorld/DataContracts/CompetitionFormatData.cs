using System.Collections.Generic;

namespace FStudio.FootballWorld.DataContracts
{
    public sealed class CompetitionFormatData
    {
        public string Id { get; }
        public string Name { get; }
        public int Version { get; }
        public int ParticipantCount { get; }
        public int MaxSubstitutions { get; }
        public IReadOnlyList<CompetitionStageData> Stages { get; }
        public IReadOnlyList<CompetitionOutcomeData> Outcomes { get; }
        public string ChampionStageId { get; }
        public bool HasChampionStage { get; }
        public CompetitionFormatData(string id, string name, int version, int participantCount, int maxSubstitutions,
            IEnumerable<CompetitionStageData> stages, IEnumerable<CompetitionOutcomeData> outcomes, string championStageId = null, bool hasChampionStage = true)
        { Id = id; Name = name; Version = version; ParticipantCount = participantCount; MaxSubstitutions = maxSubstitutions; Stages = DataSnapshot.Copy(stages); Outcomes = DataSnapshot.Copy(outcomes); ChampionStageId = championStageId; HasChampionStage = hasChampionStage; }
    }
    public sealed class CompetitionStageData
    {
        public string Id { get; }
        public string Name { get; }
        public string Kind { get; }
        public int GroupCount { get; }
        public string Opponents { get; }
        public int Legs { get; }
        public int RoundCount { get; }
        public int WinPoints { get; }
        public int DrawPoints { get; }
        public int LossPoints { get; }
        public IReadOnlyList<string> TieBreakers { get; }
        public string QualificationMode { get; }
        public int QualificationCount { get; }
        public IReadOnlyList<string> RankingStageIds { get; }
        public string Pairing { get; }
        public string Venue { get; }
        public bool AwayGoals { get; }
        public string TiedWinner { get; }
        public CompetitionStageSourceData Source { get; }
        public CompetitionStageData(string id, string name, string kind, int groupCount, string opponents, int legs, int roundCount,
            int winPoints, int drawPoints, int lossPoints, IEnumerable<string> tieBreakers, string qualificationMode,
            int qualificationCount, string pairing, string venue, bool awayGoals, string tiedWinner, IEnumerable<string> rankingStageIds = null,
            CompetitionStageSourceData source = null)
        {
            Id = id; Name = name; Kind = kind; GroupCount = groupCount; Opponents = opponents; Legs = legs; RoundCount = roundCount;
            WinPoints = winPoints; DrawPoints = drawPoints; LossPoints = lossPoints; TieBreakers = DataSnapshot.Copy(tieBreakers);
            QualificationMode = qualificationMode; QualificationCount = qualificationCount; Pairing = pairing; Venue = venue;
            AwayGoals = awayGoals; TiedWinner = tiedWinner;
            RankingStageIds = DataSnapshot.Copy(rankingStageIds ?? new string[0]);
            Source = source;
        }
    }
    public sealed class CompetitionStageSourceData
    {
        public string StageId { get; }
        public string Selection { get; }
        public CompetitionStageSourceData(string stageId, string selection) { StageId = stageId; Selection = selection; }
    }
    public sealed class CompetitionOutcomeData
    {
        public string Id { get; }
        public string Label { get; }
        public string StageId { get; }
        public string Kind { get; }
        public string Ranking { get; }
        public int FromRank { get; }
        public int ToRank { get; }
        public CompetitionOutcomeData(string id, string label, string stageId, string kind, string ranking, int fromRank, int toRank)
        { Id = id; Label = label; StageId = stageId; Kind = kind; Ranking = ranking; FromRank = fromRank; ToRank = toRank; }
    }
    public sealed class CompetitionStageScheduleData
    {
        public string StageId { get; }
        public IReadOnlyList<string> RoundDates { get; }
        public IReadOnlyList<CompetitionGroupData> Groups { get; }
        public IReadOnlyList<AuthoredFixtureData> AuthoredFixtures { get; }
        public string NeutralStadiumId { get; }
        public IReadOnlyList<string> FixtureDates { get; }
        public CompetitionStageScheduleData(string stageId, IEnumerable<string> roundDates, IEnumerable<CompetitionGroupData> groups,
            IEnumerable<AuthoredFixtureData> authoredFixtures, string neutralStadiumId = null, IEnumerable<string> fixtureDates = null)
        { StageId = stageId; RoundDates = DataSnapshot.Copy(roundDates); Groups = DataSnapshot.Copy(groups); AuthoredFixtures = DataSnapshot.Copy(authoredFixtures); NeutralStadiumId = neutralStadiumId; FixtureDates = DataSnapshot.Copy(fixtureDates ?? new string[0]); }
    }
    public sealed class CompetitionGroupData
    {
        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<string> ClubIds { get; }
        public IReadOnlyList<int> SeedRanks { get; }
        public CompetitionGroupData(string id, string name, IEnumerable<string> clubIds = null, IEnumerable<int> seedRanks = null)
        { Id = id; Name = name; ClubIds = DataSnapshot.Copy(clubIds ?? new string[0]); SeedRanks = DataSnapshot.Copy(seedRanks ?? new int[0]); }
    }
    public sealed class CompetitionQualificationRouteData
    {
        public string OutcomeId { get; }
        public string TargetCompetitionId { get; }
        public CompetitionQualificationRouteData(string outcomeId, string targetCompetitionId)
        { OutcomeId = outcomeId; TargetCompetitionId = targetCompetitionId; }
    }
    public sealed class CompetitionPrizesData
    {
        public string Currency { get; }
        public int Participation { get; }
        public int Win { get; }
        public int Draw { get; }
        public IReadOnlyList<CompetitionRankingAwardData> RankingAwards { get; }
        public CompetitionPrizesData(string currency, int participation, int win, int draw, IEnumerable<CompetitionRankingAwardData> rankingAwards)
        { Currency = currency; Participation = participation; Win = win; Draw = draw; RankingAwards = DataSnapshot.Copy(rankingAwards); }
    }
    public sealed class CompetitionRankingAwardData
    {
        public string StageId { get; }
        public string Ranking { get; }
        public int FromRank { get; }
        public int ToRank { get; }
        public int Amount { get; }
        public CompetitionRankingAwardData(string stageId, string ranking, int fromRank, int toRank, int amount)
        { StageId = stageId; Ranking = ranking; FromRank = fromRank; ToRank = toRank; Amount = amount; }
    }
}
