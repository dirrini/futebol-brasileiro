using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public sealed partial class CompetitionSession
    {
        public CompetitionSnapshot CaptureSnapshot() => new CompetitionSnapshot(SeasonId, Catalog.DatabaseId,
            Catalog.DatabaseRevision, Edition.Id, ControlledClubId, fixtures, results,
            usedExecutionIds.OrderBy(value => value, StringComparer.Ordinal), DailyProgress, CurrentDate);

        public static CompetitionSession Restore(DatabaseCatalog catalog, CompetitionSnapshot snapshot)
        {
            if (catalog == null || snapshot == null) throw new ArgumentNullException();
            if (catalog.DatabaseId != snapshot.DatabaseId || catalog.DatabaseRevision != snapshot.DatabaseRevision)
                throw new ArgumentException("The save requires its exact authored database snapshot.", nameof(snapshot));
            var edition = catalog.GetCompetitionEdition(snapshot.EditionId);
            var session = new CompetitionSession(catalog, snapshot.EditionId, snapshot.SeasonId,
                snapshot.ControlledClubId, edition.Rules.IsPaulista2026 ? edition.ScheduledFixtures : snapshot.Fixtures,
                snapshot.DailyProgress, snapshot.CurrentDate);
            if (!edition.Rules.IsPaulista2026)
                ValidateLegacySchedule(session, snapshot);
            foreach (var executionId in snapshot.UsedExecutionIds)
                if (!session.usedExecutionIds.Add(CompetitionIdentity.Validate(executionId)))
                    throw new ArgumentException("Saved execution identities must be unique.");
            var completedExecutions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var result in snapshot.Results)
            {
                if (result == null || !session.fixturesById.TryGetValue(result.FixtureId, out var fixture) ||
                    session.resultsById.ContainsKey(result.FixtureId))
                    throw new ArgumentException("Saved results require distinct fixtures reached through the preceding stages.");
                ValidateResultClubs(fixture, result);
                if (!session.usedExecutionIds.Contains(result.ExecutionId) || !completedExecutions.Add(result.ExecutionId))
                    throw new ArgumentException("Saved results require distinct registered executions.");
                if (!fixture.IncludesClub(session.ControlledClubId) && !result.IsSimulated)
                    throw new ArgumentException("Other clubs require declared simulation provenance.");
                if (session.DailyProgress && fixture.Date.CompareTo(session.CurrentDate.Value) > 0)
                    throw new ArgumentException("A saved result is ahead of the daily calendar.");
                try
                {
                    var supplemented = session.SupplementResult(fixture, result);
                    if (!supplemented.SameOutcome(result)) throw new ArgumentException("Saved results must retain their simulation supplements.");
                    session.ValidateSimulation(fixture, result);
                }
                catch (InvalidOperationException error) { throw new ArgumentException("Saved simulation or final-leg order is invalid.", error); }
                session.results.Add(result);
                session.resultsById.Add(result.FixtureId, result);
                session.EnsurePlayoffs();
            }
            if (edition.Rules.IsPaulista2026)
                ValidatePaulistaSnapshotFixtures(session, snapshot);
            session.ValidateRestoredProgress();
            // An interrupted match returns to pending; its execution stays used
            // so a delayed engine callback cannot mutate the restored season.
            return session;
        }

        private static void ValidateLegacySchedule(CompetitionSession session, CompetitionSnapshot snapshot)
        {
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
                if (!remaining.TryGetValue(FixtureKey(fixture), out var saved) || !saved.Date.Equals(fixture.Date) || saved.StadiumId != null)
                    throw new ArgumentException("Saved fixtures do not match the edition's dates and pairings.");
        }

        private static void ValidatePaulistaSnapshotFixtures(CompetitionSession session, CompetitionSnapshot snapshot)
        {
            if (snapshot.Fixtures.Count != session.fixtures.Count)
                throw new ArgumentException("Saved knockout fixtures do not match the reached competition stages.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var saved in snapshot.Fixtures)
                if (saved == null || !seen.Add(saved.Id) || !session.fixturesById.TryGetValue(saved.Id, out var expected) ||
                    FixtureKey(saved) != FixtureKey(expected) || !saved.Date.Equals(expected.Date) || saved.StadiumId != expected.StadiumId)
                    throw new ArgumentException("Saved fixtures do not match the authored calendar or qualified knockout bracket.");
        }

        private void ValidateRestoredProgress()
        {
            var missingControlled = false;
            foreach (var fixture in fixtures.Where(value => value.IncludesClub(ControlledClubId)))
            {
                if (!resultsById.ContainsKey(fixture.Id)) missingControlled = true;
                else if (missingControlled) throw new ArgumentException("Saved controlled fixtures must be completed in calendar order.");
            }
            if (DailyProgress)
            {
                foreach (var fixture in fixtures)
                    if (fixture.Date.CompareTo(CurrentDate.Value) < 0 && !resultsById.ContainsKey(fixture.Id))
                        throw new ArgumentException("The daily calendar cannot skip pending fixtures.");
            }
            else if (!Edition.Rules.IsPaulista2026)
            {
                var throughRound = SimulatableRound();
                foreach (var fixture in fixtures.Where(value => !value.IncludesClub(ControlledClubId)))
                    if (resultsById.ContainsKey(fixture.Id) != (fixture.Round <= throughRound))
                        throw new ArgumentException("Saved round progress is incomplete or ahead of the controlled club.");
            }
            else
            {
                var missingRound = false;
                foreach (var round in fixtures.GroupBy(value => value.Round).OrderBy(value => value.Key))
                {
                    var completed = round.Count(value => resultsById.ContainsKey(value.Id));
                    if (completed != 0 && (missingRound || completed != round.Count()))
                        throw new ArgumentException("A championship snapshot must contain whole rounds in calendar order.");
                    if (completed == 0) missingRound = true;
                }
            }
        }
    }
}
