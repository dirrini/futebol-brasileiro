using System;
using System.Collections.Generic;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public static class RoundRobinScheduler
    {
        public static IReadOnlyList<FixtureDefinition> CreateFixtures(CompetitionEditionDefinition edition,
            Func<string> nextFixtureId = null)
        {
            if (edition == null) throw new ArgumentNullException(nameof(edition));
            if (edition.IsDeclarative || edition.Rules.IsPaulista2026)
                throw new ArgumentException("Use the edition's authored schedule for Paulista 2026.", nameof(edition));
            nextFixtureId = nextFixtureId ?? (() => "fixture-" + Guid.NewGuid().ToString("N"));
            var rotation = new List<string>(edition.ParticipantClubIds);
            rotation.Sort(StringComparer.Ordinal);
            if (rotation.Count % 2 != 0) rotation.Add(null); // A bye has no sporting fixture or result.
            var result = new List<FixtureDefinition>();
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            var rounds = rotation.Count - 1;
            for (var round = 0; round < rounds; round++)
            {
                for (var pair = 0; pair < rotation.Count / 2; pair++)
                {
                    var home = rotation[pair];
                    var away = rotation[rotation.Count - 1 - pair];
                    if (home == null || away == null) continue;
                    if (pair == 0 && round % 2 != 0) { var swap = home; home = away; away = swap; }
                    var id = nextFixtureId();
                    if (!identifiers.Add(id)) throw new ArgumentException("Fixture IDs must be unique.", nameof(nextFixtureId));
                    result.Add(new FixtureDefinition(id, round + 1, edition.RoundDates[round], home, away));
                }
                var last = rotation[rotation.Count - 1];
                rotation.RemoveAt(rotation.Count - 1);
                rotation.Insert(1, last);
            }
            if (edition.Rules.Legs == 2)
            {
                var firstLeg = result.ToArray();
                foreach (var fixture in firstLeg)
                {
                    var id = nextFixtureId();
                    if (!identifiers.Add(id)) throw new ArgumentException("Fixture IDs must be unique.", nameof(nextFixtureId));
                    result.Add(new FixtureDefinition(id, fixture.Round + rounds,
                        edition.RoundDates[fixture.Round + rounds - 1], fixture.AwayClubId, fixture.HomeClubId));
                }
            }
            return result.AsReadOnly();
        }
    }
}
