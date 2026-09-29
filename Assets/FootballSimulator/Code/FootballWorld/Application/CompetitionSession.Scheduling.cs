using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public sealed partial class CompetitionSession
    {
        private void GenerateLeagueStage(ActiveStage active, int index)
        {
            var stage = active.Definition; var schedule = active.Schedule; var offset = RoundOffset(index);
            if (schedule.AuthoredFixtures.Count > 0)
            {
                foreach (var fixture in schedule.AuthoredFixtures)
                    AddFixture(new FixtureDefinition(fixture.Id, offset + fixture.Round, fixture.Date, fixture.HomeClubId, fixture.AwayClubId,
                        stage.Venue == "neutral" ? schedule.NeutralStadiumId : fixture.StadiumId ?? Catalog.GetClub(fixture.HomeClubId).StadiumId,
                        stage.Id, isNeutral: stage.Venue == "neutral"));
                return;
            }
            var populations = stage.Opponents == "same-group" ? active.Groups.Select(value => value.ClubIds).ToArray() : new[] { active.Participants };
            var groupByClub = active.Groups.SelectMany(group => group.ClubIds.Select(id => new { ClubId = id, GroupId = group.Id }))
                .ToDictionary(value => value.ClubId, value => value.GroupId, StringComparer.Ordinal);
            foreach (var population in populations)
            {
                var rotation = population.OrderBy(value => value, StringComparer.Ordinal).ToList();
                if (rotation.Count % 2 != 0) rotation.Add(null);
                var rounds = rotation.Count - 1;
                for (var round = 0; round < rounds; round++)
                {
                    for (var pair = 0; pair < rotation.Count / 2; pair++)
                    {
                        var home = rotation[pair]; var away = rotation[rotation.Count - 1 - pair];
                        if (home == null || away == null || (stage.Opponents == "cross-group" && groupByClub[home] == groupByClub[away])) continue;
                        if (pair == 0 && round % 2 != 0) Swap(ref home, ref away);
                        if (stage.Venue == "draw" && (StableCompetitionHash.Value(SeasonId + "|league-home|" + stage.Id + "|" + home + "|" + away) & 1) == 0)
                            Swap(ref home, ref away);
                        for (var leg = 1; leg <= stage.Legs; leg++)
                        {
                            var localRound = round + 1 + (leg - 1) * rounds;
                            var host = leg == 1 ? home : away; var visitor = leg == 1 ? away : home;
                            var id = StableStageId("fixture", stage.Id + "|league|" + host + "|" + visitor + "|leg|" + leg);
                            AddFixture(new FixtureDefinition(id, offset + localRound, schedule.RoundDates[localRound - 1], host, visitor,
                                stage.Venue == "neutral" ? schedule.NeutralStadiumId : Catalog.GetClub(host).StadiumId,
                                stage.Id, leg: leg, isNeutral: stage.Venue == "neutral"));
                        }
                    }
                    var last = rotation[rotation.Count - 1]; rotation.RemoveAt(rotation.Count - 1); rotation.Insert(1, last);
                }
            }
        }

        private void GenerateKnockoutStage(ActiveStage active, int index)
        {
            var stage = active.Definition; var schedule = active.Schedule; var offset = RoundOffset(index);
            var paired = stage.Pairing == "cross-group" ? CrossGroupPairs(active) : SeededPairs(active);
            for (var pair = 0; pair < paired.Count; pair++)
            {
                var first = paired[pair][0]; var second = paired[pair][1];
                var stablePair = string.CompareOrdinal(first, second) < 0 ? first + "|" + second : second + "|" + first;
                var tieId = StableStageId("tie", stage.Id + "|" + stablePair);
                var homeFirst = first; var awayFirst = second;
                if (stage.Venue == "seeded")
                {
                    var firstSeed = IndexOf(active.Participants, first); var secondSeed = IndexOf(active.Participants, second);
                    var better = firstSeed < secondSeed ? first : second; var worse = better == first ? second : first;
                    homeFirst = stage.Legs == 2 ? worse : better; awayFirst = homeFirst == first ? second : first;
                }
                else if (stage.Venue == "draw" && (StableCompetitionHash.Value(SeasonId + "|venue|" + tieId) & 1) == 0)
                    Swap(ref homeFirst, ref awayFirst);
                // first-listed grants the first pairing entrant the first home leg;
                // seeded grants the stronger seed the single game or return leg.
                for (var leg = 1; leg <= stage.Legs; leg++)
                {
                    var host = leg == 1 ? homeFirst : awayFirst; var visitor = leg == 1 ? awayFirst : homeFirst;
                    var date = schedule.FixtureDates.Count == 0 ? schedule.RoundDates[leg - 1] : schedule.FixtureDates[(leg - 1) * paired.Count + pair];
                    var id = StableStageId("fixture", tieId + "|leg|" + leg);
                    AddFixture(new FixtureDefinition(id, offset + leg, date, host, visitor,
                        stage.Venue == "neutral" ? schedule.NeutralStadiumId : Catalog.GetClub(host).StadiumId,
                        stage.Id, tieId, leg, stage.Venue == "neutral"));
                }
            }
        }

        private List<string[]> SeededPairs(ActiveStage active)
        {
            var entries = active.Participants.ToList();
            if (active.Definition.Pairing == "draw")
                entries = entries.OrderBy(value => StableCompetitionHash.Value(SeasonId + "|pairing|" + active.Definition.Id + "|" + value))
                    .ThenBy(value => value, StringComparer.Ordinal).ToList();
            var result = new List<string[]>();
            for (var pair = 0; pair < entries.Count / 2; pair++) result.Add(new[] { entries[pair], entries[entries.Count - 1 - pair] });
            return result;
        }

        private List<string[]> CrossGroupPairs(ActiveStage active)
        {
            var parentId = Edition.Format.GetStageSource(active.Definition.Id).StageId;
            var parent = activatedStages.Single(value => value.Definition.Id == parentId);
            var groupByClub = parent.Groups.SelectMany(group => group.ClubIds.Select(id => new { ClubId = id, GroupId = group.Id }))
                .ToDictionary(value => value.ClubId, value => value.GroupId, StringComparer.Ordinal);
            var remaining = active.Participants.ToList(); var pairs = new List<string[]>();
            while (remaining.Count > 0)
            {
                var first = remaining[0]; string second = null;
                for (var candidate = remaining.Count - 1; candidate > 0; candidate--)
                {
                    if (groupByClub[first] == groupByClub[remaining[candidate]]) continue;
                    var rest = remaining.Where((value, position) => position != 0 && position != candidate).ToArray();
                    if (rest.Length > 0 && rest.GroupBy(value => groupByClub[value]).Max(value => value.Count()) > rest.Length / 2) continue;
                    second = remaining[candidate]; break;
                }
                if (second == null) throw new InvalidOperationException("The declared groups cannot form cross-group knockout pairs.");
                pairs.Add(new[] { first, second }); remaining.Remove(first); remaining.Remove(second);
            }
            return pairs;
        }

        private static int IndexOf(IReadOnlyList<string> values, string value)
        {
            for (var index = 0; index < values.Count; index++) if (values[index] == value) return index;
            throw new ArgumentException("Unknown seed.");
        }
        private static void Swap(ref string first, ref string second) { var previous = first; first = second; second = previous; }
    }
}
