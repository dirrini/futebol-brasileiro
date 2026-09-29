using System;
using System.Collections.Generic;
using System.Linq;

namespace FStudio.FootballWorld.Domain
{
    public sealed partial class CompetitionEditionDefinition
    {
        public CompetitionEditionDefinition(string id, string competitionId, string name, IEnumerable<string> participantClubIds,
            CompetitionFormatDefinition format, IEnumerable<CompetitionStageSchedule> stageSchedules)
        {
            Id = DomainValidation.Id(id, nameof(id)); CompetitionId = DomainValidation.Id(competitionId, nameof(competitionId));
            Name = DomainValidation.Name(name, nameof(name)); Format = format ?? throw new ArgumentNullException(nameof(format));
            ParticipantClubIds = CompetitionRuleValidation.Ids(participantClubIds ?? throw new ArgumentNullException(nameof(participantClubIds)), nameof(participantClubIds));
            if (ParticipantClubIds.Count != format.ParticipantCount) throw new ArgumentException("Edition participants do not match the format field size.");
            var schedules = new List<CompetitionStageSchedule>(stageSchedules ?? throw new ArgumentNullException(nameof(stageSchedules)));
            if (schedules.Count != format.Stages.Count || schedules.Any(value => value == null) ||
                schedules.Select(value => value.StageId).Distinct(StringComparer.Ordinal).Count() != schedules.Count)
                throw new ArgumentException("Every format stage needs exactly one calendar.");
            var ordered = new List<CompetitionStageSchedule>();
            var dates = new List<GameDate>();
            var fixtureIds = new HashSet<string>(StringComparer.Ordinal);
            var stageEndDates = new Dictionary<string, GameDate>(StringComparer.Ordinal);
            var totalFixtures = 0;
            for (var index = 0; index < format.Stages.Count; index++)
            {
                var stage = format.Stages[index];
                var schedule = schedules.SingleOrDefault(value => value.StageId == stage.Id) ?? throw new ArgumentException("Unknown or missing stage calendar.");
                var count = format.GetStageParticipantCount(stage.Id);
                if (schedule.RoundDates.Count != stage.RoundCount) throw new ArgumentException("Stage calendar requires one date per declared round.");
                var source = format.GetStageSource(stage.Id);
                if (source != null && schedule.RoundDates[0].CompareTo(stageEndDates[source.StageId]) <= 0)
                    throw new ArgumentException("Each stage must start after all games in its source stage.");
                ValidateGroups(stage, schedule, count, index == 0);
                if ((stage.Venue == "neutral") != (schedule.NeutralStadiumId != null))
                    throw new ArgumentException("A neutral stage must declare its stadium, and other venues cannot declare a neutral stadium.");
                if (schedule.AuthoredFixtures.Count > 0 && (index != 0 || stage.Kind != "league"))
                    throw new ArgumentException("Authored clubs can be scheduled only in the initial league stage.");
                if (schedule.FixtureDates.Count > 0 && (stage.Kind != "knockout" || schedule.FixtureDates.Count != count / 2 * stage.Legs))
                    throw new ArgumentException("Knockout fixture dates require one date per pairing and leg.");
                if (stage.Kind == "league")
                {
                    if (schedule.AuthoredFixtures.Count == 0)
                    {
                        if (stage.Opponents == "authored") throw new ArgumentException("An authored league requires its authored fixtures.");
                        var rotationCount = stage.Opponents == "same-group" ? count / stage.GroupCount : count;
                        var requiredRounds = (rotationCount % 2 == 0 ? rotationCount - 1 : rotationCount) * stage.Legs;
                        if (stage.RoundCount != requiredRounds) throw new ArgumentException("Generated stage requires exactly " + requiredRounds + " round dates.");
                        totalFixtures += stage.Opponents == "same-group" ? count * (count / stage.GroupCount - 1) / 2 * stage.Legs :
                            stage.Opponents == "cross-group" ? count * (count - count / stage.GroupCount) / 2 * stage.Legs : count * (count - 1) / 2 * stage.Legs;
                    }
                    else { ValidateAuthoredStage(stage, schedule, fixtureIds); totalFixtures += schedule.AuthoredFixtures.Count; }
                }
                else totalFixtures += count / 2 * stage.Legs;
                if (totalFixtures > 4096) throw new ArgumentException("Edition exceeds the supported 4096-fixture budget.");
                for (var fixtureIndex = 0; fixtureIndex < schedule.FixtureDates.Count; fixtureIndex++)
                {
                    var leg = fixtureIndex / (count / 2);
                    var date = schedule.FixtureDates[fixtureIndex];
                    if (date.CompareTo(schedule.RoundDates[leg]) < 0 ||
                        (leg + 1 < stage.Legs && date.CompareTo(schedule.RoundDates[leg + 1]) >= 0))
                        throw new ArgumentException("A knockout date must be inside its leg window.");
                }
                stageEndDates.Add(stage.Id, schedule.RoundDates.Concat(schedule.FixtureDates).Concat(schedule.AuthoredFixtures.Select(value => value.Date)).Max());
                dates.AddRange(schedule.RoundDates); ordered.Add(schedule);
            }
            StageSchedules = ordered.AsReadOnly(); RoundDates = dates.AsReadOnly(); declarativeEndDate = stageEndDates.Values.Max();
            ScheduledFixtures = Array.Empty<FixtureDefinition>(); PlayoffDates = Array.Empty<GameDate>();
            ValidateConcurrentStageDates();
        }

