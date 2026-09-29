using System.Collections.Generic;

namespace FStudio.FootballWorld.DataContracts
{
    public sealed class CompetitionData
    {
        public string Id { get; }
        public string Name { get; }
        public string DefaultFormatId { get; }
        public string Level { get; }
        public int? Reputation { get; }
        public int? PrizeLevel { get; }
        public string LogoUri { get; }
        public string TrophyImageUri { get; }
        public string TrophyModelUri { get; }
        public CompetitionEligibilityData Eligibility { get; }
        public IReadOnlyList<CompetitionQualificationRouteData> QualificationRoutes { get; }
        public CompetitionPrizesData Prizes { get; }
        public CompetitionData(string id, string name, string defaultFormatId = null, string level = null,
            int? reputation = null, int? prizeLevel = null, string logoUri = null, string trophyImageUri = null,
            string trophyModelUri = null, CompetitionEligibilityData eligibility = null,
            IEnumerable<CompetitionQualificationRouteData> qualificationRoutes = null, CompetitionPrizesData prizes = null)
        {
            Id = id; Name = name; DefaultFormatId = defaultFormatId; Level = level; Reputation = reputation;
            PrizeLevel = prizeLevel; LogoUri = logoUri; TrophyImageUri = trophyImageUri;
            TrophyModelUri = trophyModelUri; Eligibility = eligibility;
            QualificationRoutes = DataSnapshot.Copy(qualificationRoutes ?? new CompetitionQualificationRouteData[0]); Prizes = prizes;
        }
    }
    public sealed class CompetitionEligibilityData
    {
        public IReadOnlyList<string> CountryCodes { get; }
        public IReadOnlyList<string> StateCodes { get; }
        public IReadOnlyList<string> AllowedClubIds { get; }
        public IReadOnlyList<string> ExcludedClubIds { get; }
        public CompetitionEligibilityData(IEnumerable<string> countryCodes, IEnumerable<string> stateCodes,
            IEnumerable<string> allowedClubIds, IEnumerable<string> excludedClubIds)
        {
            CountryCodes = DataSnapshot.Copy(countryCodes); StateCodes = DataSnapshot.Copy(stateCodes);
            AllowedClubIds = DataSnapshot.Copy(allowedClubIds); ExcludedClubIds = DataSnapshot.Copy(excludedClubIds);
        }
    }
    public sealed class CompetitionEditionData
    {
        public string Id { get; }
        public string CompetitionId { get; }
        public string Name { get; }
        public IReadOnlyList<string> ParticipantClubIds { get; }
        public IReadOnlyList<string> RoundDates { get; }
        public LeagueRulesData Rules { get; }
        public IReadOnlyList<AuthoredFixtureData> AuthoredFixtures { get; }
        public IReadOnlyList<string> PlayoffDates { get; }
        public string FormatId { get; }
        public IReadOnlyList<CompetitionStageScheduleData> StageSchedules { get; }
        public CompetitionEditionData(string id, string competitionId, string name,
            IEnumerable<string> participantClubIds, IEnumerable<string> roundDates, LeagueRulesData rules,
            IEnumerable<AuthoredFixtureData> authoredFixtures = null, IEnumerable<string> playoffDates = null)
        {
            Id = id; CompetitionId = competitionId; Name = name;
            ParticipantClubIds = DataSnapshot.Copy(participantClubIds);
            RoundDates = DataSnapshot.Copy(roundDates);
            Rules = rules;
            AuthoredFixtures = DataSnapshot.Copy(authoredFixtures ?? new AuthoredFixtureData[0]);
            PlayoffDates = DataSnapshot.Copy(playoffDates ?? new string[0]);
            StageSchedules = DataSnapshot.Copy(new CompetitionStageScheduleData[0]);
        }
        public CompetitionEditionData(string id, string competitionId, string name, IEnumerable<string> participantClubIds,
            string formatId, IEnumerable<CompetitionStageScheduleData> stageSchedules)
            : this(id, competitionId, name, participantClubIds, new string[0], null)
        { FormatId = formatId; StageSchedules = DataSnapshot.Copy(stageSchedules); }
    }
    public sealed class AuthoredFixtureData
    {
        public string Id { get; }
        public int Round { get; }
        public string Date { get; }
        public string HomeClubId { get; }
        public string AwayClubId { get; }
        public string StadiumId { get; }
        public AuthoredFixtureData(string id, int round, string date, string homeClubId, string awayClubId, string stadiumId = null)
        {
            Id = id; Round = round; Date = date; HomeClubId = homeClubId; AwayClubId = awayClubId; StadiumId = stadiumId;
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
