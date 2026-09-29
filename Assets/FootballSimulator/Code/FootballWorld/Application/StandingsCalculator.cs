using System;
using System.Collections.Generic;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public static class StandingsCalculator
    {
        public static IReadOnlyList<StandingRow> Calculate(IEnumerable<string> participants, LeagueRules rules,
            IEnumerable<FixtureResult> results)
        {
            if (participants == null || rules == null || results == null) throw new ArgumentNullException();
            var rows = new Dictionary<string, MutableRow>(StringComparer.Ordinal);
            foreach (var clubId in participants) rows.Add(clubId, new MutableRow { ClubId = clubId });
            var seenFixtures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var result in results)
            {
                if (result == null || !seenFixtures.Add(result.FixtureId))
                    throw new ArgumentException("Results must contain each fixture exactly once.", nameof(results));
                if (result.HomeClubId == result.AwayClubId || !rows.TryGetValue(result.HomeClubId, out var home) ||
                    !rows.TryGetValue(result.AwayClubId, out var away))
                    throw new ArgumentException("Result clubs must be distinct edition participants.", nameof(results));
                home.GoalsFor += result.HomeGoals; home.GoalsAgainst += result.AwayGoals;
                away.GoalsFor += result.AwayGoals; away.GoalsAgainst += result.HomeGoals;
                if (result.HomeGoals == result.AwayGoals) { home.Draws++; away.Draws++; }
                else if (result.HomeGoals > result.AwayGoals) { home.Wins++; away.Losses++; }
                else { away.Wins++; home.Losses++; }
            }
            var ordered = new List<MutableRow>(rows.Values);
            foreach (var row in ordered)
                row.Points = row.Wins * rules.WinPoints + row.Draws * rules.DrawPoints + row.Losses * rules.LossPoints;
            ordered.Sort((left, right) =>
            {
                var sporting = CompareSporting(left, right);
                return sporting != 0 ? sporting : string.CompareOrdinal(left.ClubId, right.ClubId);
            });
            var standings = new List<StandingRow>(ordered.Count);
            var rank = 1;
            for (var i = 0; i < ordered.Count; i++)
            {
                var row = ordered[i];
                if (i > 0 && CompareSporting(ordered[i - 1], row) != 0) rank = i + 1;
                standings.Add(new StandingRow(row.ClubId, rank, row.Wins, row.Draws, row.Losses,
                    row.GoalsFor, row.GoalsAgainst, row.Points));
            }
            return standings.AsReadOnly();
        }

        private static int CompareSporting(MutableRow left, MutableRow right)
        {
            var compare = right.Points.CompareTo(left.Points);
            if (compare != 0) return compare;
            compare = right.Wins.CompareTo(left.Wins);
            if (compare != 0) return compare;
            compare = (right.GoalsFor - right.GoalsAgainst).CompareTo(left.GoalsFor - left.GoalsAgainst);
            return compare != 0 ? compare : right.GoalsFor.CompareTo(left.GoalsFor);
        }
        private sealed class MutableRow
        {
            public string ClubId;
            public int Wins, Draws, Losses, GoalsFor, GoalsAgainst, Points;
        }
    }
}
