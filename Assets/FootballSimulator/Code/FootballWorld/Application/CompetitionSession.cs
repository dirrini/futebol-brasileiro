using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public sealed partial class CompetitionSession
    {
        private readonly Dictionary<string, FixtureDefinition> fixturesById = new Dictionary<string, FixtureDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, FixtureResult> resultsById = new Dictionary<string, FixtureResult>(StringComparer.Ordinal);
        private readonly List<FixtureDefinition> fixtures = new List<FixtureDefinition>();
        private readonly List<FixtureResult> results = new List<FixtureResult>();
        private readonly HashSet<string> usedExecutionIds = new HashSet<string>(StringComparer.Ordinal);
        private bool activeSimulation;

        public DatabaseCatalog Catalog { get; }
        public CompetitionEditionDefinition Edition { get; }
        public string SeasonId { get; }
        public string ControlledClubId { get; }
        public IReadOnlyList<FixtureDefinition> Fixtures { get; }
        public IReadOnlyList<FixtureResult> Results { get; }
        public bool DailyProgress { get; }
        public GameDate? CurrentDate { get; private set; }
        public IReadOnlyList<StandingRow> Standings => StandingsCalculator.Calculate(Edition.ParticipantClubIds, Edition.Rules, results, SeasonId);
        public IReadOnlyList<StandingRow> LeagueStandings => StandingsCalculator.Calculate(Edition.ParticipantClubIds, Edition.Rules,
            results.Where(value => fixturesById[value.FixtureId].Round <= Edition.RoundDates.Count), SeasonId);
        public bool IsComplete => results.Count == fixtures.Count && (!Edition.Rules.IsPaulista2026 || fixtures.Any(value => value.Round == 12));
        public FixtureDefinition NextFixture => GetNextFixture(ControlledClubId);
        public FixtureExecution ActiveExecution { get; private set; }
        public CompetitionPhase Phase => IsComplete ? CompetitionPhase.Complete : GetPhase(fixtures.First(value => !resultsById.ContainsKey(value.Id)));
        public string ChampionClubId => !IsComplete ? null : Edition.Rules.IsPaulista2026
            ? FinalWinner() : Standings.Count(value => value.Rank == 1) == 1 ? Standings[0].ClubId : null;
        public IReadOnlyList<string> RelegatedClubIds => Edition.Rules.IsPaulista2026 && RoundComplete(8)
            ? LeagueStandings.Skip(14).Select(value => value.ClubId).ToList().AsReadOnly() : new List<string>().AsReadOnly();

        private CompetitionSession(DatabaseCatalog catalog, string editionId, string seasonId, string controlledClubId,
            IEnumerable<FixtureDefinition> initialFixtures, bool dailyProgress = false, GameDate? currentDate = null)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Edition = catalog.GetCompetitionEdition(editionId);
            SeasonId = CompetitionIdentity.Validate(seasonId);
            ControlledClubId = CompetitionIdentity.Validate(controlledClubId);
            if (!Edition.ParticipantClubIds.Contains(controlledClubId))
                throw new ArgumentException("The controlled club must participate in this edition.", nameof(controlledClubId));
            if (dailyProgress != currentDate.HasValue) throw new ArgumentException("Daily progress requires a current calendar date.");
            DailyProgress = dailyProgress;
            CurrentDate = currentDate;
            foreach (var fixture in initialFixtures) AddFixture(fixture);
            Fixtures = fixtures.AsReadOnly();
            Results = results.AsReadOnly();
        }

        public static CompetitionSession Create(DatabaseCatalog catalog, string editionId, string seasonId,
            string controlledClubId, Func<string> nextFixtureId = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var edition = catalog.GetCompetitionEdition(editionId);
            return new CompetitionSession(catalog, editionId, seasonId, controlledClubId,
                edition.Rules.IsPaulista2026 ? edition.ScheduledFixtures : RoundRobinScheduler.CreateFixtures(edition, nextFixtureId));
        }

        public static CompetitionSession CreateDaily(DatabaseCatalog catalog, string editionId, string seasonId,
            string controlledClubId, GameDate startDate)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var edition = catalog.GetCompetitionEdition(editionId);
            if (startDate.CompareTo(edition.StartDate) > 0)
                throw new ArgumentException("A new daily season must start before its first round.", nameof(startDate));
            return new CompetitionSession(catalog, editionId, seasonId, controlledClubId,
                edition.Rules.IsPaulista2026 ? edition.ScheduledFixtures : RoundRobinScheduler.CreateFixtures(edition), true, startDate);
        }

        public CompetitionPhase GetPhase(FixtureDefinition fixture)
        {
            if (fixture == null) throw new ArgumentNullException(nameof(fixture));
            if (!Edition.Rules.IsPaulista2026 || fixture.Round <= 8) return CompetitionPhase.League;
            return fixture.Round == 9 ? CompetitionPhase.QuarterFinal : fixture.Round == 10 ? CompetitionPhase.SemiFinal : CompetitionPhase.Final;
        }

        public FixtureDefinition GetNextFixture(string clubId)
            => fixtures.FirstOrDefault(fixture => fixture.IncludesClub(clubId) && !resultsById.ContainsKey(fixture.Id));
        public FixtureResult GetResult(string fixtureId) => resultsById.TryGetValue(fixtureId, out var result) ? result : null;
        public FixtureExecution BeginFixture(string fixtureId, string executionId = null)
            => BeginFixtureInternal(fixtureId, executionId, false, false);

        private FixtureExecution BeginFixtureInternal(string fixtureId, string executionId, bool simulateControlled, bool allowRound)
        {
            if (ActiveExecution != null) throw new InvalidOperationException("A fixture execution is already active.");
            if (!fixturesById.TryGetValue(fixtureId, out var fixture)) throw new ArgumentException("Unknown fixture.", nameof(fixtureId));
            if (resultsById.ContainsKey(fixtureId)) throw new InvalidOperationException("This fixture is already complete.");
            if (DailyProgress && fixture.Date.CompareTo(CurrentDate.Value) > 0)
                throw new InvalidOperationException("The fixture is scheduled after the current career date.");
            var controlled = fixture.IncludesClub(ControlledClubId);
            if (controlled)
            {
                if (NextFixture?.Id != fixtureId) throw new InvalidOperationException("Play the controlled club's next fixture first.");
            }
            else if (!DailyProgress && !allowRound && fixture.Round > SimulatableRound())
                throw new InvalidOperationException("Other results are applied only after the controlled match finishes.");
            executionId = CompetitionIdentity.Validate(executionId ?? "execution-" + Guid.NewGuid().ToString("N"));
            if (!usedExecutionIds.Add(executionId)) throw new InvalidOperationException("Execution IDs cannot be reused.");
            activeSimulation = !controlled || simulateControlled;
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
            result = SupplementResult(fixture, result);
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
            if (result.IsSimulated != activeSimulation)
                throw new InvalidOperationException("The result must match the explicitly selected execution mode.");
            ValidateSimulation(fixture, result);
            resultsById.Add(result.FixtureId, result);
            results.Add(result);
            ActiveExecution = null;
            EnsurePlayoffs();
            if (!DailyProgress && fixture.IncludesClub(ControlledClubId)) CompleteOtherFixtures();
            return FixtureCompletion.Applied;
        }

        // Management simulation is explicit. A played result cannot be relabelled
        // by a callback; scores and supplements are verified when restoring saves.
        public FixtureCompletion SimulateFixture(string fixtureId)
        {
            return SimulateFixtureInternal(fixtureId, false);
        }

        private FixtureCompletion SimulateFixtureInternal(string fixtureId, bool allowRound)
        {
            var execution = BeginFixtureInternal(fixtureId, null, true, allowRound);
            return CompleteFixture(DeterministicMatchSimulator.Simulate(fixturesById[fixtureId], execution.ExecutionId, SeasonId));
        }

        public int SimulateThrough(GameDate targetDate, bool includeControlled = false)
        {
            if (!DailyProgress) throw new InvalidOperationException("Calendar advancement requires a daily competition.");
            if (ActiveExecution != null) throw new InvalidOperationException("Finish or abandon the active match before advancing.");
            if (targetDate.CompareTo(CurrentDate.Value) < 0) throw new ArgumentException("The calendar cannot move backwards.", nameof(targetDate));
            var before = results.Count;
            while (true)
            {
                var controlled = NextFixture;
                var cutoff = !includeControlled && controlled != null && controlled.Date.CompareTo(targetDate) < 0 ? controlled.Date : targetDate;
                CurrentDate = cutoff;
                var pending = fixtures.FirstOrDefault(value => !resultsById.ContainsKey(value.Id) && value.Date.CompareTo(cutoff) <= 0 &&
                    (includeControlled || !value.IncludesClub(ControlledClubId)));
                if (pending == null) break;
                SimulateFixtureInternal(pending.Id, true);
            }
            return results.Count - before;
        }

        public int SimulateNextRound()
        {
            if (ActiveExecution != null) throw new InvalidOperationException("Finish or abandon the active match before simulating.");
            if (IsComplete) return 0;
            var round = fixtures.Where(value => !resultsById.ContainsKey(value.Id)).Min(value => value.Round);
            if (DailyProgress)
                return SimulateThrough(fixtures.Where(value => value.Round == round).Max(value => value.Date), true);
            // A legacy odd-team league can begin with a bye for the user. Finish
            // through their next game so the existing whole-round save invariant
            // is retained rather than persisting AI results ahead of that club.
            if (!Edition.Rules.IsPaulista2026) round = NextFixture?.Round ?? Edition.RoundDates.Count;
            var before = results.Count;
            foreach (var fixture in fixtures.Where(value => value.Round <= round).ToArray())
                if (!resultsById.ContainsKey(fixture.Id)) SimulateFixtureInternal(fixture.Id, true);
            return results.Count - before;
        }

        private int SimulatableRound()
        {
            var completed = results.Where(result => fixturesById[result.FixtureId].IncludesClub(ControlledClubId)).ToArray();
            if (completed.Length == 0) return 0;
            return !Edition.Rules.IsPaulista2026 && NextFixture == null ? Edition.RoundDates.Count : completed.Max(result => fixturesById[result.FixtureId].Round);
        }

        private void CompleteOtherFixtures()
        {
            var throughRound = SimulatableRound();
            foreach (var fixture in fixtures.Where(value => !value.IncludesClub(ControlledClubId) && value.Round <= throughRound).ToArray())
                if (!resultsById.ContainsKey(fixture.Id)) SimulateFixtureInternal(fixture.Id, true);
        }

        private void AddFixture(FixtureDefinition fixture)
        {
            if (fixture == null || fixturesById.ContainsKey(fixture.Id)) throw new ArgumentException("Fixtures require distinct, non-null identities.");
            fixturesById.Add(fixture.Id, fixture);
            fixtures.Add(fixture);
            fixtures.Sort((left, right) =>
            {
                var compare = left.Date.CompareTo(right.Date);
                if (compare != 0) return compare;
                compare = left.Round.CompareTo(right.Round);
                return compare != 0 ? compare : string.CompareOrdinal(left.Id, right.Id);
            });
        }

        private bool RoundComplete(int round) => fixtures.Any(value => value.Round == round) &&
            fixtures.Where(value => value.Round <= round).All(value => resultsById.ContainsKey(value.Id));
        private static string FixtureKey(FixtureDefinition fixture) => fixture.Round + "|" + fixture.HomeClubId + "|" + fixture.AwayClubId;
        private static void ValidateResultClubs(FixtureDefinition fixture, FixtureResult result)
        {
            if (fixture.HomeClubId != result.HomeClubId || fixture.AwayClubId != result.AwayClubId)
                throw new ArgumentException("Result clubs must match the fixture's home and away sides.", nameof(result));
        }
        private void ValidateSimulation(FixtureDefinition fixture, FixtureResult result)
        {
            if (result.IsSimulated && !SupplementResult(fixture, DeterministicMatchSimulator.Simulate(fixture, result.ExecutionId, SeasonId)).SameOutcome(result))
                throw new InvalidOperationException("The simulated result does not match this season's deterministic simulation.");
        }
    }
}
