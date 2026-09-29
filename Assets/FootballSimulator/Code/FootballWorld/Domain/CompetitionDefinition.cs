using System;
using System.Collections.Generic;
using System.Linq;

namespace FStudio.FootballWorld.Domain
{
    public sealed class CompetitionDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string DefaultFormatId { get; }
        public string Level { get; }
        public int Reputation { get; }
        public int PrizeLevel { get; }
        public string LogoUri { get; }
        public string TrophyImageUri { get; }
        public string TrophyModelUri { get; }
        public CompetitionEligibilityDefinition Eligibility { get; }
        public IReadOnlyList<CompetitionQualificationRoute> QualificationRoutes { get; }
        public CompetitionPrizeDefinition Prizes { get; }
        public CompetitionDefinition(string id, string name, string defaultFormatId = null, string level = null,
            int reputation = 0, int prizeLevel = 0, string logoUri = null, string trophyImageUri = null,
            string trophyModelUri = null, CompetitionEligibilityDefinition eligibility = null,
            IEnumerable<CompetitionQualificationRoute> qualificationRoutes = null, CompetitionPrizeDefinition prizes = null)
        {
            Id = DomainValidation.Id(id, nameof(id));
            Name = DomainValidation.Name(name, nameof(name));
            DefaultFormatId = defaultFormatId == null ? null : DomainValidation.Id(defaultFormatId, nameof(defaultFormatId));
            Level = level == null ? null : CompetitionRuleValidation.OneOf(level, nameof(level), "state", "regional", "national", "continental", "world");
            Reputation = DomainValidation.InRange(reputation, 0, 100, nameof(reputation));
            PrizeLevel = DomainValidation.InRange(prizeLevel, 0, 100, nameof(prizeLevel));
            LogoUri = MediaUri(logoUri, nameof(logoUri)); TrophyImageUri = MediaUri(trophyImageUri, nameof(trophyImageUri));
            TrophyModelUri = MediaUri(trophyModelUri, nameof(trophyModelUri));
            Eligibility = eligibility ?? new CompetitionEligibilityDefinition();
            var routes = new List<CompetitionQualificationRoute>(qualificationRoutes ?? Enumerable.Empty<CompetitionQualificationRoute>());
            if (routes.Count > 128 || routes.Any(value => value == null) || routes.Select(value => value.OutcomeId).Distinct(StringComparer.Ordinal).Count() != routes.Count)
                throw new ArgumentException("Qualification routes require distinct outcomes.");
            QualificationRoutes = routes.AsReadOnly(); Prizes = prizes;
        }

        private static string MediaUri(string value, string parameter)
        {
            if (value == null) return null;
            DomainValidation.Text(value, 2048, false, parameter);
            if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo))
                    throw new ArgumentException("Media references require HTTPS or safe package paths.", parameter);
            }
            else if (value.StartsWith("/", StringComparison.Ordinal) || value.Contains(":") || value.Contains("\\") ||
                value.Split('/').Any(part => part == ".." || part == "." || part.Length == 0) || value.Contains("?") || value.Contains("#"))
                throw new ArgumentException("Media references require HTTPS or safe package paths.", parameter);
            return value;
        }
    }

    public sealed class CompetitionEligibilityDefinition
    {
        public IReadOnlyList<string> CountryCodes { get; }
        public IReadOnlyList<string> StateCodes { get; }
        public IReadOnlyList<string> AllowedClubIds { get; }
        public IReadOnlyList<string> ExcludedClubIds { get; }
        public CompetitionEligibilityDefinition(IEnumerable<string> countryCodes = null, IEnumerable<string> stateCodes = null,
            IEnumerable<string> allowedClubIds = null, IEnumerable<string> excludedClubIds = null)
        {
            CountryCodes = Codes(countryCodes, nameof(countryCodes)); StateCodes = Codes(stateCodes, nameof(stateCodes), true);
            if (StateCodes.Count > 0 && CountryCodes.Count != 1) throw new ArgumentException("State eligibility requires exactly one country.");
            AllowedClubIds = CompetitionRuleValidation.Ids(allowedClubIds, nameof(allowedClubIds));
            ExcludedClubIds = CompetitionRuleValidation.Ids(excludedClubIds, nameof(excludedClubIds));
        }
        private static IReadOnlyList<string> Codes(IEnumerable<string> source, string parameter, bool state = false)
        {
            var values = new List<string>(source ?? Enumerable.Empty<string>());
            foreach (var value in values) { if (state) CompetitionRuleValidation.StateCode(value, parameter); else DomainValidation.Code(value, 2, parameter); }
            if (values.Distinct(StringComparer.Ordinal).Count() != values.Count) throw new ArgumentException("Eligibility codes must be unique.");
            return values.AsReadOnly();
        }
        public bool Allows(ClubDefinition club) => club != null && !ExcludedClubIds.Contains(club.Id) &&
            (AllowedClubIds.Count == 0 || AllowedClubIds.Contains(club.Id)) &&
            (CountryCodes.Count == 0 || CountryCodes.Contains(club.CountryCode)) &&
            (StateCodes.Count == 0 || StateCodes.Contains(club.StateCode));
    }

    public sealed class CompetitionQualificationRoute
    {
        public string OutcomeId { get; }
        public string TargetCompetitionId { get; }
        public CompetitionQualificationRoute(string outcomeId, string targetCompetitionId)
        { OutcomeId = DomainValidation.Id(outcomeId, nameof(outcomeId)); TargetCompetitionId = DomainValidation.Id(targetCompetitionId, nameof(targetCompetitionId)); }
    }

    public sealed class CompetitionPrizeDefinition
    {
        public string Currency { get; }
        public int Participation { get; }
        public int Win { get; }
        public int Draw { get; }
        public IReadOnlyList<CompetitionRankingAward> RankingAwards { get; }
        public CompetitionPrizeDefinition(string currency, int participation, int win, int draw, IEnumerable<CompetitionRankingAward> rankingAwards = null)
        {
            Currency = DomainValidation.Code(currency, 3, nameof(currency));
            Participation = DomainValidation.InRange(participation, 0, int.MaxValue, nameof(participation));
            Win = DomainValidation.InRange(win, 0, int.MaxValue, nameof(win)); Draw = DomainValidation.InRange(draw, 0, int.MaxValue, nameof(draw));
            var list = new List<CompetitionRankingAward>(rankingAwards ?? Enumerable.Empty<CompetitionRankingAward>());
            if (list.Count > 128 || list.Any(value => value == null)) throw new ArgumentException("Invalid ranking prize list.");
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            foreach (var award in list)
                for (var rank = award.FromRank; rank <= award.ToRank; rank++)
                    if (!occupied.Add(award.StageId + "|" + award.Ranking + "|" + rank)) throw new ArgumentException("Ranking prize ranges cannot overlap.");
            RankingAwards = list.AsReadOnly();
        }
    }

    public sealed class CompetitionRankingAward
    {
        public string StageId { get; }
        public string Ranking { get; }
        public int FromRank { get; }
        public int ToRank { get; }
        public int Amount { get; }
        public CompetitionRankingAward(string stageId, string ranking, int fromRank, int toRank, int amount)
        {
            StageId = DomainValidation.Id(stageId, nameof(stageId)); Ranking = CompetitionRuleValidation.OneOf(ranking, nameof(ranking), "overall", "per-group");
            FromRank = DomainValidation.InRange(fromRank, 1, 64, nameof(fromRank)); ToRank = DomainValidation.InRange(toRank, fromRank, 64, nameof(toRank));
            Amount = DomainValidation.InRange(amount, 0, int.MaxValue, nameof(amount));
        }
    }
}
