using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;
using NUnit.Framework;

namespace FStudio.FootballWorld.Tests
{
    public sealed class CompetitionSessionTests
    {
        [TestCase(2, 1)] [TestCase(3, 1)] [TestCase(4, 1)] [TestCase(5, 1)] [TestCase(6, 1)] [TestCase(7, 1)]
        [TestCase(2, 2)] [TestCase(3, 2)] [TestCase(4, 2)] [TestCase(5, 2)] [TestCase(6, 2)] [TestCase(64, 2)]
        public void ScheduleUsesEachPairPerLegWithDatesAndNoClubTwicePerRound(int count, int legs)
        {
            var catalog = Catalog(count, legs);
            var edition = catalog.CompetitionEditions.Single();
            var fixtures = RoundRobinScheduler.CreateFixtures(edition, FixtureIds());
            Assert.That(fixtures.Count, Is.EqualTo(count * (count - 1) / 2 * legs));
            Assert.That(fixtures.Select(x => x.Id).Distinct().Count(), Is.EqualTo(fixtures.Count));
            foreach (var round in fixtures.GroupBy(x => x.Round))
            {
                var participants = round.SelectMany(x => new[] {x.HomeClubId, x.AwayClubId}).ToArray();
                Assert.That(participants.Distinct().Count(), Is.EqualTo(participants.Length));
                Assert.That(round.All(x => x.Date.Equals(edition.RoundDates[round.Key - 1])), Is.True);
            }
            foreach (var club in edition.ParticipantClubIds)
            {
                Assert.That(fixtures.Count(x => x.IncludesClub(club)), Is.EqualTo((count - 1) * legs));
                var home = fixtures.Count(x => x.HomeClubId == club);
                var away = fixtures.Count(x => x.AwayClubId == club);
                Assert.That(Math.Abs(home - away), Is.LessThanOrEqualTo(1));
            }
            foreach (var pair in fixtures.GroupBy(x => PairKey(x.HomeClubId, x.AwayClubId)))
            {
                Assert.That(pair.Count(), Is.EqualTo(legs));
                if (legs == 2) Assert.That(pair.Select(x => x.HomeClubId).Distinct().Count(), Is.EqualTo(2));
            }
        }

        [Test]
        public void ScheduleIgnoresParticipantArrayOrderAndRejectsDuplicateFixtureIds()
        {
            var edition = Catalog().CompetitionEditions.Single();
            var reversed = new CompetitionEditionDefinition(edition.Id, edition.CompetitionId, edition.Name,
                edition.ParticipantClubIds.Reverse(), edition.RoundDates, edition.Rules);
            var expected = RoundRobinScheduler.CreateFixtures(edition, FixtureIds());
            var actual = RoundRobinScheduler.CreateFixtures(reversed, FixtureIds());
            Assert.That(actual.Select(x => new {x.Id, x.Round, x.Date, x.HomeClubId, x.AwayClubId}),
                Is.EqualTo(expected.Select(x => new {x.Id, x.Round, x.Date, x.HomeClubId, x.AwayClubId})));
            Assert.Throws<ArgumentException>(() => RoundRobinScheduler.CreateFixtures(edition, () => "repeated"));
        }

        [Test]
        public void FinishingControlledMatchAppliesOtherResultsOnceAndKeepsTheirSimulationProvenance()
        {
            var session = Session();
            var fixture = session.NextFixture;
            var execution = session.BeginFixture(fixture.Id, "attempt-1");
            Assert.That(session.Results, Is.Empty);
            Assert.That(session.ActiveExecution, Is.SameAs(execution));
            var result = Played(fixture, execution.ExecutionId, 2, 1);
            Assert.That(session.CompleteFixture(result), Is.EqualTo(FixtureCompletion.Applied));
            Assert.That(session.Results.Count, Is.EqualTo(2));
            Assert.That(session.Results.Single(x => x.FixtureId != fixture.Id).IsSimulated, Is.True);
            Assert.That(session.Results.Single(x => x.FixtureId == fixture.Id).IsSimulated, Is.False);
            Assert.That(session.ActiveExecution, Is.Null);
            var points = session.Standings.Select(x => x.Points).ToArray();
            Assert.That(session.CompleteFixture(result), Is.EqualTo(FixtureCompletion.AlreadyApplied));
            Assert.That(session.Results.Count, Is.EqualTo(2));
            Assert.That(session.Standings.Select(x => x.Points), Is.EqualTo(points));
            Assert.Throws<InvalidOperationException>(() => session.CompleteFixture(Played(fixture, execution.ExecutionId, 4, 1)));
            Assert.Throws<InvalidOperationException>(() => session.BeginFixture(fixture.Id));
        }

