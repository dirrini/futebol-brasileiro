using System;
using System.Collections.Generic;
using System.Linq;

namespace FStudio.FootballWorld.Domain
{
    // These bounded declarations describe sporting rules, not executable scripts.
    public sealed class CompetitionFormatDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public int Version { get; }
        public int ParticipantCount { get; }
        public IReadOnlyList<CompetitionStageDefinition> Stages { get; }
        public CompetitionMatchRules MatchRules { get; }
        public IReadOnlyList<CompetitionOutcomeDefinition> Outcomes { get; }
        public string ChampionStageId { get; }
        private readonly Dictionary<string, int> stageCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, StageSourceDefinition> stageSources = new Dictionary<string, StageSourceDefinition>(StringComparer.Ordinal);

        public CompetitionFormatDefinition(string id, string name, int version, int participantCount,
            IEnumerable<CompetitionStageDefinition> stages, CompetitionMatchRules matchRules,
            IEnumerable<CompetitionOutcomeDefinition> outcomes = null, string championStageId = null, bool hasChampionStage = true)
        {
            Id = DomainValidation.Id(id, nameof(id)); Name = DomainValidation.Name(name, nameof(name));
            if (version != 1) throw new ArgumentException("Unsupported competition format version.", nameof(version));
            Version = version; ParticipantCount = DomainValidation.InRange(participantCount, 2, 64, nameof(participantCount));
            MatchRules = matchRules ?? throw new ArgumentNullException(nameof(matchRules));
            var list = new List<CompetitionStageDefinition>(stages ?? throw new ArgumentNullException(nameof(stages)));
            DomainValidation.InRange(list.Count, 1, 16, nameof(stages));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var totalRounds = 0;
            for (var index = 0; index < list.Count; index++)
            {
                var stage = list[index];
                if (stage == null || !seen.Add(stage.Id)) throw new ArgumentException("Stage IDs must be distinct and non-null.");
                var source = stage.Source ?? (index == 0 ? null : new StageSourceDefinition(list[index - 1].Id, "qualified"));
                if ((index == 0 && source != null) || (source != null && (!stageCounts.ContainsKey(source.StageId) || source.StageId == stage.Id)))
                    throw new ArgumentException("Stage sources must reference an earlier phase in topological order; the first phase uses edition participants.");
                var count = ParticipantCount;
                CompetitionStageDefinition parent = null;
                if (source != null)
                {
                    parent = list.Take(index).Single(value => value.Id == source.StageId);
                    if (source.Selection == "qualified")
                    {
                        if (parent.Qualification == null) throw new ArgumentException("A qualified source needs a qualification rule on its parent.");
                        count = parent.Qualification.Count * (parent.Qualification.Mode == "per-group" ? parent.GroupCount : 1);
                    }
                    else
                    {
                        if (parent.Kind != "knockout") throw new ArgumentException("Winner and loser sources require a knockout parent.");
                        count = stageCounts[parent.Id] / 2;
                    }
                }
                stageCounts.Add(stage.Id, count); stageSources.Add(stage.Id, source);
                if (count < 2 || count % stage.GroupCount != 0) throw new ArgumentException("Stages require equal, complete groups and at least two participants.");
                if (stage.Kind == "knockout" && count % 2 != 0)
                    throw new ArgumentException("Knockout stages require an even field.");
                if (stage.Kind == "league" && count / stage.GroupCount < 2)
                    throw new ArgumentException("League groups require at least two participants.");
                if (stage.Opponents == "cross-group" && stage.GroupCount < 2)
                    throw new ArgumentException("Cross-group league scheduling requires multiple groups.");
                if (stage.Pairing == "cross-group")
                {
                    if (parent == null || parent.Kind != "league" || parent.GroupCount < 2 ||
                        parent.Qualification?.Mode != "per-group" || source.Selection != "qualified")
                        throw new ArgumentException("Cross-group knockout pairing requires qualifiers from multiple equal league groups.");
                }
                totalRounds += stage.RoundCount;
                if (totalRounds > 2048) throw new ArgumentException("A format exceeds the supported round budget.");
                var qualification = stage.Qualification;
                if (qualification != null)
                {
                    foreach (var reference in qualification.RankingStageIds)
                        if (reference != stage.Id && !IsAncestor(reference, stage.Id))
                            throw new ArgumentException("Ranking may reference only the current stage or a source ancestor.");
                    if (stage.Kind == "knockout" && (qualification.Mode != "winners" || qualification.Count != count / 2))
                        throw new ArgumentException("Knockout qualification must select every tie winner.");
                    if (stage.Kind == "league" && qualification.Mode == "winners")
                        throw new ArgumentException("League stages qualify by overall or group standings.");
                    var next = qualification.Mode == "per-group" ? qualification.Count * stage.GroupCount : qualification.Count;
                    if (next > count) throw new ArgumentException("Qualification cannot select more clubs than the stage contains.");
                }
            }
            if (!hasChampionStage && championStageId != null) throw new ArgumentException("A format without a title cannot designate a champion stage.");
            ChampionStageId = hasChampionStage ? championStageId ?? list.Last().Id : null;
            if (ChampionStageId != null)
            {
                var championStage = list.SingleOrDefault(value => value.Id == ChampionStageId);
                if (championStage == null || (championStage.Kind == "knockout" && stageCounts[ChampionStageId] != 2))
                    throw new ArgumentException("The champion stage must be a league or a two-club knockout final.");
            }
            var awards = new List<CompetitionOutcomeDefinition>(outcomes ?? Enumerable.Empty<CompetitionOutcomeDefinition>());
            DomainValidation.InRange(awards.Count, 0, 128, nameof(outcomes));
            var awardIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var outcome in awards)
            {
                if (outcome == null || !awardIds.Add(outcome.Id) || !seen.Contains(outcome.StageId))
                    throw new ArgumentException("Outcomes require unique IDs and a known stage.");
                var stage = list.Single(value => value.Id == outcome.StageId);
                if (outcome.Ranking == "per-group" && stage.Kind != "league") throw new ArgumentException("Group outcomes require a league stage.");
                var stageCount = stageCounts[stage.Id];
                if (outcome.ToRank > stageCount / (outcome.Ranking == "per-group" ? stage.GroupCount : 1))
                    throw new ArgumentException("Outcome rank exceeds its standings table.");
            }
            Stages = list.AsReadOnly(); Outcomes = awards.AsReadOnly();
        }

        public int GetStageParticipantCount(string stageId) => stageCounts.TryGetValue(stageId, out var count) ? count : throw new ArgumentException("Unknown stage.", nameof(stageId));
        public StageSourceDefinition GetStageSource(string stageId) => stageSources.TryGetValue(stageId, out var source) ? source : throw new ArgumentException("Unknown stage.", nameof(stageId));
        public bool IsAncestor(string ancestorStageId, string stageId)
        {
            while (stageSources.TryGetValue(stageId, out var source) && source != null)
            {
                if (source.StageId == ancestorStageId) return true;
                stageId = source.StageId;
            }
            return false;
        }
    }

    public sealed class CompetitionStageDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Kind { get; }
        public int GroupCount { get; }
        public string Opponents { get; }
        public int Legs { get; }
        public int RoundCount { get; }
        public CompetitionPointsDefinition Points { get; }
        public IReadOnlyList<string> TieBreakers { get; }
        public StageQualificationDefinition Qualification { get; }
        public string Pairing { get; }
        public string Venue { get; }
        public bool AwayGoals { get; }
        public string TiedWinner { get; }
        public StageSourceDefinition Source { get; }

        public CompetitionStageDefinition(string id, string name, string kind, int groupCount, string opponents,
            int legs, int roundCount, CompetitionPointsDefinition points, IEnumerable<string> tieBreakers,
            StageQualificationDefinition qualification = null, string pairing = "seeded", string venue = "seeded",
            bool awayGoals = false, string tiedWinner = "penalties", StageSourceDefinition source = null)
        {
            Id = DomainValidation.Id(id, nameof(id)); Name = DomainValidation.Name(name, nameof(name));
            Kind = CompetitionRuleValidation.OneOf(kind, nameof(kind), "league", "knockout");
            GroupCount = DomainValidation.InRange(groupCount, 1, 32, nameof(groupCount));
            Opponents = CompetitionRuleValidation.OneOf(opponents, nameof(opponents), "all", "same-group", "cross-group", "authored");
            Legs = DomainValidation.InRange(legs, 1, 2, nameof(legs));
            RoundCount = DomainValidation.InRange(roundCount, 1, 128, nameof(roundCount));
            Points = points ?? throw new ArgumentNullException(nameof(points));
            var order = new List<string>(tieBreakers ?? throw new ArgumentNullException(nameof(tieBreakers)));
            if (order.Count == 0 || order.Count > 6 || order.Distinct(StringComparer.Ordinal).Count() != order.Count || order.Last() != "seeded-draw")
                throw new ArgumentException("Ordered, unique tiebreakers must finish with seeded-draw.", nameof(tieBreakers));
            foreach (var rule in order) CompetitionRuleValidation.OneOf(rule, nameof(tieBreakers),
                "wins", "goal-difference", "goals-for", "red-cards", "yellow-cards", "seeded-draw");
            TieBreakers = order.AsReadOnly(); Qualification = qualification;
            Pairing = CompetitionRuleValidation.OneOf(pairing, nameof(pairing), "seeded", "draw", "cross-group");
            Venue = CompetitionRuleValidation.OneOf(venue, nameof(venue), "seeded", "first-listed", "draw", "neutral");
            AwayGoals = awayGoals; TiedWinner = CompetitionRuleValidation.OneOf(tiedWinner, nameof(tiedWinner), "penalties", "higher-seed");
            Source = source;
            if (kind == "knockout" && (groupCount != 1 || opponents != "all" || roundCount != legs))
                throw new ArgumentException("A knockout stage is one round of ties with one date per leg and no league groups.");
            if (awayGoals && (kind != "knockout" || legs != 2 || venue == "neutral"))
                throw new ArgumentException("Away goals require two knockout legs at club home venues.");
            if (tiedWinner == "higher-seed" && (kind != "knockout" || pairing != "seeded"))
                throw new ArgumentException("Higher-seed ties require seeded knockout pairing.");
            if (kind == "league" && (pairing != "seeded" || tiedWinner != "penalties"))
                throw new ArgumentException("Knockout-only controls must retain their explicit defaults in league stages.");
        }
    }

    public sealed class StageSourceDefinition
    {
        public string StageId { get; }
        public string Selection { get; }
        public StageSourceDefinition(string stageId, string selection)
        {
            StageId = DomainValidation.Id(stageId, nameof(stageId));
            Selection = CompetitionRuleValidation.OneOf(selection, nameof(selection), "qualified", "winners", "losers");
        }
    }

    public sealed class CompetitionPointsDefinition
    {
        public int Win { get; }
        public int Draw { get; }
        public int Loss { get; }
        public CompetitionPointsDefinition(int win, int draw, int loss)
        {
            Win = DomainValidation.InRange(win, 0, 100, nameof(win)); Draw = DomainValidation.InRange(draw, 0, 100, nameof(draw));
            Loss = DomainValidation.InRange(loss, 0, 100, nameof(loss));
            if (win <= draw || draw < loss) throw new ArgumentException("Points require win > draw >= loss.");
        }
    }

    public sealed class StageQualificationDefinition
    {
        public string Mode { get; }
        public int Count { get; }
        public IReadOnlyList<string> RankingStageIds { get; }
        public StageQualificationDefinition(string mode, int count, IEnumerable<string> rankingStageIds = null)
        {
            Mode = CompetitionRuleValidation.OneOf(mode, nameof(mode), "overall", "per-group", "winners");
            Count = DomainValidation.InRange(count, 1, 64, nameof(count));
            RankingStageIds = CompetitionRuleValidation.Ids(rankingStageIds, nameof(rankingStageIds));
        }
    }

    public sealed class CompetitionMatchRules
    {
        public int MaxSubstitutions { get; }
        public CompetitionMatchRules(int maxSubstitutions) { MaxSubstitutions = DomainValidation.InRange(maxSubstitutions, 0, 11, nameof(maxSubstitutions)); }
    }

    public sealed class CompetitionOutcomeDefinition
    {
        public string Id { get; }
        public string Label { get; }
        public string StageId { get; }
        public string Kind { get; }
        public string Ranking { get; }
        public int FromRank { get; }
        public int ToRank { get; }
        public CompetitionOutcomeDefinition(string id, string label, string stageId, string kind, string ranking, int fromRank, int toRank)
        {
            Id = DomainValidation.Id(id, nameof(id)); Label = DomainValidation.Name(label, nameof(label)); StageId = DomainValidation.Id(stageId, nameof(stageId));
            Kind = CompetitionRuleValidation.OneOf(kind, nameof(kind), "qualification", "promotion", "relegation");
            Ranking = CompetitionRuleValidation.OneOf(ranking, nameof(ranking), "overall", "per-group");
            FromRank = DomainValidation.InRange(fromRank, 1, 64, nameof(fromRank)); ToRank = DomainValidation.InRange(toRank, fromRank, 64, nameof(toRank));
        }
    }

    internal static class CompetitionRuleValidation
    {
        internal static string StateCode(string value, string parameter)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 8 || value.Any(character =>
                (character < 'A' || character > 'Z') && (character < '0' || character > '9')))
                throw new ArgumentException("State codes require one to eight uppercase ASCII letters or digits.", parameter);
            return value;
        }
        internal static string OneOf(string value, string parameter, params string[] allowed)
        {
            if (!allowed.Contains(value, StringComparer.Ordinal)) throw new ArgumentException("Unsupported " + parameter + ": " + value, parameter);
            return value;
        }
        internal static IReadOnlyList<string> Ids(IEnumerable<string> source, string parameter)
        {
            var values = new List<string>(source ?? Enumerable.Empty<string>());
            if (values.Count > 1024 || values.Any(value => DomainValidation.Id(value, parameter) == null) || values.Distinct(StringComparer.Ordinal).Count() != values.Count)
                throw new ArgumentException("IDs must be unique.", parameter);
            return values.AsReadOnly();
        }
    }
}
