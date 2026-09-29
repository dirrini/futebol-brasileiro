using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;
using NUnit.Framework;

namespace FStudio.FootballWorld.Tests
{
    public sealed class DeclarativeCompetitionTests
    {
        private static readonly string[] Tiebreakers = { "wins", "goal-difference", "goals-for", "red-cards", "yellow-cards", "seeded-draw" };
        private static readonly CompetitionPointsDefinition Points = new CompetitionPointsDefinition(3, 1, 0);

        [TestCase("all", 1, 3, 6)]
        [TestCase("all", 2, 6, 12)]
        [TestCase("same-group", 1, 1, 2)]
        [TestCase("same-group", 2, 2, 4)]
        [TestCase("cross-group", 1, 3, 4)]
        [TestCase("cross-group", 2, 6, 8)]
        public void GroupOpponentPoliciesGenerateOnlyDeclaredEdgesAndUpdateBothGroupTables(string opponents, int legs, int rounds, int matches)
        {
            var stage = League("league", 2, opponents, legs, rounds);
            var format = Format(4, new[] { stage });
            var groups = new[] { Group("group-a", "club-1", "club-2"), Group("group-b", "club-3", "club-4") };
            var catalog = Catalog(format, new[] { Schedule("league", 1, rounds, groups) });
            var session = CompetitionSession.Create(catalog, "edition", "season", "club-1");
            Assert.That(session.Fixtures.Count, Is.EqualTo(matches));
            foreach (var round in session.Fixtures.GroupBy(value => value.Round))
                Assert.That(round.SelectMany(value => new[] { value.HomeClubId, value.AwayClubId }).Distinct().Count(), Is.EqualTo(round.Count() * 2));
            foreach (var fixture in session.Fixtures)
            {
                var same = groups[0].ClubIds.Contains(fixture.HomeClubId) == groups[0].ClubIds.Contains(fixture.AwayClubId);
                if (opponents != "all") Assert.That(same, Is.EqualTo(opponents == "same-group"));
            }
            CompleteAndRestore(session);
            foreach (var group in groups)
                foreach (var row in session.GetStageStandings("league", group.Id))
                {
                    var own = session.Results.Where(value => value.HomeClubId == row.ClubId || value.AwayClubId == row.ClubId).ToArray();
                    Assert.That(row.Played, Is.EqualTo(own.Length));
                    Assert.That(row.Points, Is.EqualTo(own.Sum(value => value.HomeGoals == value.AwayGoals ? 1 :
                        (value.HomeClubId == row.ClubId ? value.HomeGoals > value.AwayGoals : value.AwayGoals > value.HomeGoals) ? 3 : 0)));
                }
        }

        [Test]
        public void LeagueSeedsPopulateEditableGroupsAndAwardPromotionBeforeTheTwoLegTitle()
        {
            var format = Format(16, new[]
            {
                League("league", 1, "all", 1, 15, new StageQualificationDefinition("overall", 8)),
                League("groups", 2, "same-group", 1, 3, new StageQualificationDefinition("per-group", 1)),
                Knockout("final", 2)
            }, new[] { new CompetitionOutcomeDefinition("promotion", "Promotion", "groups", "promotion", "per-group", 1, 2) });
            var groupAssignments = new[]
            {
                new CompetitionGroupDefinition("group-a", "A", seedRanks: new[] { 1, 4, 5, 8 }),
                new CompetitionGroupDefinition("group-b", "B", seedRanks: new[] { 2, 3, 6, 7 })
            };
            var session = CompetitionSession.Create(Catalog(format, new[]
            { Schedule("league", 1, 15), Schedule("groups", 20, 3, groupAssignments), Schedule("final", 25, 2) }), "edition", "season", "club-1");
            while (!session.IsStageComplete("league")) { session.SimulateNextRound(); AssertRestore(session); }
            var seeds = session.GetStageStandings("league").Take(8).Select(value => value.ClubId).ToArray();
            var groups = session.StageStates.Single(value => value.StageId == "groups").Groups;
            Assert.That(groups.Single(value => value.Id == "group-a").ClubIds, Is.EqualTo(new[] { seeds[0], seeds[3], seeds[4], seeds[7] }));
            Assert.That(groups.Single(value => value.Id == "group-b").ClubIds, Is.EqualTo(new[] { seeds[1], seeds[2], seeds[5], seeds[6] }));
            while (!session.IsStageComplete("groups")) { session.SimulateNextRound(); AssertRestore(session); }
            Assert.That(session.QualifiedOutcomes.Count, Is.EqualTo(4));
            Assert.That(session.IsComplete, Is.False);
            var groupWinners = groups.Select(group => session.GetStageStandings("groups", group.Id)[0].ClubId);
            Assert.That(session.StageStates.Single(value => value.StageId == "final").ParticipantClubIds, Is.EquivalentTo(groupWinners));
            CompleteAndRestore(session);
            Assert.That(session.GetStageStandings("final")[0].ClubId, Is.EqualTo(session.ChampionClubId));
            Assert.That(session.QualifiedOutcomes.Count, Is.EqualTo(4));
        }