        [Test]
        public void AbandonRetryAndLateCallbacksCannotCreateOrOverwriteResults()
        {
            var session = Session();
            var fixture = session.NextFixture;
            var abandoned = session.BeginFixture(fixture.Id, "abandoned");
            Assert.Throws<InvalidOperationException>(() => session.BeginFixture(fixture.Id));
            Assert.That(session.AbortFixture(fixture.Id, "different"), Is.False);
            Assert.That(session.ActiveExecution, Is.SameAs(abandoned));
            Assert.That(session.AbortFixture(fixture.Id, abandoned.ExecutionId), Is.True);
            Assert.That(session.Results, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => session.BeginFixture(fixture.Id, abandoned.ExecutionId));
            var current = session.BeginFixture(fixture.Id, "retry");
            Assert.That(session.CompleteFixture(Played(fixture, abandoned.ExecutionId, 5, 0)), Is.EqualTo(FixtureCompletion.IgnoredStale));
            Assert.That(session.ActiveExecution, Is.SameAs(current));
            Assert.That(session.Results, Is.Empty);
            session.CompleteFixture(Played(fixture, current.ExecutionId, 1, 0));
            Assert.That(session.CompleteFixture(Played(fixture, abandoned.ExecutionId, 5, 0)), Is.EqualTo(FixtureCompletion.IgnoredStale));
            Assert.That(session.GetResult(fixture.Id).HomeGoals, Is.EqualTo(1));
        }

        [Test]
        public void ResultsRequireCorrectExecutionClubsAndPlayedProvenance()
        {
            var session = Session();
            var fixture = session.NextFixture;
            Assert.Throws<InvalidOperationException>(() => session.CompleteFixture(Played(fixture, "unknown", 1, 0)));
            Assert.Throws<InvalidOperationException>(() => session.BeginFixture(session.Fixtures.First(x => !x.IncludesClub("club-1")).Id));
            var future = session.Fixtures.First(x => x.IncludesClub("club-1") && x.Round > fixture.Round);
            Assert.Throws<InvalidOperationException>(() => session.BeginFixture(future.Id));
            var execution = session.BeginFixture(fixture.Id);
            Assert.Throws<ArgumentException>(() => session.CompleteFixture(new FixtureResult(fixture.Id,
                execution.ExecutionId, fixture.AwayClubId, fixture.HomeClubId, 1, 0)));
            Assert.Throws<InvalidOperationException>(() => session.CompleteFixture(new FixtureResult(fixture.Id,
                execution.ExecutionId, fixture.HomeClubId, fixture.AwayClubId, 1, 0, true)));
            Assert.That(session.Results, Is.Empty);
            Assert.That(session.ActiveExecution, Is.SameAs(execution));
        }

        [TestCase(3, 1)] [TestCase(4, 1)] [TestCase(5, 1)] [TestCase(4, 2)] [TestCase(5, 2)]
        public void PlayingOnlyTheControlledClubCompletesTheWholeEditionIncludingByes(int count, int legs)
        {
            foreach (var controlled in Catalog(count, legs).Clubs.Select(x => x.Id))
            {
                var session = Session(count, legs, controlled);
                var played = 0;
                while (session.NextFixture != null)
                {
                    var fixture = session.NextFixture;
                    var execution = session.BeginFixture(fixture.Id);
                    session.CompleteFixture(Played(fixture, execution.ExecutionId, 1, 1));
                    played++;
                }
                Assert.That(played, Is.EqualTo((count - 1) * legs));
                Assert.That(session.IsComplete, Is.True);
                Assert.That(session.Results.Count, Is.EqualTo(session.Fixtures.Count));
                Assert.That(session.Results.Count(x => !x.IsSimulated), Is.EqualTo(played));
                Assert.That(session.Standings.All(x => x.Played == (count - 1) * legs), Is.True);
                var restored = CompetitionSession.Restore(session.Catalog, session.CaptureSnapshot());
                Assert.That(restored.IsComplete, Is.True);
            }
        }

