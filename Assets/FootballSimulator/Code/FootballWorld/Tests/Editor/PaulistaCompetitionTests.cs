using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;
using NUnit.Framework;

namespace FStudio.FootballWorld.Tests
{
    public sealed class PaulistaCompetitionTests
    {
        [Test]
        public void AuthoredCalendarKeepsDatesHomeSidesAndVenueIdentities()
        {
            var catalog = Catalog();
            var session = CompetitionSession.Create(catalog, "edition", "season", "club-1");
            Assert.That(session.Fixtures.Count, Is.EqualTo(64));
            Assert.That(session.Fixtures.Select(value => value.Id), Is.EquivalentTo(catalog.CompetitionEditions[0].ScheduledFixtures.Select(value => value.Id)));
            foreach (var fixture in session.Fixtures)
            {
                var authored = catalog.CompetitionEditions[0].ScheduledFixtures.Single(value => value.Id == fixture.Id);
                Assert.That(fixture.Date, Is.EqualTo(authored.Date));
                Assert.That(fixture.HomeClubId, Is.EqualTo(authored.HomeClubId));
            }
            Assert.Throws<ArgumentException>(() => RoundRobinScheduler.CreateFixtures(session.Edition));
            Assert.That(session.Edition.EndDate, Is.EqualTo(new GameDate(2026, 3, 8)));
        }

        [Test]
        public void FullSeasonCreatesSeededQuarterfinalsReseededSemifinalsAndTwoLegFinal()
        {
            var session = CompetitionSession.Create(Catalog(), "edition", "season", "club-1");
            for (var round = 1; round <= 8; round++)
            {
                session.SimulateNextRound();
                Assert.That(session.Results.Count, Is.EqualTo(round * 8));
                AssertRestore(session);
            }
            Assert.That(session.Phase, Is.EqualTo(CompetitionPhase.QuarterFinal));
            Assert.That(session.RelegatedClubIds, Is.EqualTo(session.LeagueStandings.Skip(14).Select(value => value.ClubId)));
            var top = session.LeagueStandings.Take(8).Select(value => value.ClubId).ToArray();
            var quarters = session.Fixtures.Where(value => value.Round == 9).ToArray();
            for (var pair = 0; pair < 4; pair++)
            {
                var fixture = quarters.Single(value => value.HomeClubId == top[pair]);
                Assert.That(fixture.AwayClubId, Is.EqualTo(top[7 - pair]));
                Assert.That(fixture.Date, Is.EqualTo(session.Edition.PlayoffDates[pair]));
            }
            session.SimulateNextRound();
            var quarterWinners = quarters.Select(value => Winner(session.GetResult(value.Id))).ToArray();
            var seeded = session.Standings.Where(value => quarterWinners.Contains(value.ClubId)).Select(value => value.ClubId).ToArray();
            var semis = session.Fixtures.Where(value => value.Round == 10).ToArray();
            Assert.That(semis.Single(value => value.HomeClubId == seeded[0]).AwayClubId, Is.EqualTo(seeded[3]));
            Assert.That(semis.Single(value => value.HomeClubId == seeded[1]).AwayClubId, Is.EqualTo(seeded[2]));
            session.SimulateNextRound();
            var finalists = semis.Select(value => Winner(session.GetResult(value.Id))).ToArray();
            var best = session.Standings.First(value => finalists.Contains(value.ClubId)).ClubId;
            Assert.That(session.Fixtures.Single(value => value.Round == 12).HomeClubId, Is.EqualTo(best));
            Assert.That(session.Fixtures.Single(value => value.Round == 11).AwayClubId, Is.EqualTo(best));
            AssertRestore(session);
            session.SimulateNextRound();
            Assert.That(session.IsComplete, Is.False);
            session.SimulateNextRound();
            Assert.That(session.IsComplete, Is.True);
            Assert.That(session.Results.Count, Is.EqualTo(72));
            Assert.That(session.ChampionClubId, Is.Not.Null);
            Assert.That(session.Phase, Is.EqualTo(CompetitionPhase.Complete));
            Assert.That(session.LeagueStandings.All(value => value.Played == 8), Is.True);
            Assert.That(session.Results.All(value => value.HasSimulatedSupplement), Is.True);
            AssertRestore(session);
        }