        private void ValidateConcurrentStageDates()
        {
            for (var first = 0; first < StageSchedules.Count; first++)
                for (var second = first + 1; second < StageSchedules.Count; second++)
                {
                    var left = StageSchedules[first]; var right = StageSchedules[second];
                    if (!EffectiveDates(left).Intersect(EffectiveDates(right)).Any()) continue;
                    var leftPath = KnockoutCohort(left.StageId); var rightPath = KnockoutCohort(right.StageId);
                    if (!leftPath.Any(pair => rightPath.TryGetValue(pair.Key, out var choice) && choice != pair.Value))
                        throw new ArgumentException("Parallel stages with potentially shared participants cannot play on the same date.");
                }
        }

        private static IEnumerable<GameDate> EffectiveDates(CompetitionStageSchedule schedule)
            => schedule.AuthoredFixtures.Count > 0 ? schedule.AuthoredFixtures.Select(value => value.Date) :
                schedule.FixtureDates.Count > 0 ? schedule.FixtureDates : schedule.RoundDates;

        private Dictionary<string, string> KnockoutCohort(string stageId)
        {
            var path = new Dictionary<string, string>(StringComparer.Ordinal);
            var source = Format.GetStageSource(stageId);
            while (source != null)
            {
                var parent = Format.Stages.Single(value => value.Id == source.StageId);
                if (parent.Kind == "knockout") path.Add(parent.Id, source.Selection == "losers" ? "losers" : "winners");
                source = Format.GetStageSource(parent.Id);
            }
            return path;
        }

        private void ValidateGroups(CompetitionStageDefinition stage, CompetitionStageSchedule schedule, int count, bool initial)
        {
            if (stage.Kind == "knockout")
            {
                if (schedule.Groups.Count != 0) throw new ArgumentException("Knockout stages do not contain league groups.");
                return;
            }
            if (schedule.Groups.Count == 0)
            {
                if (initial && stage.GroupCount > 1) throw new ArgumentException("Initial league groups require explicit club assignments.");
                return;
            }
            if (schedule.Groups.Count != stage.GroupCount) throw new ArgumentException("Calendar group count must match the stage rule.");
            var clubs = new HashSet<string>(StringComparer.Ordinal); var ranks = new HashSet<int>();
            foreach (var group in schedule.Groups)
            {
                if (initial)
                {
                    if (group.SeedRanks.Count != 0 || group.ClubIds.Count != count / stage.GroupCount)
                        throw new ArgumentException("Initial groups require equally sized club ID lists.");
                    foreach (var clubId in group.ClubIds)
                        if (!ParticipantClubIds.Contains(clubId) || !clubs.Add(clubId)) throw new ArgumentException("Initial groups must partition edition participants.");
                }
                else
                {
                    if (group.ClubIds.Count != 0 || group.SeedRanks.Count != count / stage.GroupCount)
                        throw new ArgumentException("Later groups require equally sized incoming seed rank lists.");
                    foreach (var rank in group.SeedRanks)
                        if (rank > count || !ranks.Add(rank)) throw new ArgumentException("Later groups must partition the incoming seed ranks.");
                }
            }
        }