        [Test]
        public void LosersBranchRunsAlongsideTitleWithoutAwardingItsWinnersTheTrophyOrSkippingCareerDates()
        {
            var catalog = BranchCatalog();
            var session = CompetitionSession.CreateDaily(catalog, "edition", "branch-season", "club-1", Date(0));
            session.SimulateThrough(session.Edition.EndDate);
            Assert.That(session.CurrentDate.Value, Is.EqualTo(Date(2)));
            PlayControlled(session, 0, 9);
            session.SimulateThrough(session.Edition.EndDate);
            Assert.That(session.CurrentDate.Value, Is.EqualTo(Date(4)));
            var quartersWinners = session.GetStageStandings("quarters").Take(4).Select(value => value.ClubId).ToArray();
            var semifinalists = session.StageStates.Single(value => value.StageId == "semis").ParticipantClubIds;
            var playoff = session.StageStates.Single(value => value.StageId == "playoff").ParticipantClubIds;
            Assert.That(semifinalists, Is.EquivalentTo(quartersWinners));
            Assert.That(playoff.Intersect(semifinalists), Is.Empty);
            Assert.That(playoff, Does.Contain("club-1"));
            Assert.That(session.Results.All(value => session.Fixtures.Single(fixture => fixture.Id == value.FixtureId).Date.CompareTo(Date(4)) <= 0), Is.True);
            AssertRestore(session);
            PlayControlled(session, 4, 0);
            session.SimulateThrough(session.Edition.EndDate);
            Assert.That(session.CurrentDate.Value, Is.EqualTo(Date(6)));
            PlayControlled(session, 4, 0);
            Assert.That(session.IsComplete, Is.False);
            Assert.That(session.ChampionClubId, Is.Null);
            session.SimulateThrough(session.Edition.EndDate);
            Assert.That(session.IsComplete, Is.True);
            Assert.That(session.QualifiedOutcomes.Count, Is.EqualTo(2));
            Assert.That(session.QualifiedOutcomes.Select(value => value.ClubId), Does.Contain("club-1"));
            Assert.That(semifinalists, Does.Contain(session.ChampionClubId));
            Assert.That(playoff, Does.Not.Contain(session.ChampionClubId));
            Assert.That(session.Results.Count, Is.EqualTo(12));
            AssertRestore(session);
        }

        [Test]
        public void EveryClubCanSimulateAndRestoreParallelBranchesThroughChampionshipAndDailyModes()
        {
            var catalog = BranchCatalog();
            foreach (var club in catalog.Clubs)
                foreach (var daily in new[] { false, true })
                {
                    var session = daily ? CompetitionSession.CreateDaily(catalog, "edition", "branch-season", club.Id, Date(0)) :
                        CompetitionSession.Create(catalog, "edition", "branch-season", club.Id);
                    CompleteAndRestore(session);
                    Assert.That(session.QualifiedOutcomes.Count, Is.EqualTo(2));
                    Assert.That(session.Results.Count, Is.EqualTo(12));
                }
        }

