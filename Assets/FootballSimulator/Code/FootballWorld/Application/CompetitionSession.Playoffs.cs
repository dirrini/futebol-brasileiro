using System;
using System.Linq;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public sealed partial class CompetitionSession
    {
        private void EnsurePlayoffs()
        {
            if (Edition.IsDeclarative) { EnsureDeclarativeProgress(); return; }
            if (!Edition.Rules.IsPaulista2026) return;
            if (RoundComplete(8) && !fixtures.Any(value => value.Round == 9))
            {
                var qualified = LeagueStandings.Take(8).Select(value => value.ClubId).ToArray();
                for (var pair = 0; pair < 4; pair++)
                    AddPlayoffFixture(9, Edition.PlayoffDates[pair], qualified[pair], qualified[7 - pair]);
            }
            if (RoundComplete(9) && !fixtures.Any(value => value.Round == 10))
            {
                var winners = fixtures.Where(value => value.Round == 9).Select(SingleMatchWinner).ToArray();
                var seeded = Standings.Where(value => winners.Contains(value.ClubId)).Select(value => value.ClubId).ToArray();
                for (var pair = 0; pair < 2; pair++)
                    AddPlayoffFixture(10, Edition.PlayoffDates[4 + pair], seeded[pair], seeded[3 - pair]);
            }
            if (RoundComplete(10) && !fixtures.Any(value => value.Round == 11))
            {
                var winners = fixtures.Where(value => value.Round == 10).Select(SingleMatchWinner).ToArray();
                var seeded = Standings.Where(value => winners.Contains(value.ClubId)).Select(value => value.ClubId).ToArray();
                AddPlayoffFixture(11, Edition.PlayoffDates[6], seeded[1], seeded[0]);
                AddPlayoffFixture(12, Edition.PlayoffDates[7], seeded[0], seeded[1]);
            }
        }

        private void AddPlayoffFixture(int round, GameDate date, string homeClubId, string awayClubId)
        {
            // Derived from the season and the participating identities, never a
            // mutable array index, display name or an engine object identity.
            var identity = SeasonId + "|" + Edition.Id + "|" + round + "|" + homeClubId + "|" + awayClubId;
            var id = "fixture-" + StableCompetitionHash.Value(identity).ToString("x16") +
                StableCompetitionHash.Value("playoff|" + identity).ToString("x16");
            AddFixture(new FixtureDefinition(id, round, date, homeClubId, awayClubId, Catalog.GetClub(homeClubId).StadiumId));
        }

        private string SingleMatchWinner(FixtureDefinition fixture)
        {
            var result = resultsById[fixture.Id];
            if (result.HomeGoals != result.AwayGoals) return result.HomeGoals > result.AwayGoals ? fixture.HomeClubId : fixture.AwayClubId;
            return result.HomePenalties > result.AwayPenalties ? fixture.HomeClubId : fixture.AwayClubId;
        }

        private string FinalWinner()
        {
            var firstFixture = fixtures.Single(value => value.Round == 11);
            var secondFixture = fixtures.Single(value => value.Round == 12);
            var first = resultsById[firstFixture.Id];
            var second = resultsById[secondFixture.Id];
            var homeAggregate = second.HomeGoals + first.AwayGoals;
            var awayAggregate = second.AwayGoals + first.HomeGoals;
            if (homeAggregate != awayAggregate) return homeAggregate > awayAggregate ? secondFixture.HomeClubId : secondFixture.AwayClubId;
            return second.HomePenalties > second.AwayPenalties ? secondFixture.HomeClubId : secondFixture.AwayClubId;
        }

        private FixtureResult SupplementResult(FixtureDefinition fixture, FixtureResult result)
        {
            if (Edition.IsDeclarative) return SupplementDeclarativeResult(fixture, result);
            if (!Edition.Rules.IsPaulista2026)
            {
                if (result.HomeYellowCards != 0 || result.AwayYellowCards != 0 || result.HomeRedCards != 0 ||
                    result.AwayRedCards != 0 || result.HomePenalties.HasValue || result.HasSimulatedSupplement)
                    throw new ArgumentException("The round-robin profile does not support discipline or shootout supplements.");
                return result;
            }
            // The current 3D bridge reports only the final score. Cards and shootouts
            // are an explicitly labelled prototype supplement, not invented telemetry.
            var hash = StableCompetitionHash.Value(SeasonId + "|supplement|" + fixture.Id);
            var homeYellow = (int)(hash % 5);
            var awayYellow = (int)((hash >> 8) % 5);
            var homeRed = (hash >> 16) % 12 == 0 ? 1 : 0;
            var awayRed = (hash >> 24) % 12 == 0 ? 1 : 0;
            var requiresShootout = (fixture.Round == 9 || fixture.Round == 10) && result.HomeGoals == result.AwayGoals;
            if (fixture.Round == 12)
            {
                var firstFixture = fixtures.Single(value => value.Round == 11);
                if (!resultsById.TryGetValue(firstFixture.Id, out var first))
                    throw new InvalidOperationException("The first final leg must finish before the second leg.");
                requiresShootout = result.HomeGoals + first.AwayGoals == result.AwayGoals + first.HomeGoals;
            }
            int? homePenalties = null, awayPenalties = null;
            if (requiresShootout)
            {
                var winnerGoals = 4 + (int)((hash >> 40) % 2);
                var loserGoals = winnerGoals - 1;
                homePenalties = (hash & (1UL << 48)) == 0 ? winnerGoals : loserGoals;
                awayPenalties = homePenalties == winnerGoals ? loserGoals : winnerGoals;
            }
            var supplemented = new FixtureResult(result.FixtureId, result.ExecutionId, result.HomeClubId, result.AwayClubId,
                result.HomeGoals, result.AwayGoals, result.IsSimulated, homeYellow, awayYellow, homeRed, awayRed,
                homePenalties, awayPenalties, true);
            if (result.HasSimulatedSupplement && !result.SameOutcome(supplemented))
                throw new ArgumentException("The saved prototype supplement does not match this season's seed and rules.");
            if (!result.HasSimulatedSupplement && (result.HomeYellowCards != 0 || result.AwayYellowCards != 0 ||
                result.HomeRedCards != 0 || result.AwayRedCards != 0 || result.HomePenalties.HasValue))
                throw new ArgumentException("The prototype accepts score-only results or its validated simulation supplement.");
            return supplemented;
        }
    }
}
