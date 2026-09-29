using System;
using System.Collections.Generic;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public static class StandingsCalculator
    {
        public static IReadOnlyList<StandingRow> Calculate(IEnumerable<string> participants, LeagueRules rules,
            IEnumerable<FixtureResult> results, string drawingLotsSeed = null)
        {
            if (participants == null || rules == null || results == null) throw new ArgumentNullException();
            var rows = new Dictionary<string, MutableRow>(StringComparer.Ordinal);
            foreach (var clubId in participants) rows.Add(clubId, new MutableRow { ClubId = clubId,
                Lot = StableCompetitionHash.Value((drawingLotsSeed ?? "standings") + "|lot|" + clubId) });
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
                home.YellowCards += result.HomeYellowCards; away.YellowCards += result.AwayYellowCards;
                home.RedCards += result.HomeRedCards; away.RedCards += result.AwayRedCards;
                if (result.HomeGoals == result.AwayGoals) { home.Draws++; away.Draws++; }
                else if (result.HomeGoals > result.AwayGoals) { home.Wins++; away.Losses++; }
                else { away.Wins++; home.Losses++; }
            }
            var ordered = new List<MutableRow>(rows.Values);
            foreach (var row in ordered)
                row.Points = row.Wins * rules.WinPoints + row.Draws * rules.DrawPoints + row.Losses * rules.LossPoints;
            ordered.Sort((left, right) =>
            {
                var sporting = CompareSporting(left, right, rules.IsPaulista2026);
                return sporting != 0 ? sporting : string.CompareOrdinal(left.ClubId, right.ClubId);
            });
            var standings = new List<StandingRow>(ordered.Count);
            var rank = 1;
            for (var i = 0; i < ordered.Count; i++)
            {
                var row = ordered[i];
                if (i > 0 && (rules.IsPaulista2026 || CompareSporting(ordered[i - 1], row, false) != 0)) rank = i + 1;
                standings.Add(new StandingRow(row.ClubId, rank, row.Wins, row.Draws, row.Losses,
                    row.GoalsFor, row.GoalsAgainst, row.Points, row.YellowCards, row.RedCards));
            }
            return standings.AsReadOnly();
        }

        private static int CompareSporting(MutableRow left, MutableRow right, bool paulista)
        {
            var compare = right.Points.CompareTo(left.Points);
            if (compare != 0) return compare;
            compare = right.Wins.CompareTo(left.Wins);
            if (compare != 0) return compare;
            compare = (right.GoalsFor - right.GoalsAgainst).CompareTo(left.GoalsFor - left.GoalsAgainst);
            if (compare != 0) return compare;
            compare = right.GoalsFor.CompareTo(left.GoalsFor);
            if (compare != 0 || !paulista) return compare;
            compare = left.RedCards.CompareTo(right.RedCards);
            if (compare != 0) return compare;
            compare = left.YellowCards.CompareTo(right.YellowCards);
            return compare != 0 ? compare : left.Lot.CompareTo(right.Lot);
        }
        private sealed class MutableRow
        {
            public string ClubId;
            public int Wins, Draws, Losses, GoalsFor, GoalsAgainst, Points, YellowCards, RedCards;
            public ulong Lot;
        }
    }
}