        [Test]
        public void ChampionshipWaitsForEarlierAiBeforeAnnouncingAParallelControlledGame()
        {
            var format = new CompetitionFormatDefinition("format", "Interleaved branches", 1, 3, new[]
            {
                League("base", 1, "all", 1, 3, new StageQualificationDefinition("overall", 3)),
                new CompetitionStageDefinition("late", "Late", "league", 1, "all", 1, 3, Points, Tiebreakers,
                    source: new StageSourceDefinition("base", "qualified")),
                new CompetitionStageDefinition("early", "Early", "league", 1, "all", 1, 3, Points, Tiebreakers,
                    new StageQualificationDefinition("overall", 2), source: new StageSourceDefinition("base", "qualified")),
                Knockout("middle", 1, source: new StageSourceDefinition("early", "qualified"))
            }, new CompetitionMatchRules(5), championStageId: "middle");
            var catalog = Catalog(format, new[] { Schedule("base", 1, 3), Schedule("late", 10, 3), Schedule("early", 4, 3), Schedule("middle", 8, 1) });
            var season = CompetitionSession.Create(catalog, "edition", "interleaved", "club-3");
            while (!season.IsStageComplete("base"))
            { if (season.NextFixture != null) PlayControlled(season, 5, 0); else season.SimulateNextRound(); AssertRestore(season); }
            PlayControlled(season, 5, 0); PlayControlled(season, 5, 0);
            var future = season.Fixtures.First(value => value.StageId == "late" && value.IncludesClub("club-3"));
            Assert.That(season.NextFixture, Is.Null, "The last AI game in early can still create a nearer final.");
            Assert.Throws<InvalidOperationException>(() => season.BeginFixture(future.Id));
            AssertRestore(season);
            Assert.That(season.SimulateNextRound(), Is.EqualTo(1));
            Assert.That(season.NextFixture.StageId, Is.EqualTo("middle"));
            Assert.That(season.NextFixture.Date, Is.EqualTo(Date(8)));
            Assert.That(season.Results.Any(value => value.FixtureId == future.Id), Is.False);
            AssertRestore(season); CompleteAndRestore(season);
        }

        [Test]
        public void WaitingSimulationStopsBeforeTheControlledDateInsideAMultiDayRound()
        {
            var season = CompetitionSession.Create(BranchCatalog(), "edition", "split-round", "club-1");
            var fixture = season.Fixtures.Single(value => value.StageId == "quarters" && value.IncludesClub("club-1"));
            Assert.That(fixture.Date, Is.EqualTo(Date(2)));
            Assert.That(season.NextFixture, Is.Null);
            Assert.That(season.SimulateNextRound(), Is.EqualTo(2));
            Assert.That(season.NextFixture.Id, Is.EqualTo(fixture.Id));
            Assert.That(season.GetResult(fixture.Id), Is.Null, "Preparing the earlier AI date must leave the user fixture playable.");
            AssertRestore(season); PlayControlled(season, 3, 0); AssertRestore(season);
        }