        [Test]
        public void DailyProgressStopsOnControlledMatchAndDoesNotApplyFutureFixtures()
        {
            var session = Daily();
            Assert.Throws<InvalidOperationException>(() => session.BeginFixture(session.NextFixture.Id));
            session.SimulateThrough(new GameDate(2026, 3, 8));
            Assert.That(session.CurrentDate, Is.EqualTo(session.NextFixture.Date));
            Assert.That(session.Results.Count, Is.LessThanOrEqualTo(7));
            Assert.That(session.Results.All(value => session.Fixtures.Single(fixture => fixture.Id == value.FixtureId).Date.CompareTo(session.CurrentDate.Value) <= 0), Is.True);
            var date = session.CurrentDate.Value;
            var next = session.NextFixture;
            Assert.That(session.SimulateFixture(next.Id), Is.EqualTo(FixtureCompletion.Applied));
            Assert.That(session.CurrentDate, Is.EqualTo(date));
            Assert.That(session.Results.Any(value => value.FixtureId == session.NextFixture.Id), Is.False);
            AssertRestore(session);
            Assert.Throws<ArgumentException>(() => session.SimulateThrough(new GameDate(2026, 1, 1)));
        }

        [Test]
        public void EliminatedClubDoesNotCauseDailySimulationToSkipTheRemainingCalendar()
        {
            var session = Daily();
            for (var round = 0; round < 8; round++)
            {
                session.SimulateThrough(session.NextFixture.Date);
                var fixture = session.NextFixture;
                Play(session, fixture.HomeClubId == session.ControlledClubId ? 0 : 20,
                    fixture.AwayClubId == session.ControlledClubId ? 0 : 20);
            }
            session.SimulateThrough(new GameDate(2026, 2, 8));
            Assert.That(session.NextFixture, Is.Null);
            Assert.That(session.Results.Count, Is.EqualTo(64));
            Assert.That(session.IsComplete, Is.False);
            Assert.That(session.Phase, Is.EqualTo(CompetitionPhase.QuarterFinal));
            session.SimulateThrough(new GameDate(2026, 2, 21));
            Assert.That(session.Results.Count, Is.EqualTo(66));
            Assert.That(session.CurrentDate, Is.EqualTo(new GameDate(2026, 2, 21)));
            Assert.That(session.Fixtures.Any(value => value.Round == 10), Is.False);
            AssertRestore(session);
        }

        [TestCase(2, 0, 1, 0, false)]
        [TestCase(2, 0, 2, 0, true)]
        [TestCase(1, 1, 0, 0, true)]
        public void FinalUsesAggregateWithoutAwayGoalsOrExtraTime(int firstHome, int firstAway, int secondHome, int secondAway, bool shootout)
        {
            var session = FinalSession();
            var first = session.Fixtures.Single(value => value.Round == 11);
            session.SimulateThrough(first.Date);
            Play(session, firstHome, firstAway);
            Assert.That(session.GetResult(first.Id).HomePenalties, Is.Null);
            var second = session.Fixtures.Single(value => value.Round == 12);
            session.SimulateThrough(second.Date);
            var execution = session.BeginFixture(second.Id, "played-final");
            var raw = new FixtureResult(second.Id, execution.ExecutionId, second.HomeClubId, second.AwayClubId, secondHome, secondAway);
            session.CompleteFixture(raw);
            Assert.That(session.GetResult(second.Id).HomePenalties.HasValue, Is.EqualTo(shootout));
            Assert.That(session.CompleteFixture(raw), Is.EqualTo(FixtureCompletion.AlreadyApplied));
            if (!shootout) Assert.That(session.ChampionClubId, Is.EqualTo(first.HomeClubId));
            Assert.That(session.IsComplete, Is.True);
            AssertRestore(session);
        }