        private void ValidateAuthoredStage(CompetitionStageDefinition stage, CompetitionStageSchedule schedule, HashSet<string> fixtureIds)
        {
            var collisions = new HashSet<string>(StringComparer.Ordinal);
            var pairCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var sides = new HashSet<string>(StringComparer.Ordinal);
            var groups = schedule.Groups.SelectMany(group => group.ClubIds.Select(id => new { id, group.Id }))
                .ToDictionary(value => value.id, value => value.Id, StringComparer.Ordinal);
            foreach (var fixture in schedule.AuthoredFixtures)
            {
                if (!fixtureIds.Add(fixture.Id) || fixture.StageId != null || fixture.TieId != null || fixture.IsNeutral || fixture.Leg != 1 ||
                    fixture.Round > stage.RoundCount || !ParticipantClubIds.Contains(fixture.HomeClubId) || !ParticipantClubIds.Contains(fixture.AwayClubId))
                    throw new ArgumentException("Authored fixtures require unique IDs, local rounds and known participants without runtime context.");
                var round = fixture.Round - 1;
                if (fixture.Date.CompareTo(schedule.RoundDates[round]) < 0 ||
                    (round + 1 < stage.RoundCount && fixture.Date.CompareTo(schedule.RoundDates[round + 1]) >= 0))
                    throw new ArgumentException("Authored fixture lies outside its declared round window.");
                foreach (var club in new[] { fixture.HomeClubId, fixture.AwayClubId })
                    if (!collisions.Add(fixture.Round + "|" + club)) throw new ArgumentException("A club cannot play twice in the same round.");
                if (stage.Opponents == "same-group" || stage.Opponents == "cross-group")
                {
                    var same = stage.GroupCount == 1 || groups[fixture.HomeClubId] == groups[fixture.AwayClubId];
                    if ((stage.Opponents == "same-group") != same) throw new ArgumentException("Authored fixture violates the stage opponent policy.");
                }
                var pair = PairKey(fixture.HomeClubId, fixture.AwayClubId);
                pairCounts.TryGetValue(pair, out var played); pairCounts[pair] = played + 1;
                if (played >= stage.Legs || !sides.Add(fixture.HomeClubId + "|" + fixture.AwayClubId))
                    throw new ArgumentException("Authored pairings exceed the declared legs or repeat the same home side.");
                if (stage.Venue == "neutral" && fixture.StadiumId != null && fixture.StadiumId != schedule.NeutralStadiumId)
                    throw new ArgumentException("An authored neutral fixture conflicts with its stage stadium.");
            }
            if (stage.Opponents != "authored")
                for (var first = 0; first < ParticipantClubIds.Count; first++)
                    for (var second = first + 1; second < ParticipantClubIds.Count; second++)
                    {
                        var home = ParticipantClubIds[first]; var away = ParticipantClubIds[second];
                        var same = stage.GroupCount == 1 || groups[home] == groups[away];
                        var required = stage.Opponents == "all" || (stage.Opponents == "same-group" ? same : !same);
                        if (required && (!pairCounts.TryGetValue(PairKey(home, away), out var played) || played != stage.Legs))
                            throw new ArgumentException("Authored fixtures must cover every declared opponent pair and leg.");
                    }
        }

        private static string PairKey(string first, string second) => string.CompareOrdinal(first, second) < 0 ? first + "|" + second : second + "|" + first;
    }
}