        [TestCase("seeded")]
        [TestCase("first-listed")]
        [TestCase("draw")]
        [TestCase("neutral")]
        public void VenuePoliciesRetainReproduciblePairingsAndReturnSides(string venue)
        {
            var format = Format(2, new[] { Knockout("final", 2, venue: venue) });
            var schedule = new CompetitionStageSchedule("final", new[] { Date(1), Date(2) }, neutralStadiumId: venue == "neutral" ? "stadium" : null);
            var catalog = Catalog(format, new[] { schedule });
            var session = CompetitionSession.Create(catalog, "edition", "season", "club-1");
            var first = session.Fixtures[0]; var second = session.Fixtures[1];
            Assert.That(second.HomeClubId, Is.EqualTo(first.AwayClubId));
            Assert.That(second.AwayClubId, Is.EqualTo(first.HomeClubId));
            if (venue == "seeded") Assert.That(second.HomeClubId, Is.EqualTo("club-1"));
            if (venue == "first-listed") Assert.That(first.HomeClubId, Is.EqualTo("club-1"));
            Assert.That(session.Fixtures.All(value => value.IsNeutral), Is.EqualTo(venue == "neutral"));
            if (venue == "neutral") Assert.That(session.Fixtures.All(value => value.StadiumId == "stadium"), Is.True);
            AssertRestore(session); CompleteAndRestore(session);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AggregateUsesConfiguredAwayGoalsBeforePenalties(bool awayGoals)
        {
            var stage = Knockout("final", 2, awayGoals: awayGoals);
            var session = CompetitionSession.Create(Catalog(Format(2, new[] { stage }), new[] { Schedule("final", 1, 2) }), "edition", "season", "club-1");
            PlayScore(session, 2, 1); PlayScore(session, 1, 0);
            var decider = session.Results.Last();
            Assert.That(decider.HomePenalties.HasValue, Is.EqualTo(!awayGoals));
            if (awayGoals) Assert.That(session.ChampionClubId, Is.EqualTo("club-1"));
            else Assert.That(session.ChampionClubId, Is.EqualTo(decider.HomePenalties > decider.AwayPenalties ? decider.HomeClubId : decider.AwayClubId));
            Assert.That(session.Standings[0].ClubId, Is.EqualTo(session.ChampionClubId));
            AssertRestore(session);
        }

        [Test]
        public void HigherSeedCanWinDrawnTieAndAnAccessOnlyFormatHasNoChampion()
        {
            var stage = Knockout("access", 1, tiedWinner: "higher-seed");
            var format = new CompetitionFormatDefinition("format", "Access", 1, 4, new[] { stage }, new CompetitionMatchRules(5),
                new[] { new CompetitionOutcomeDefinition("access", "Access", "access", "promotion", "overall", 1, 2) }, hasChampionStage: false);
            var session = CompetitionSession.Create(Catalog(format, new[] { Schedule("access", 1, 1) }), "edition", "season", "club-1");
            PlayControlled(session, 0, 0);
            Assert.That(session.IsComplete, Is.True);
            Assert.That(session.ChampionClubId, Is.Null);
            Assert.That(session.GetStageStandings("access").Take(2).Select(value => value.ClubId), Does.Contain("club-1"));
            Assert.That(session.Results.All(value => !value.HomePenalties.HasValue), Is.True);
            Assert.That(session.QualifiedOutcomes.Count, Is.EqualTo(2)); AssertRestore(session);
        }

        [Test]
        public void ThreeGroupsCanCrossPairWithoutFacingTheSameGroupAndDrawIsStable()
        {
            var groups = new[] { Group("a", "club-1", "club-2"), Group("b", "club-3", "club-4"), Group("c", "club-5", "club-6") };
            var format = new CompetitionFormatDefinition("format", "Groups", 1, 6, new[]
            { League("groups", 3, "same-group", 1, 1, new StageQualificationDefinition("per-group", 2)), Knockout("access", 1, pairing: "cross-group") },
                new CompetitionMatchRules(5), hasChampionStage: false);
            var session = CompetitionSession.Create(Catalog(format, new[] { Schedule("groups", 1, 1, groups), Schedule("access", 3, 1) }), "edition", "season", "club-1");
            session.SimulateNextRound();
            foreach (var fixture in session.Fixtures.Where(value => value.StageId == "access"))
                Assert.That(groups.Single(value => value.ClubIds.Contains(fixture.HomeClubId)).Id,
                    Is.Not.EqualTo(groups.Single(value => value.ClubIds.Contains(fixture.AwayClubId)).Id));
            AssertRestore(session); CompleteAndRestore(session);
        }

        [Test]
        public void AuthoredPaulistaFixtureDatesAndCumulativeSeedingSurviveTheGenericPath()
        {
            var legacy = PaulistaCompetitionTests.Catalog(); var edition = legacy.CompetitionEditions[0];
            var format = Format(16, new[]
            {
                League("league", 1, "authored", 1, 8, new StageQualificationDefinition("overall", 8)),
                Knockout("quarters", 1, new StageQualificationDefinition("winners", 4, new[] { "league", "quarters" })),
                Knockout("semis", 1, new StageQualificationDefinition("winners", 2, new[] { "league", "quarters", "semis" })),
                Knockout("final", 2)
            }, new[] { new CompetitionOutcomeDefinition("relegation", "Relegation", "league", "relegation", "overall", 15, 16) });
            var schedules = new[]
            {
                new CompetitionStageSchedule("league", edition.RoundDates, authoredFixtures: edition.ScheduledFixtures),
                new CompetitionStageSchedule("quarters", new[] { edition.PlayoffDates.Take(4).Min() }, fixtureDates: edition.PlayoffDates.Take(4)),
                new CompetitionStageSchedule("semis", new[] { edition.PlayoffDates.Skip(4).Take(2).Min() }, fixtureDates: edition.PlayoffDates.Skip(4).Take(2)),
                new CompetitionStageSchedule("final", edition.PlayoffDates.Skip(6))
            };
            var genericEdition = new CompetitionEditionDefinition("edition", "competition", "Generic", edition.ParticipantClubIds, format, schedules);
            var catalog = new DatabaseCatalog("database", 1, legacy.Clubs, legacy.Players, legacy.Memberships,
                new[] { new CompetitionDefinition("competition", "Generic", format.Id) }, new[] { genericEdition }, legacy.Countries, legacy.Stadiums,
                competitionFormats: new[] { format });
            var session = CompetitionSession.Create(catalog, "edition", "season", "club-1");
            Assert.That(session.Fixtures.Select(value => value.Id), Is.EquivalentTo(edition.ScheduledFixtures.Select(value => value.Id)));
            while (!session.IsStageComplete("quarters")) { session.SimulateNextRound(); AssertRestore(session); }
            var winners = session.GetStageStandings("quarters").Take(4).Select(value => value.ClubId).ToArray();
            var cumulative = StandingsCalculator.Calculate(edition.ParticipantClubIds, edition.Rules, session.Results, "season");
            // Sporting order is decisive here; a seeded lot remains profile-specific.
            var participants = session.StageStates.Single(value => value.StageId == "semis").ParticipantClubIds;
            var winnerPoints = cumulative.Where(value => winners.Contains(value.ClubId)).ToDictionary(value => value.ClubId, value => value.Points);
            Assert.That(participants.Select(value => winnerPoints[value]), Is.Ordered.Descending);
            CompleteAndRestore(session);
            Assert.That(session.Results.Count, Is.EqualTo(72)); Assert.That(session.RelegatedClubIds.Count, Is.EqualTo(2));
        }

        [Test]
        public void RestoreRejectsTamperedBranchFixturesSupplementAndCalendarAndKeepsCorrelatedResultsIdempotent()
        {
            var catalog = BranchCatalog(); var session = CompetitionSession.Create(catalog, "edition", "season", "club-1");
            session.SimulateNextRound(); // Finish the earlier AI day before the user's quarterfinal.
            var first = session.NextFixture; var abandoned = session.BeginFixture(first.Id, "old-execution");
            Assert.That(session.AbortFixture(first.Id, abandoned.ExecutionId), Is.True);
            Assert.That(session.CompleteFixture(new FixtureResult(first.Id, abandoned.ExecutionId, first.HomeClubId, first.AwayClubId, 0, 0)), Is.EqualTo(FixtureCompletion.IgnoredStale));
            PlayControlled(session, 0, 5); session.SimulateNextRound(); AssertRestore(session);
            var valid = session.CaptureSnapshot(); var fixture = valid.Fixtures.Last();
            var altered = new FixtureDefinition(fixture.Id, fixture.Round, fixture.Date, fixture.HomeClubId, fixture.AwayClubId,
                fixture.StadiumId, "forged-stage", fixture.TieId, fixture.Leg, fixture.IsNeutral);
            Assert.Throws<ArgumentException>(() => CompetitionSession.Restore(catalog, Snapshot(valid, valid.Fixtures.Select(value => value.Id == fixture.Id ? altered : value))));
            var result = valid.Results.First();
            Assert.That(session.CompleteFixture(result), Is.EqualTo(FixtureCompletion.AlreadyApplied));
            var tampered = new FixtureResult(result.FixtureId, result.ExecutionId, result.HomeClubId, result.AwayClubId, result.HomeGoals, result.AwayGoals,
                result.IsSimulated, result.HomeYellowCards + 1, result.AwayYellowCards, result.HomeRedCards, result.AwayRedCards,
                result.HomePenalties, result.AwayPenalties, true);
            Assert.Throws<ArgumentException>(() => CompetitionSession.Restore(catalog, Snapshot(valid, results: valid.Results.Select(value => value.FixtureId == result.FixtureId ? tampered : value))));
            Assert.Throws<ArgumentException>(() => CompetitionSession.Restore(catalog, new CompetitionSnapshot(valid.SeasonId, valid.DatabaseId, valid.DatabaseRevision,
                valid.EditionId, valid.ControlledClubId, valid.Fixtures, valid.Results, valid.UsedExecutionIds, true, Date(0))));
        }

        [Test]
        public void DefinitionsRejectCyclesInvalidSeedsUnknownRulesAndInvalidCalendarsBeforeStarting()
        {
            Assert.Throws<ArgumentException>(() => Format(8, new[] { Knockout("a", 1, source: new StageSourceDefinition("b", "winners")), Knockout("b", 1) }));
            Assert.Throws<ArgumentException>(() => Format(8, new[] { Knockout("a", 1), Knockout("b", 1) }));
            Assert.Throws<ArgumentException>(() => Format(4, new[] { Knockout("not-final", 1) }));
            Assert.Throws<ArgumentException>(() => new CompetitionStageDefinition("s", "Stage", "league", 1, "all", 1, 3, Points, new[] { "unknown", "seeded-draw" }));
            Assert.Throws<ArgumentException>(() => Knockout("neutral", 2, venue: "neutral", awayGoals: true));
            var format = Format(4, new[] { League("league", 1, "all", 1, 3, new StageQualificationDefinition("overall", 2)), Knockout("final", 1) });
            Assert.Throws<ArgumentException>(() => Catalog(format, new[] { Schedule("league", 1, 2), Schedule("final", 4, 1) }));
            Assert.Throws<ArgumentException>(() => Catalog(format, new[] { Schedule("league", 1, 3), Schedule("final", 3, 1) }));
            var grouped = Format(4, new[] { League("groups", 2, "same-group", 1, 1) });
            Assert.Throws<ArgumentException>(() => Catalog(grouped, new[] { Schedule("groups", 1, 1,
                new[] { Group("a", "club-1", "club-2"), Group("b", "club-2", "club-4") }) }));
            var overlapping = new CompetitionFormatDefinition("format", "Shared winners", 1, 4, new[]
            {
                Knockout("semis", 1, new StageQualificationDefinition("winners", 2)), Knockout("final-a", 1),
                Knockout("final-b", 1, source: new StageSourceDefinition("semis", "winners"))
            }, new CompetitionMatchRules(5), championStageId: "final-a");
            Assert.Throws<ArgumentException>(() => Catalog(overlapping, new[]
            { Schedule("semis", 1, 1), Schedule("final-a", 3, 1), Schedule("final-b", 3, 1) }));
        }

        private static DatabaseCatalog BranchCatalog()
        {
            var stages = new[]
            {
                Knockout("quarters", 1, new StageQualificationDefinition("winners", 4)),
                Knockout("semis", 1, new StageQualificationDefinition("winners", 2)),
                Knockout("final", 2),
                Knockout("playoff", 2, source: new StageSourceDefinition("quarters", "losers"))
            };
            var format = new CompetitionFormatDefinition("format", "Branched cup", 1, 8, stages, new CompetitionMatchRules(5),
                new[] { new CompetitionOutcomeDefinition("promotion", "Promotion", "playoff", "promotion", "overall", 1, 2) }, "final");
            return Catalog(format, new[]
            {
                new CompetitionStageSchedule("quarters", new[] { Date(1) }, fixtureDates: new[] { Date(2), Date(1), Date(1), Date(2) }),
                Schedule("semis", 4, 1), new CompetitionStageSchedule("final", new[] { Date(8), Date(10) }),
                new CompetitionStageSchedule("playoff", new[] { Date(4), Date(6) })
            });
        }
        private static CompetitionFormatDefinition Format(int count, IEnumerable<CompetitionStageDefinition> stages, IEnumerable<CompetitionOutcomeDefinition> outcomes = null)
            => new CompetitionFormatDefinition("format", "Format", 1, count, stages, new CompetitionMatchRules(5), outcomes);
        private static CompetitionStageDefinition League(string id, int groups, string opponents, int legs, int rounds, StageQualificationDefinition qualification = null)
            => new CompetitionStageDefinition(id, id, "league", groups, opponents, legs, rounds, Points, Tiebreakers, qualification);
        private static CompetitionStageDefinition Knockout(string id, int legs, StageQualificationDefinition qualification = null,
            string venue = "seeded", string pairing = "seeded", bool awayGoals = false, string tiedWinner = "penalties", StageSourceDefinition source = null)
            => new CompetitionStageDefinition(id, id, "knockout", 1, "all", legs, legs, Points, Tiebreakers, qualification, pairing, venue, awayGoals, tiedWinner, source);
        private static CompetitionGroupDefinition Group(string id, params string[] clubs) => new CompetitionGroupDefinition(id, id, clubs);
        private static CompetitionStageSchedule Schedule(string id, int day, int rounds, IEnumerable<CompetitionGroupDefinition> groups = null)
            => new CompetitionStageSchedule(id, Enumerable.Range(day, rounds).Select(Date), groups);
        private static GameDate Date(int day)
        { var value = new DateTime(2026, 1, 1).AddDays(day); return new GameDate(value.Year, value.Month, value.Day); }
        private static DatabaseCatalog Catalog(CompetitionFormatDefinition format, IEnumerable<CompetitionStageSchedule> schedules)
        {
            var clubs = Enumerable.Range(1, format.ParticipantCount).Select(value => new ClubDefinition("club-" + value, "Club " + value, "BR", "City", stadiumId: "stadium", stateCode: "SP")).ToArray();
            var edition = new CompetitionEditionDefinition("edition", "competition", "Edition", clubs.Select(value => value.Id), format, schedules);
            return new DatabaseCatalog("database", 1, clubs, Array.Empty<PlayerDefinition>(), Array.Empty<RosterMembership>(),
                new[] { new CompetitionDefinition("competition", "Competition", format.Id) }, new[] { edition },
                new[] { new CountryDefinition("BR", "Brazil") }, new[] { new StadiumDefinition("stadium", "Stadium", "BR", "City") }, competitionFormats: new[] { format });
        }
        private static void PlayControlled(CompetitionSession session, int ownGoals, int opponentGoals)
        {
            var fixture = session.NextFixture;
            PlayScore(session, fixture.HomeClubId == session.ControlledClubId ? ownGoals : opponentGoals,
                fixture.HomeClubId == session.ControlledClubId ? opponentGoals : ownGoals);
        }
        private static void PlayScore(CompetitionSession session, int homeGoals, int awayGoals)
        {
            var fixture = session.NextFixture; var execution = session.BeginFixture(fixture.Id);
            session.CompleteFixture(new FixtureResult(fixture.Id, execution.ExecutionId, fixture.HomeClubId, fixture.AwayClubId, homeGoals, awayGoals));
        }
        private static void CompleteAndRestore(CompetitionSession session)
        {
            var rounds = 0;
            while (!session.IsComplete)
            {
                Assert.That(session.SimulateNextRound(), Is.GreaterThan(0)); AssertRestore(session);
                Assert.That(++rounds, Is.LessThan(150));
            }
        }
        private static void AssertRestore(CompetitionSession session)
        {
            var restored = CompetitionSession.Restore(session.Catalog, session.CaptureSnapshot());
            Assert.That(restored.Fixtures.Select(value => value.Id), Is.EqualTo(session.Fixtures.Select(value => value.Id)));
            Assert.That(restored.Results.Select(value => value.FixtureId), Is.EqualTo(session.Results.Select(value => value.FixtureId)));
            Assert.That(restored.ChampionClubId, Is.EqualTo(session.ChampionClubId));
            Assert.That(restored.QualifiedOutcomes.Select(value => value.OutcomeId + "|" + value.ClubId), Is.EqualTo(session.QualifiedOutcomes.Select(value => value.OutcomeId + "|" + value.ClubId)));
        }
        private static CompetitionSnapshot Snapshot(CompetitionSnapshot source, IEnumerable<FixtureDefinition> fixtures = null, IEnumerable<FixtureResult> results = null)
            => new CompetitionSnapshot(source.SeasonId, source.DatabaseId, source.DatabaseRevision, source.EditionId, source.ControlledClubId,
                fixtures ?? source.Fixtures, results ?? source.Results, source.UsedExecutionIds, source.DailyProgress, source.CurrentDate);
    }
}