        [Test]
        public void StandingsUseSportingCriteriaAndShareCompletelyTiedRanks()
        {
            var rules = new LeagueRules(1, 3, 1, 0);
            var rows = StandingsCalculator.Calculate(new[] {"d", "c", "b", "a"}, rules, new[] {
                new FixtureResult("f1", "e1", "a", "b", 2, 0),
                new FixtureResult("f2", "e2", "c", "d", 2, 0)
            });
            Assert.That(rows.Select(x => x.ClubId), Is.EqualTo(new[] {"a", "c", "b", "d"}));
            Assert.That(rows.Select(x => x.Rank), Is.EqualTo(new[] {1, 1, 3, 3}));
            Assert.That(rows.Select(x => x.Points), Is.EqualTo(new[] {3, 3, 0, 0}));
            Assert.That(rows.Select(x => x.GoalDifference), Is.EqualTo(new[] {2, 2, -2, -2}));
            var scoreless = StandingsCalculator.Calculate(new[] {"b", "a"}, rules, new FixtureResult[0]);
            Assert.That(scoreless.Select(x => x.Rank), Is.EqualTo(new[] {1, 1}));
            Assert.Throws<ArgumentException>(() => StandingsCalculator.Calculate(new[] {"a", "b"}, rules,
                new[] {new FixtureResult("f", "e", "a", "b", 0, 0), new FixtureResult("f", "e", "a", "b", 0, 0)}));
        }

        [Test]
        public void SimulationIsStableForTheSameFixtureAndSeasonRegardlessOfExecution()
        {
            var fixture = Session().NextFixture;
            var first = DeterministicMatchSimulator.Simulate(fixture, "attempt-one", "season");
            var next = DeterministicMatchSimulator.Simulate(fixture, "attempt-two", "season");
            Assert.That(next.HomeGoals, Is.EqualTo(first.HomeGoals));
            Assert.That(next.AwayGoals, Is.EqualTo(first.AwayGoals));
            Assert.That(next.IsSimulated, Is.True);
            Assert.That(next.ExecutionId, Is.Not.EqualTo(first.ExecutionId));
        }

        [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void ExplicitManagementSimulationAlsoSupportsLegacyRoundRobinAndRestoresItsProvenance(int count)
        {
            foreach (var club in Catalog(count).Clubs)
            {
                var session = Session(count, club: club.Id);
                var fixture = session.NextFixture;
                session.SimulateFixture(fixture.Id);
                Assert.That(session.GetResult(fixture.Id).IsSimulated, Is.True);
                var restored = CompetitionSession.Restore(session.Catalog, session.CaptureSnapshot());
                while (!restored.IsComplete)
                {
                    restored.SimulateNextRound();
                    restored = CompetitionSession.Restore(session.Catalog, restored.CaptureSnapshot());
                }
                Assert.That(restored.Results.All(value => value.IsSimulated), Is.True);
                var roundsOnly = Session(count, club: club.Id);
                while (!roundsOnly.IsComplete)
                {
                    roundsOnly.SimulateNextRound();
                    roundsOnly = CompetitionSession.Restore(roundsOnly.Catalog, roundsOnly.CaptureSnapshot());
                }
            }
        }

        [Test]
        public void RestorePinsDatabaseFixturesAndResultsAndAbandonsInterruptedExecution()
        {
            var session = Session();
            FinishNext(session);
            var next = session.NextFixture;
            var interrupted = session.BeginFixture(next.Id, "interrupted");
            var snapshot = session.CaptureSnapshot();
            var restored = CompetitionSession.Restore(session.Catalog, snapshot);
            Assert.That(restored.Catalog, Is.SameAs(session.Catalog));
            Assert.That(restored.Fixtures.Select(x => x.Id), Is.EqualTo(session.Fixtures.Select(x => x.Id)));
            Assert.That(restored.Results.Select(x => x.FixtureId), Is.EqualTo(session.Results.Select(x => x.FixtureId)));
            Assert.That(restored.Standings.Select(x => new {x.ClubId, x.Points, x.Rank}),
                Is.EqualTo(session.Standings.Select(x => new {x.ClubId, x.Points, x.Rank})));
            Assert.That(restored.ActiveExecution, Is.Null);
            Assert.That(restored.NextFixture.Id, Is.EqualTo(next.Id));
            Assert.Throws<InvalidOperationException>(() => restored.BeginFixture(next.Id, interrupted.ExecutionId));
            Assert.That(restored.CompleteFixture(Played(next, interrupted.ExecutionId, 3, 0)), Is.EqualTo(FixtureCompletion.IgnoredStale));
            FinishNext(restored);
            Assert.That(snapshot.Results.Count, Is.EqualTo(2));
            Assert.That(session.Results.Count, Is.EqualTo(2));
        }

        [Test]
        public void RestoreRejectsTamperedRevisionFixtureDateOutcomeAndIncompleteRound()
        {
            var session = Session();
            FinishNext(session);
            var valid = session.CaptureSnapshot();
            Assert.Throws<ArgumentException>(() => CompetitionSession.Restore(session.Catalog,
                Snapshot(valid, revision: valid.DatabaseRevision + 1)));
            var fixtures = valid.Fixtures.ToArray();
            var original = fixtures[0];
            fixtures[0] = new FixtureDefinition(original.Id, original.Round, new GameDate(2030, 1, 1), original.HomeClubId, original.AwayClubId);
            Assert.Throws<ArgumentException>(() => CompetitionSession.Restore(session.Catalog, Snapshot(valid, fixtures: fixtures)));
            var results = valid.Results.ToArray();
            var simulatedIndex = Array.FindIndex(results, result => result.IsSimulated);
            var previous = results[simulatedIndex];
            results[simulatedIndex] = new FixtureResult(previous.FixtureId, previous.ExecutionId, previous.HomeClubId,
                previous.AwayClubId, previous.HomeGoals + 1, previous.AwayGoals, true);
            Assert.Throws<ArgumentException>(() => CompetitionSession.Restore(session.Catalog, Snapshot(valid, results: results)));
            Assert.Throws<ArgumentException>(() => CompetitionSession.Restore(session.Catalog,
                Snapshot(valid, results: valid.Results.Where(x => !x.IsSimulated))));
            Assert.Throws<ArgumentException>(() => CompetitionSession.Restore(session.Catalog,
                new CompetitionSnapshot(valid.SeasonId, valid.DatabaseId, valid.DatabaseRevision, valid.EditionId,
                    valid.ControlledClubId, valid.Fixtures, valid.Results, new string[0])));
        }

        [Test]
        public void DomainRejectsInvalidDatesRulesParticipantsAndCompetitionReferences()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(2026, 2, 29));
            Assert.DoesNotThrow(() => new GameDate(2024, 2, 29));
            Assert.Throws<ArgumentException>(() => new LeagueRules(1, 1, 1, 0));
            Assert.Throws<ArgumentException>(() => new LeagueRules(1, 3, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LeagueRules(3, 3, 1, 0));
            var catalog = Catalog();
            var edition = catalog.CompetitionEditions.Single();
            Assert.Throws<ArgumentException>(() => new CompetitionEditionDefinition("e", "c", "Test",
                new[] {"club-1", "club-1"}, new[] {new GameDate(2026, 1, 1)}, edition.Rules));
            Assert.Throws<ArgumentException>(() => new DatabaseCatalog("d", 1, catalog.Clubs, catalog.Players,
                catalog.Memberships, new CompetitionDefinition[0], new[] {edition}));
            Assert.Throws<ArgumentException>(() => CompetitionSession.Create(catalog, edition.Id, "s", "missing"));
        }

