using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public sealed partial class CompetitionSession
    {
        private HashSet<string> GetStageWinners(ActiveStage stage)
        {
            if (stage.Definition.Kind != "knockout" || !IsStageComplete(stage.Definition.Id))
                throw new InvalidOperationException("Tie winners require a completed knockout stage.");
            var winners = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tie in fixtures.Where(value => value.StageId == stage.Definition.Id).GroupBy(value => value.TieId))
            {
                var last = tie.OrderBy(value => value.Leg).Last();
                var winner = ResolveDeclarativeTie(stage, last, resultsById[last.Id]);
                if (winner == null) throw new InvalidOperationException("A completed knockout tie lacks its deciding shootout.");
                winners.Add(winner);
            }
            return winners;
        }

        private string ResolveDeclarativeTie(ActiveStage stage, FixtureDefinition finalFixture, FixtureResult finalResult)
        {
            var home = finalFixture.HomeClubId; var away = finalFixture.AwayClubId;
            var homeAggregate = 0; var awayAggregate = 0; var homeAwayGoals = 0; var awayAwayGoals = 0;
            foreach (var fixture in fixtures.Where(value => value.TieId == finalFixture.TieId).OrderBy(value => value.Leg))
            {
                FixtureResult result;
                if (fixture.Id == finalFixture.Id) result = finalResult;
                else if (!resultsById.TryGetValue(fixture.Id, out result))
                    throw new InvalidOperationException("Previous knockout legs must finish before the deciding leg.");
                homeAggregate += fixture.HomeClubId == home ? result.HomeGoals : result.AwayGoals;
                awayAggregate += fixture.HomeClubId == away ? result.HomeGoals : result.AwayGoals;
                if (fixture.AwayClubId == home) homeAwayGoals += result.AwayGoals;
                if (fixture.AwayClubId == away) awayAwayGoals += result.AwayGoals;
            }
            if (homeAggregate != awayAggregate) return homeAggregate > awayAggregate ? home : away;
            if (stage.Definition.AwayGoals && homeAwayGoals != awayAwayGoals) return homeAwayGoals > awayAwayGoals ? home : away;
            if (stage.Definition.TiedWinner == "higher-seed")
                return IndexOf(stage.Participants, home) < IndexOf(stage.Participants, away) ? home : away;
            if (finalResult.HomePenalties.HasValue) return finalResult.HomePenalties > finalResult.AwayPenalties ? home : away;
            return null;
        }

        private FixtureResult SupplementDeclarativeResult(FixtureDefinition fixture, FixtureResult result)
        {
            var stage = activatedStages.Single(value => value.Definition.Id == fixture.StageId);
            var hash = StableCompetitionHash.Value(SeasonId + "|supplement|" + fixture.Id);
            var scoreOnly = new FixtureResult(result.FixtureId, result.ExecutionId, result.HomeClubId, result.AwayClubId,
                result.HomeGoals, result.AwayGoals, result.IsSimulated);
            var shootout = stage.Definition.Kind == "knockout" && fixture.Leg == stage.Definition.Legs &&
                ResolveDeclarativeTie(stage, fixture, scoreOnly) == null;
            int? homePenalties = null, awayPenalties = null;
            if (shootout)
            {
                var winnerGoals = 4 + (int)((hash >> 40) % 2); var loserGoals = winnerGoals - 1;
                homePenalties = (hash & (1UL << 48)) == 0 ? winnerGoals : loserGoals;
                awayPenalties = homePenalties == winnerGoals ? loserGoals : winnerGoals;
            }
            var supplemented = new FixtureResult(result.FixtureId, result.ExecutionId, result.HomeClubId, result.AwayClubId,
                result.HomeGoals, result.AwayGoals, result.IsSimulated,
                (int)(hash % 5), (int)((hash >> 8) % 5), (hash >> 16) % 12 == 0 ? 1 : 0, (hash >> 24) % 12 == 0 ? 1 : 0,
                homePenalties, awayPenalties, true);
            if (result.HasSimulatedSupplement && !result.SameOutcome(supplemented))
                throw new ArgumentException("The declarative competition supplement does not match its pinned rules and seed.");
            if (!result.HasSimulatedSupplement && (result.HomeYellowCards != 0 || result.AwayYellowCards != 0 || result.HomeRedCards != 0 ||
                result.AwayRedCards != 0 || result.HomePenalties.HasValue))
                throw new ArgumentException("The current match bridge accepts score-only results or validated prototype supplements.");
            return supplemented;
        }
    }
}
