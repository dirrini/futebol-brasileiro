using System;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    // Deliberately small prototype simulation: seeded scores, not the 3D engine
    // or a prediction of real clubs. Stable hashing avoids platform GetHashCode differences.
    public static class DeterministicMatchSimulator
    {
        public static FixtureResult Simulate(FixtureDefinition fixture, string executionId, string seed)
        {
            if (fixture == null || seed == null) throw new ArgumentNullException();
            uint state = 2166136261;
            unchecked
            {
                foreach (var character in seed + "|" + fixture.Id + "|" + fixture.HomeClubId + "|" + fixture.AwayClubId)
                    state = (state ^ character) * 16777619;
                state = state * 1664525 + 1013904223;
                var home = (int)((state >> 16) % 5);
                state = state * 1664525 + 1013904223;
                var away = (int)((state >> 16) % 5);
                return new FixtureResult(fixture.Id, executionId, fixture.HomeClubId, fixture.AwayClubId, home, away, true);
            }
        }
    }
}
