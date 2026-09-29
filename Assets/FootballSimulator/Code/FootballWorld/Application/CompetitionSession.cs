using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public sealed class CompetitionSession
    {
        private readonly Dictionary<string, FixtureDefinition> fixturesById;
        private readonly Dictionary<string, FixtureResult> resultsById = new Dictionary<string, FixtureResult>(StringComparer.Ordinal);
        private readonly List<FixtureResult> results = new List<FixtureResult>();
        private readonly HashSet<string> usedExecutionIds = new HashSet<string>(StringComparer.Ordinal);

        public DatabaseCatalog Catalog { get; }
        public CompetitionEditionDefinition Edition { get; }
        public string SeasonId { get; }
        public string ControlledClubId { get; }
        public IReadOnlyList<FixtureDefinition> Fixtures { get; }
        public IReadOnlyList<FixtureResult> Results { get; }
        public IReadOnlyList<StandingRow> Standings => StandingsCalculator.Calculate(Edition.ParticipantClubIds, Edition.Rules, results);
        public bool IsComplete => results.Count == Fixtures.Count;
        public FixtureDefinition NextFixture => GetNextFixture(ControlledClubId);
        public FixtureExecution ActiveExecution { get; private set; }

        private CompetitionSession(DatabaseCatalog catalog, string editionId, string seasonId, string controlledClubId,
            IEnumerable<FixtureDefinition> fixtures)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Edition = catalog.GetCompetitionEdition(editionId);
            SeasonId = CompetitionIdentity.Validate(seasonId);
            ControlledClubId = CompetitionIdentity.Validate(controlledClubId);
            if (!Edition.ParticipantClubIds.Contains(controlledClubId))
                throw new ArgumentException("The controlled club must participate in this edition.", nameof(controlledClubId));
            var ordered = new List<FixtureDefinition>(fixtures);
            ordered.Sort((left, right) =>
            {
                if (left == null || right == null) throw new ArgumentException("Fixtures cannot be null.");
                var compare = left.Round.CompareTo(right.Round);
                return compare != 0 ? compare : string.CompareOrdinal(left.Id, right.Id);
            });
            fixturesById = new Dictionary<string, FixtureDefinition>(StringComparer.Ordinal);
            foreach (var fixture in ordered)
            {
                if (fixture == null || fixturesById.ContainsKey(fixture.Id))
                    throw new ArgumentException("Fixtures require distinct, non-null identities.", nameof(fixtures));
                fixturesById.Add(fixture.Id, fixture);
            }
            Fixtures = ordered.AsReadOnly();
            Results = results.AsReadOnly();
        }

        public static CompetitionSession Create(DatabaseCatalog catalog, string editionId, string seasonId,
            string controlledClubId, Func<string> nextFixtureId = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            return new CompetitionSession(catalog, editionId, seasonId, controlledClubId,
                RoundRobinScheduler.CreateFixtures(catalog.GetCompetitionEdition(editionId), nextFixtureId));
        }

        public FixtureDefinition GetNextFixture(string clubId)
            => Fixtures.FirstOrDefault(fixture => fixture.IncludesClub(clubId) && !resultsById.ContainsKey(fixture.Id));

        public FixtureResult GetResult(string fixtureId)
            => resultsById.TryGetValue(fixtureId, out var result) ? result : null;

        public FixtureExecution BeginFixture(string fixtureId, string executionId = null)
        {
            if (ActiveExecution != null) throw new InvalidOperationException("A fixture execution is already active.");
            if (!fixturesById.TryGetValue(fixtureId, out var fixture)) throw new ArgumentException("Unknown fixture.", nameof(fixtureId));
            if (resultsById.ContainsKey(fixtureId)) throw new InvalidOperationException("This fixture is already complete.");
            if (fixture.IncludesClub(ControlledClubId))
            {
                if (NextFixture?.Id != fixtureId) throw new InvalidOperationException("Play the controlled club's next fixture first.");
            }
            else if (fixture.Round > SimulatableRound())
                throw new InvalidOperationException("Other results are applied only after the controlled match finishes.");
            executionId = CompetitionIdentity.Validate(executionId ?? "execution-" + Guid.NewGuid().ToString("N"));
            if (!usedExecutionIds.Add(executionId)) throw new InvalidOperationException("Execution IDs cannot be reused.");
            ActiveExecution = new FixtureExecution(fixtureId, executionId);
            return ActiveExecution;
        }

        public bool AbortFixture(string fixtureId, string executionId)
        {
            if (ActiveExecution == null || ActiveExecution.FixtureId != fixtureId || ActiveExecution.ExecutionId != executionId) return false;
            ActiveExecution = null;
            return true;
        }

        public FixtureCompletion CompleteFixture(FixtureResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (!fixturesById.TryGetValue(result.FixtureId, out var fixture)) throw new ArgumentException("Unknown fixture.", nameof(result));
            ValidateResultClubs(fixture, result);
            if (resultsById.TryGetValue(result.FixtureId, out var previous))
            {
                if (previous.SameOutcome(result)) return FixtureCompletion.AlreadyApplied;
                if (previous.ExecutionId != result.ExecutionId && usedExecutionIds.Contains(result.ExecutionId)) return FixtureCompletion.IgnoredStale;
                throw new InvalidOperationException("A conflicting result cannot replace a completed fixture.");
            }
            if (ActiveExecution == null || ActiveExecution.ExecutionId != result.ExecutionId || ActiveExecution.FixtureId != result.FixtureId)
            {
                if (usedExecutionIds.Contains(result.ExecutionId)) return FixtureCompletion.IgnoredStale;
                throw new InvalidOperationException("The result is not correlated to an active execution.");
            }
            var controlled = fixture.IncludesClub(ControlledClubId);
            if (result.IsSimulated == controlled)
                throw new InvalidOperationException("The controlled club is played; other clubs use the declared prototype simulation.");
            if (result.IsSimulated)
            {
                var expected = DeterministicMatchSimulator.Simulate(fixture, result.ExecutionId, SeasonId);
                if (!expected.SameOutcome(result)) throw new InvalidOperationException("The simulated result does not match this season's deterministic simulation.");
            }
            resultsById.Add(result.FixtureId, result);
            results.Add(result);
            ActiveExecution = null;
            if (controlled) CompleteOtherFixtures();
            return FixtureCompletion.Applied;
        }

        private int SimulatableRound()
        {
            var completed = results.Where(result => fixturesById[result.FixtureId].IncludesClub(ControlledClubId)).ToArray();
            if (completed.Length == 0) return 0;
            return NextFixture == null ? Edition.RoundDates.Count : completed.Max(result => fixturesById[result.FixtureId].Round);
        }

        private void CompleteOtherFixtures()
        {
            var throughRound = SimulatableRound();
            foreach (var fixture in Fixtures)
            {
                if (fixture.IncludesClub(ControlledClubId) || fixture.Round > throughRound || resultsById.ContainsKey(fixture.Id)) continue;
                var execution = BeginFixture(fixture.Id);
                CompleteFixture(DeterministicMatchSimulator.Simulate(fixture, execution.ExecutionId, SeasonId));
            }
        }

        public CompetitionSnapshot CaptureSnapshot() => new CompetitionSnapshot(SeasonId, Catalog.DatabaseId,
            Catalog.DatabaseRevision, Edition.Id, ControlledClubId, Fixtures, results, usedExecutionIds.OrderBy(value => value, StringComparer.Ordinal));

        public static CompetitionSession Restore(DatabaseCatalog catalog, CompetitionSnapshot snapshot)
        {
            if (catalog == null || snapshot == null) throw new ArgumentNullException();
            if (catalog.DatabaseId != snapshot.DatabaseId || catalog.DatabaseRevision != snapshot.DatabaseRevision)
                throw new ArgumentException("The save requires its exact authored database snapshot.", nameof(snapshot));
            var session = new CompetitionSession(catalog, snapshot.EditionId, snapshot.SeasonId,
                snapshot.ControlledClubId, snapshot.Fixtures);
            var expected = RoundRobinScheduler.CreateFixtures(session.Edition);
            if (expected.Count != snapshot.Fixtures.Count) throw new ArgumentException("Saved fixture count does not match the edition.");
            var remaining = new Dictionary<string, FixtureDefinition>(StringComparer.Ordinal);
            foreach (var fixture in snapshot.Fixtures)
            {
                var key = FixtureKey(fixture);
                if (remaining.ContainsKey(key)) throw new ArgumentException("Saved fixture pairing is repeated.");
                remaining.Add(key, fixture);
            }
            foreach (var fixture in expected)
                if (!remaining.TryGetValue(FixtureKey(fixture), out var saved) || !saved.Date.Equals(fixture.Date))
                    throw new ArgumentException("Saved fixtures do not match the edition's dates and pairings.");
            foreach (var executionId in snapshot.UsedExecutionIds)
                if (!session.usedExecutionIds.Add(CompetitionIdentity.Validate(executionId)))
                    throw new ArgumentException("Saved execution identities must be unique.");
            var completedExecutions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var result in snapshot.Results)
            {
                if (result == null || !session.fixturesById.TryGetValue(result.FixtureId, out var fixture) ||
                    session.resultsById.ContainsKey(result.FixtureId)) throw new ArgumentException("Saved results require distinct known fixtures.");
                ValidateResultClubs(fixture, result);
                if (!session.usedExecutionIds.Contains(result.ExecutionId) || !completedExecutions.Add(result.ExecutionId))
                    throw new ArgumentException("Saved results require distinct registered executions.");
                if (result.IsSimulated == fixture.IncludesClub(session.ControlledClubId))
                    throw new ArgumentException("Saved result provenance is inconsistent with the controlled club.");
                if (result.IsSimulated && !DeterministicMatchSimulator.Simulate(fixture, result.ExecutionId, session.SeasonId).SameOutcome(result))
                    throw new ArgumentException("Saved simulated result is inconsistent with the season seed.");
                session.results.Add(result);
                session.resultsById.Add(result.FixtureId, result);
            }
            var missingControlled = false;
            foreach (var fixture in session.Fixtures.Where(value => value.IncludesClub(session.ControlledClubId)))
            {
                if (!session.resultsById.ContainsKey(fixture.Id)) missingControlled = true;
                else if (missingControlled) throw new ArgumentException("Saved controlled fixtures must be completed in calendar order.");
            }
            var throughRound = session.SimulatableRound();
            foreach (var fixture in session.Fixtures.Where(value => !value.IncludesClub(session.ControlledClubId)))
                if (session.resultsById.ContainsKey(fixture.Id) != (fixture.Round <= throughRound))
                    throw new ArgumentException("Saved round progress is incomplete or ahead of the controlled club.");
            // An interrupted 3D match returns to pending; its execution ID remains used.
            // Storage/UI can announce the interruption without reviving old callbacks.
            return session;
        }

        private static string FixtureKey(FixtureDefinition fixture)
            => fixture.Round + "|" + fixture.HomeClubId + "|" + fixture.AwayClubId;

        private static void ValidateResultClubs(FixtureDefinition fixture, FixtureResult result)
        {
            if (fixture.HomeClubId != result.HomeClubId || fixture.AwayClubId != result.AwayClubId)
                throw new ArgumentException("Result clubs must match the fixture's home and away sides.", nameof(result));
        }
    }
}