        private static void FinishNext(CompetitionSession session)
        {
            var fixture = session.NextFixture;
            var execution = session.BeginFixture(fixture.Id);
            session.CompleteFixture(Played(fixture, execution.ExecutionId, 1, 1));
        }
        private static FixtureResult Played(FixtureDefinition fixture, string executionId, int home, int away)
            => new FixtureResult(fixture.Id, executionId, fixture.HomeClubId, fixture.AwayClubId, home, away);
        private static string PairKey(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
        private static Func<string> FixtureIds() { var i = 0; return () => "fixture-" + ++i; }
        private static CompetitionSession Session(int count = 4, int legs = 1, string club = "club-1")
            => CompetitionSession.Create(Catalog(count, legs), "edition", "season", club, FixtureIds());
        private static DatabaseCatalog Catalog(int count = 4, int legs = 1)
        {
            var clubs = Enumerable.Range(1, count).Select(i => new ClubDefinition("club-" + i, "Club " + i)).ToArray();
            var rules = new LeagueRules(legs, 3, 1, 0);
            var dates = Enumerable.Range(0, rules.RoundCount(count)).Select(i => new DateTime(2026, 10, 3).AddDays(i * 7))
                .Select(date => new GameDate(date.Year, date.Month, date.Day));
            var edition = new CompetitionEditionDefinition("edition", "competition", "Demonstration", clubs.Select(x => x.Id), dates, rules);
            return new DatabaseCatalog("database", 1, clubs, new PlayerDefinition[0], new RosterMembership[0],
                new[] {new CompetitionDefinition("competition", "Test league")}, new[] {edition});
        }
        private static CompetitionSnapshot Snapshot(CompetitionSnapshot source, int? revision = null,
            IEnumerable<FixtureDefinition> fixtures = null, IEnumerable<FixtureResult> results = null)
            => new CompetitionSnapshot(source.SeasonId, source.DatabaseId, revision ?? source.DatabaseRevision,
                source.EditionId, source.ControlledClubId, fixtures ?? source.Fixtures, results ?? source.Results, source.UsedExecutionIds);
    }
}
