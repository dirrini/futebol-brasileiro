using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public sealed partial class CompetitionSession
    {
        private readonly List<ActiveStage> activatedStages = new List<ActiveStage>();
        public string CurrentStageId => !Edition.IsDeclarative ? null : fixtures.FirstOrDefault(value => !resultsById.ContainsKey(value.Id))?.StageId
            ?? Edition.Format.ChampionStageId ?? activatedStages.Last().Definition.Id;
        public IReadOnlyList<CompetitionStageState> StageStates => !Edition.IsDeclarative ? (IReadOnlyList<CompetitionStageState>)Array.Empty<CompetitionStageState>() :
            Edition.Format.Stages.Select(definition =>
            {
                var active = activatedStages.SingleOrDefault(value => value.Definition.Id == definition.Id);
                var complete = active != null && IsStageComplete(definition.Id);
                return new CompetitionStageState(definition, active?.Participants ?? (IEnumerable<string>)Array.Empty<string>(),
                    active?.Groups ?? (IEnumerable<CompetitionGroupDefinition>)Array.Empty<CompetitionGroupDefinition>(), active != null && !complete, complete);
            }).ToList().AsReadOnly();

        public bool IsStageComplete(string stageId)
        {
            RequireDeclaredStage(stageId);
            return activatedStages.Any(value => value.Definition.Id == stageId) &&
                fixtures.Where(value => value.StageId == stageId).All(value => resultsById.ContainsKey(value.Id));
        }

        public IReadOnlyList<string> GetStageGroupIds(string stageId)
        {
            RequireDeclaredStage(stageId);
            var active = activatedStages.SingleOrDefault(value => value.Definition.Id == stageId);
            return active == null ? (IReadOnlyList<string>)Array.Empty<string>() : active.Groups.Select(value => value.Id).ToList().AsReadOnly();
        }

        public IReadOnlyList<StandingRow> GetStageStandings(string stageId, string groupId = null)
        {
            var definition = RequireDeclaredStage(stageId);
            var active = activatedStages.SingleOrDefault(value => value.Definition.Id == stageId);
            if (active == null) return Array.Empty<StandingRow>();
            var participants = groupId == null ? active.Participants : active.Groups.SingleOrDefault(value => value.Id == groupId)?.ClubIds
                ?? throw new ArgumentException("Unknown standings group.", nameof(groupId));
            var rows = CalculateDeclarativeStandings(participants, new[] { stageId }, definition);
            if (definition.Kind != "knockout" || !IsStageComplete(stageId)) return rows;
            var winners = GetStageWinners(active);
            return rows.OrderBy(value => winners.Contains(value.ClubId) ? 0 : 1)
                .Select((value, index) => DeclarativeStandingsCalculator.WithRank(value, index + 1)).ToList().AsReadOnly();
        }

        public IReadOnlyList<CompetitionOutcomeAward> QualifiedOutcomes
        {
            get
            {
                var awarded = new List<CompetitionOutcomeAward>();
                if (!Edition.IsDeclarative) return awarded.AsReadOnly();
                foreach (var outcome in Edition.Format.Outcomes)
                {
                    if (!IsStageComplete(outcome.StageId)) continue;
                    var groups = outcome.Ranking == "per-group" ? GetStageGroupIds(outcome.StageId) : new string[] { null };
                    // Knockout stages have one overall classification.
                    if (groups.Count == 0) groups = new string[] { null };
                    foreach (var groupId in groups)
                        foreach (var row in GetStageStandings(outcome.StageId, groupId).Where(value => value.Rank >= outcome.FromRank && value.Rank <= outcome.ToRank))
                            awarded.Add(new CompetitionOutcomeAward(outcome, row, groupId));
                }
                return awarded.AsReadOnly();
            }
        }

        private CompetitionStageDefinition RequireDeclaredStage(string stageId)
        {
            if (!Edition.IsDeclarative) throw new InvalidOperationException("This edition uses a legacy competition profile.");
            return Edition.Format.Stages.SingleOrDefault(value => value.Id == stageId) ?? throw new ArgumentException("Unknown stage.", nameof(stageId));
        }

        private IReadOnlyList<StandingRow> CalculateDeclarativeStandings(IEnumerable<string> participants, IEnumerable<string> stageIds,
            CompetitionStageDefinition rankingDefinition)
        {
            var selected = new HashSet<string>(stageIds, StringComparer.Ordinal);
            var points = Edition.Format.Stages.ToDictionary(value => value.Id, value => value.Points, StringComparer.Ordinal);
            return DeclarativeStandingsCalculator.Calculate(participants,
                results.Where(value => selected.Contains(fixturesById[value.FixtureId].StageId)),
                result => points[fixturesById[result.FixtureId].StageId], rankingDefinition.TieBreakers,
                SeasonId + "|ranking|" + rankingDefinition.Id);
        }

        private void EnsureDeclarativeProgress()
        {
            for (var index = 1; index < Edition.Format.Stages.Count; index++)
            {
                var definition = Edition.Format.Stages[index];
                if (activatedStages.Any(value => value.Definition.Id == definition.Id)) continue;
                var source = Edition.Format.GetStageSource(definition.Id);
                if (!IsStageComplete(source.StageId)) continue;
                var parent = activatedStages.Single(value => value.Definition.Id == source.StageId);
                ActivateDeclarativeStage(index, source.Selection == "qualified" ? QualifiedSeeds(parent) : SourceWinnerOrLoserSeeds(parent, source.Selection == "winners"));
            }
        }

        private IReadOnlyList<string> SourceWinnerOrLoserSeeds(ActiveStage parent, bool winners)
        {
            var selected = GetStageWinners(parent);
            var ranking = parent.Definition.Qualification?.RankingStageIds;
            var sourceStages = ranking == null || ranking.Count == 0 ? new[] { parent.Definition.Id } : ranking;
            return CalculateDeclarativeStandings(parent.Participants, sourceStages, parent.Definition)
                .Where(value => selected.Contains(value.ClubId) == winners).Select(value => value.ClubId).ToList().AsReadOnly();
        }

        private IReadOnlyList<string> QualifiedSeeds(ActiveStage stage)
        {
            var qualification = stage.Definition.Qualification;
            var rankingStages = qualification.RankingStageIds.Count == 0 ? new[] { stage.Definition.Id } : qualification.RankingStageIds;
            var table = CalculateDeclarativeStandings(stage.Participants, rankingStages, stage.Definition);
            var selected = new HashSet<string>(StringComparer.Ordinal);
            if (qualification.Mode == "winners") selected.UnionWith(GetStageWinners(stage));
            else if (qualification.Mode == "overall") selected.UnionWith(table.Take(qualification.Count).Select(value => value.ClubId));
            else
                foreach (var group in stage.Groups)
                    selected.UnionWith(CalculateDeclarativeStandings(group.ClubIds, rankingStages, stage.Definition)
                        .Take(qualification.Count).Select(value => value.ClubId));
            return table.Where(value => selected.Contains(value.ClubId)).Select(value => value.ClubId).ToList().AsReadOnly();
        }

        private void ActivateDeclarativeStage(int index, IReadOnlyList<string> seeds)
        {
            var definition = Edition.Format.Stages[index];
            var active = new ActiveStage(definition, Edition.StageSchedules[index], seeds);
            ResolveStageGroups(active, index);
            activatedStages.Add(active);
            if (definition.Kind == "league") GenerateLeagueStage(active, index);
            else GenerateKnockoutStage(active, index);
        }

        private void ResolveStageGroups(ActiveStage active, int index)
        {
            if (active.Definition.Kind == "knockout") return;
            if (active.Schedule.Groups.Count > 0)
            {
                foreach (var group in active.Schedule.Groups.OrderBy(value => value.Id, StringComparer.Ordinal))
                    active.Groups.Add(new CompetitionGroupDefinition(group.Id, group.Name,
                        index == 0 ? group.ClubIds : group.SeedRanks.Select(rank => active.Participants[rank - 1])));
                return;
            }
            var groups = new List<string>[active.Definition.GroupCount];
            for (var group = 0; group < groups.Length; group++) groups[group] = new List<string>();
            for (var seed = 0; seed < active.Participants.Count; seed++)
            {
                var group = seed % groups.Length;
                if (seed / groups.Length % 2 != 0) group = groups.Length - 1 - group;
                groups[group].Add(active.Participants[seed]);
            }
            for (var group = 0; group < groups.Length; group++)
            {
                var label = group < 26 ? ((char)('A' + group)).ToString() : "A" + ((char)('A' + group - 26));
                active.Groups.Add(new CompetitionGroupDefinition(StableStageId("group", active.Definition.Id + "|" + label), label, groups[group]));
            }
        }

        private int RoundOffset(int stageIndex) => Edition.Format.Stages.Take(stageIndex).Sum(value => value.RoundCount);
        private string StableStageId(string prefix, string identity) => prefix + "-" +
            StableCompetitionHash.Value(SeasonId + "|" + Edition.Id + "|" + identity).ToString("x16") +
            StableCompetitionHash.Value(prefix + "|" + identity + "|" + SeasonId).ToString("x16");

        private sealed class ActiveStage
        {
            internal CompetitionStageDefinition Definition { get; }
            internal CompetitionStageSchedule Schedule { get; }
            internal IReadOnlyList<string> Participants { get; }
            internal List<CompetitionGroupDefinition> Groups { get; } = new List<CompetitionGroupDefinition>();
            internal ActiveStage(CompetitionStageDefinition definition, CompetitionStageSchedule schedule, IReadOnlyList<string> participants)
            { Definition = definition; Schedule = schedule; Participants = participants.ToList().AsReadOnly(); }
        }
    }
}