        [Test]
        public void DisciplineBreaksTiesBeforeReproducibleSimulatedLots()
        {
            var rules = new LeagueRules(1, 3, 1, 0, true);
            var red = StandingsCalculator.Calculate(new[] { "a", "b" }, rules,
                new[] { new FixtureResult("f", "e", "a", "b", 0, 0, true, 0, 9, 1, 0) }, "seed");
            Assert.That(red[0].ClubId, Is.EqualTo("b"));
            var yellow = StandingsCalculator.Calculate(new[] { "a", "b" }, rules,
                new[] { new FixtureResult("f", "e", "a", "b", 0, 0, true, 2, 1) }, "seed");
            Assert.That(yellow[0].ClubId, Is.EqualTo("b"));
            var lots = StandingsCalculator.Calculate(new[] { "a", "b" }, rules, new FixtureResult[0], "seed");
            var reversed = StandingsCalculator.Calculate(new[] { "b", "a" }, rules, new FixtureResult[0], "seed");
            Assert.That(lots.Select(value => value.ClubId), Is.EqualTo(reversed.Select(value => value.ClubId)));
            Assert.That(lots.Select(value => value.Rank), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void RestoreRejectsTamperedBracketSupplementsAndFutureDailyResults()
        {
            var session = Daily();
            session.SimulateThrough(new GameDate(2026, 3, 8), true);
            var snapshot = session.CaptureSnapshot();
            AssertRestore(session);
            var fixtures = snapshot.Fixtures.ToArray();
            var index = Array.FindIndex(fixtures, value => value.Round == 9);
            var original = fixtures[index];
            fixtures[index] = new FixtureDefinition(original.Id, original.Round, original.Date, original.AwayClubId, original.HomeClubId);
            Assert.Throws<ArgumentException>(() => CompetitionSession.Restore(session.Catalog, Copy(snapshot, fixtures: fixtures)));
            var results = snapshot.Results.ToArray();
            var first = results[0];
            results[0] = new FixtureResult(first.FixtureId, first.ExecutionId, first.HomeClubId, first.AwayClubId,
                first.HomeGoals, first.AwayGoals, first.IsSimulated, (first.HomeYellowCards + 1) % 5, first.AwayYellowCards,
                first.HomeRedCards, first.AwayRedCards, first.HomePenalties, first.AwayPenalties, true);
            Assert.Throws<ArgumentException>(() => CompetitionSession.Restore(session.Catalog, Copy(snapshot, results: results)));
            Assert.Throws<ArgumentException>(() => CompetitionSession.Restore(session.Catalog, Copy(snapshot, currentDate: new GameDate(2026, 1, 1))));
            Assert.Throws<ArgumentException>(() => CompetitionSession.Restore(session.Catalog, Copy(snapshot, fixtures: snapshot.Fixtures.Where(value => value.Round != 12))));
        }

        [Test]
        public void DefinitionRejectsMissingRepeatedOrOutOfWindowFixturesAndInvalidPlayoffDates()
        {
            var edition = Catalog().CompetitionEditions[0];
            Assert.Throws<ArgumentException>(() => Edition(edition, edition.ScheduledFixtures.Take(63)));
            var fixtures = edition.ScheduledFixtures.ToArray();
            var fixture = fixtures[0];
            fixtures[0] = new FixtureDefinition(fixture.Id, fixture.Round, new GameDate(2026, 4, 1), fixture.HomeClubId, fixture.AwayClubId);
            Assert.Throws<ArgumentException>(() => Edition(edition, fixtures));
            fixtures = edition.ScheduledFixtures.ToArray();
            fixtures[1] = fixtures[0];
            Assert.Throws<ArgumentException>(() => Edition(edition, fixtures));
            var playoffDates = edition.PlayoffDates.ToArray();
            playoffDates[4] = playoffDates[0];
            Assert.Throws<ArgumentException>(() => new CompetitionEditionDefinition(edition.Id, edition.CompetitionId, edition.Name,
                edition.ParticipantClubIds, edition.RoundDates, edition.Rules, edition.ScheduledFixtures, playoffDates));
            Assert.Throws<ArgumentException>(() => new LeagueRules(2, 3, 1, 0, true));
        }

        private static CompetitionEditionDefinition Edition(CompetitionEditionDefinition source, IEnumerable<FixtureDefinition> fixtures)
            => new CompetitionEditionDefinition(source.Id, source.CompetitionId, source.Name, source.ParticipantClubIds,
                source.RoundDates, source.Rules, fixtures, source.PlayoffDates);
        private static void AssertRestore(CompetitionSession session)
        {
            var restored = CompetitionSession.Restore(session.Catalog, session.CaptureSnapshot());
            Assert.That(restored.Results.Count, Is.EqualTo(session.Results.Count));
            Assert.That(restored.Fixtures.Select(value => value.Id), Is.EqualTo(session.Fixtures.Select(value => value.Id)));
            Assert.That(restored.ChampionClubId, Is.EqualTo(session.ChampionClubId));
            Assert.That(restored.CurrentDate, Is.EqualTo(session.CurrentDate));
        }
        private static void Play(CompetitionSession session, int home, int away)
        {
            var fixture = session.NextFixture;
            var execution = session.BeginFixture(fixture.Id);
            session.CompleteFixture(new FixtureResult(fixture.Id, execution.ExecutionId, fixture.HomeClubId, fixture.AwayClubId, home, away));
        }
        private static string Winner(FixtureResult result) => result.HomeGoals != result.AwayGoals
            ? result.HomeGoals > result.AwayGoals ? result.HomeClubId : result.AwayClubId
            : result.HomePenalties > result.AwayPenalties ? result.HomeClubId : result.AwayClubId;
        private static CompetitionSession FinalSession()
        {
            var preview = Daily();
            preview.SimulateThrough(new GameDate(2026, 3, 1), true);
            var finalist = preview.Fixtures.Single(value => value.Round == 12).HomeClubId;
            var session = Daily(finalist);
            session.SimulateThrough(new GameDate(2026, 3, 1), true);
            return session;
        }
        private static CompetitionSession Daily(string clubId = "club-1")
            => CompetitionSession.CreateDaily(Catalog(), "edition", "season", clubId, new GameDate(2026, 1, 1));
        private static CompetitionSnapshot Copy(CompetitionSnapshot source, IEnumerable<FixtureDefinition> fixtures = null,
            IEnumerable<FixtureResult> results = null, GameDate? currentDate = null)
            => new CompetitionSnapshot(source.SeasonId, source.DatabaseId, source.DatabaseRevision, source.EditionId,
                source.ControlledClubId, fixtures ?? source.Fixtures, results ?? source.Results, source.UsedExecutionIds,
                source.DailyProgress, currentDate ?? source.CurrentDate);

        internal static DatabaseCatalog Catalog()
        {
            var clubs = Enumerable.Range(1, 16).Select(value => new ClubDefinition("club-" + value, "Club " + value)).ToArray();
            var dates = Enumerable.Range(0, 8).Select(value => new DateTime(2026, 1, 10).AddDays(value * 4))
                .Select(value => new GameDate(value.Year, value.Month, value.Day)).ToArray();
            var fixtures = new List<FixtureDefinition>();
            for (var round = 0; round < 8; round++)
                for (var pair = 0; pair < 8; pair++)
                {
                    var first = clubs[pair].Id;
                    var second = clubs[8 + (pair + round) % 8].Id;
                    var date = dates[round].ToDateTime().AddDays(pair % 2);
                    fixtures.Add(new FixtureDefinition("authored-" + round + "-" + pair, round + 1,
                        new GameDate(date.Year, date.Month, date.Day), round % 2 == 0 ? first : second, round % 2 == 0 ? second : first));
                }
            var playoffs = new[] {
                new GameDate(2026, 2, 22), new GameDate(2026, 2, 21), new GameDate(2026, 2, 21), new GameDate(2026, 2, 22),
                new GameDate(2026, 2, 28), new GameDate(2026, 3, 1), new GameDate(2026, 3, 4), new GameDate(2026, 3, 8)
            };
            var edition = new CompetitionEditionDefinition("edition", "competition", "Paulista prototype",
                clubs.Select(value => value.Id), dates, new LeagueRules(1, 3, 1, 0, true), fixtures, playoffs);
            return new DatabaseCatalog("database", 1, clubs, new PlayerDefinition[0], new RosterMembership[0],
                new[] { new CompetitionDefinition("competition", "Paulista") }, new[] { edition });
        }
    }
}
