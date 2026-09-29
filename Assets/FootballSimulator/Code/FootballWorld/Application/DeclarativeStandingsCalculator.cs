using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    internal static class DeclarativeStandingsCalculator
    {
        internal static IReadOnlyList<StandingRow> Calculate(IEnumerable<string> participants, IEnumerable<FixtureResult> results,
            Func<FixtureResult, CompetitionPointsDefinition> pointsFor, IReadOnlyList<string> tieBreakers, string seed)
        {
            var rows = participants.ToDictionary(id => id, id => new Row { ClubId = id,
                Lot = StableCompetitionHash.Value(seed + "|lot|" + id) }, StringComparer.Ordinal);
            foreach (var result in results)
            {
                var points = pointsFor(result);
                // Cross-group opponents earn points in their own group table.
                // A result must therefore be counted even when its opponent is
                // outside the selected standings population.
                if (rows.TryGetValue(result.HomeClubId, out var home))
                    home.Add(result.HomeGoals, result.AwayGoals, result.HomeYellowCards, result.HomeRedCards, points);
                if (rows.TryGetValue(result.AwayClubId, out var away))
                    away.Add(result.AwayGoals, result.HomeGoals, result.AwayYellowCards, result.AwayRedCards, points);
            }
            var ordered = rows.Values.ToList();
            ordered.Sort((left, right) => Compare(left, right, tieBreakers));
            return ordered.Select((row, index) => row.ToStanding(index + 1)).ToList().AsReadOnly();
        }
        internal static StandingRow WithRank(StandingRow row, int rank) => new StandingRow(row.ClubId, rank,
            row.Wins, row.Draws, row.Losses, row.GoalsFor, row.GoalsAgainst, row.Points, row.YellowCards, row.RedCards);
        private static int Compare(Row left, Row right, IReadOnlyList<string> tieBreakers)
        {
            var compare = right.Points.CompareTo(left.Points);
            if (compare != 0) return compare;
            foreach (var rule in tieBreakers)
            {
                switch (rule)
                {
                    case "wins": compare = right.Wins.CompareTo(left.Wins); break;
                    case "goal-difference": compare = (right.GoalsFor - right.GoalsAgainst).CompareTo(left.GoalsFor - left.GoalsAgainst); break;
                    case "goals-for": compare = right.GoalsFor.CompareTo(left.GoalsFor); break;
                    case "red-cards": compare = left.RedCards.CompareTo(right.RedCards); break;
                    case "yellow-cards": compare = left.YellowCards.CompareTo(right.YellowCards); break;
                    case "seeded-draw": compare = left.Lot.CompareTo(right.Lot); break;
                    default: throw new ArgumentException("Unsupported standings tiebreaker.");
                }
                if (compare != 0) return compare;
            }
            return string.CompareOrdinal(left.ClubId, right.ClubId);
        }
        private sealed class Row
        {
            internal string ClubId;
            internal int Wins, Draws, Losses, GoalsFor, GoalsAgainst, Points, YellowCards, RedCards;
            internal ulong Lot;
            internal void Add(int goalsFor, int goalsAgainst, int yellow, int red, CompetitionPointsDefinition points)
            {
                GoalsFor += goalsFor; GoalsAgainst += goalsAgainst; YellowCards += yellow; RedCards += red;
                if (goalsFor == goalsAgainst) { Draws++; Points += points.Draw; }
                else if (goalsFor > goalsAgainst) { Wins++; Points += points.Win; }
                else { Losses++; Points += points.Loss; }
            }
            internal StandingRow ToStanding(int rank) => new StandingRow(ClubId, rank, Wins, Draws, Losses, GoalsFor, GoalsAgainst, Points, YellowCards, RedCards);
        }
    }
}
