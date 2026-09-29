using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FStudio.FootballWorld.DataContracts;
using FStudio.FootballWorld.Domain;
using Newtonsoft.Json.Linq;

namespace FStudio.FootballWorld.Infrastructure.Importing
{
    public sealed partial class JsonDatabaseImporter
    {
        private static List<string> ReadStrings(JToken token, string path, int min, int max,
            List<DatabaseImportError> errors, Func<JToken, string, List<DatabaseImportError>, string> read)
        {
            var values = new List<string>();
            ReadBoundedItems(token, path, min, max, errors, (value, valuePath) => values.Add(read(value, valuePath, errors)));
            return values;
        }
        private static string Choice(JToken token, string path, List<DatabaseImportError> errors, params string[] choices)
        {
            var value = String(token, path, errors);
            if (value != null && !choices.Contains(value)) Error(errors, "unsupported_rule_value", path, "Supported values: " + string.Join(", ", choices) + ".");
            return value;
        }
        private static bool Boolean(JToken token, string path, List<DatabaseImportError> errors)
        {
            if (token == null) return false;
            if (token.Type != JTokenType.Boolean) { Error(errors, "invalid_type", path, "Expected a boolean."); return false; }
            return (bool)token;
        }
        private static string MediaUri(JObject obj, string field, string path, List<DatabaseImportError> errors)
        {
            if (obj.Property(field) == null) return null;
            var value = Text(obj[field], path + "." + field, 2048, false, errors);
            if (value != null && !Regex.IsMatch(value, @"\A(?:https://[^\s/?#]+(?:[/?#][^\s]*)?|[A-Za-z0-9_-][A-Za-z0-9._-]*(?:/[A-Za-z0-9_-][A-Za-z0-9._-]*)*)\z"))
                Error(errors, "invalid_media_uri", path + "." + field, "Use HTTPS or a safe relative package path without traversal.");
            else if (value != null && value.StartsWith("https://", StringComparison.Ordinal) &&
                (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo) || value.Contains("\\")))
                Error(errors, "invalid_media_uri", path + "." + field, "Use an absolute HTTPS URL without credentials or backslashes.");
            return value;
        }
        private static CompetitionData ReadCompetition(JToken item, string path, int version, List<DatabaseImportError> errors)
        {
            var obj = ObjectWithOptionalFields(item, path, errors, new[] {"id", "name"}, version >= 6
                ? new[] {"defaultFormatId", "level", "reputation", "prizeLevel", "logoUri", "trophyImageUri", "trophyModelUri", "eligibility", "qualificationRoutes", "prizes"}
                : Array.Empty<string>());
            if (obj == null) return null;
            CompetitionEligibilityData eligibility = null;
            if (obj.Property("eligibility") != null)
            {
                var ep = path + ".eligibility";
                var value = Object(obj["eligibility"], ep, errors, "countryCodes", "stateCodes", "allowedClubIds", "excludedClubIds");
                if (value != null) eligibility = new CompetitionEligibilityData(
                    ReadStrings(value["countryCodes"], ep + ".countryCodes", 0, 300, errors, (t, p, e) => Code(t, p, 2, e)),
                    ReadStrings(value["stateCodes"], ep + ".stateCodes", 0, 128, errors, StateCode),
                    ReadStrings(value["allowedClubIds"], ep + ".allowedClubIds", 0, 1024, errors, Id),
                    ReadStrings(value["excludedClubIds"], ep + ".excludedClubIds", 0, 1024, errors, Id));
            }
            var routes = new List<CompetitionQualificationRouteData>();
            if (obj.Property("qualificationRoutes") != null) ReadBoundedItems(obj["qualificationRoutes"], path + ".qualificationRoutes", 0, 128, errors, (value, vp) =>
            {
                var route = Object(value, vp, errors, "outcomeId", "targetCompetitionId");
                if (route != null) routes.Add(new CompetitionQualificationRouteData(Id(route["outcomeId"], vp + ".outcomeId", errors), Id(route["targetCompetitionId"], vp + ".targetCompetitionId", errors)));
            });
            CompetitionPrizesData prizes = null;
            if (obj.Property("prizes") != null)
            {
                var pp = path + ".prizes"; var value = Object(obj["prizes"], pp, errors, "currency", "participation", "win", "draw", "rankingAwards");
                if (value != null)
                {
                    var awards = new List<CompetitionRankingAwardData>();
                    ReadBoundedItems(value["rankingAwards"], pp + ".rankingAwards", 0, 128, errors, (awardToken, ap) =>
                    {
                        var award = Object(awardToken, ap, errors, "stageId", "ranking", "fromRank", "toRank", "amount");
                        if (award != null) awards.Add(new CompetitionRankingAwardData(Id(award["stageId"], ap + ".stageId", errors),
                            Choice(award["ranking"], ap + ".ranking", errors, "overall", "per-group"), Integer(award["fromRank"], ap + ".fromRank", 1, 64, errors),
                            Integer(award["toRank"], ap + ".toRank", 1, 64, errors), Integer(award["amount"], ap + ".amount", 0, int.MaxValue, errors)));
                    });
                    prizes = new CompetitionPrizesData(Code(value["currency"], pp + ".currency", 3, errors),
                        Integer(value["participation"], pp + ".participation", 0, int.MaxValue, errors), Integer(value["win"], pp + ".win", 0, int.MaxValue, errors),
                        Integer(value["draw"], pp + ".draw", 0, int.MaxValue, errors), awards);
                }
            }
            return new CompetitionData(Id(obj["id"], path + ".id", errors), Name(obj["name"], path + ".name", errors),
                obj.Property("defaultFormatId") == null ? null : Id(obj["defaultFormatId"], path + ".defaultFormatId", errors),
                obj.Property("level") == null ? null : Choice(obj["level"], path + ".level", errors, "state", "regional", "national", "continental", "world"),
                OptionalInteger(obj, "reputation", path, 0, 100, errors), OptionalInteger(obj, "prizeLevel", path, 0, 100, errors),
                MediaUri(obj, "logoUri", path, errors), MediaUri(obj, "trophyImageUri", path, errors), MediaUri(obj, "trophyModelUri", path, errors), eligibility, routes, prizes);
        }
        private static void ReadCompetitionFormats(JObject root, List<CompetitionFormatData> formats, List<DatabaseImportError> errors)
        {
            ReadBoundedItems(root["competitionFormats"], "$.competitionFormats", 0, 128, errors, (item, path) =>
            {
                var obj = ObjectWithOptionalFields(item, path, errors, new[] {"id", "name", "version", "participantCount", "matchRules", "stages", "outcomes"}, new[] {"championStageId"}); if (obj == null) return;
                var version = Integer(obj["version"], path + ".version", 1, int.MaxValue, errors);
                if (version != 1) Error(errors, "unsupported_rule_version", path + ".version", "Only format version 1 is supported.");
                var match = Object(obj["matchRules"], path + ".matchRules", errors, "maxSubstitutions");
                var substitutions = match == null ? 0 : Integer(match["maxSubstitutions"], path + ".matchRules.maxSubstitutions", 0, 11, errors);
                var stages = new List<CompetitionStageData>();
                ReadBoundedItems(obj["stages"], path + ".stages", 1, 16, errors, (stageToken, sp) =>
                {
                    var stage = ObjectWithOptionalFields(stageToken, sp, errors, new[] {"id", "name", "kind", "groupCount", "opponents", "legs", "roundCount", "points", "tieBreakers", "qualification", "pairing", "venue", "awayGoals", "tiedWinner"}, new[] {"source"});
                    if (stage == null) return;
                    var points = Object(stage["points"], sp + ".points", errors, "win", "draw", "loss");
                    var qualification = ObjectWithOptionalFields(stage["qualification"], sp + ".qualification", errors, new[] {"mode", "count"}, new[] {"rankingStageIds"});
                    if (points == null || qualification == null) return;
                    var ties = ReadStrings(stage["tieBreakers"], sp + ".tieBreakers", 1, 6, errors,
                        (t, p, e) => Choice(t, p, e, "wins", "goal-difference", "goals-for", "red-cards", "yellow-cards", "seeded-draw"));
                    var ranking = qualification.Property("rankingStageIds") == null ? new List<string>() : ReadStrings(qualification["rankingStageIds"], sp + ".qualification.rankingStageIds", 0, 16, errors, Id);
                    CompetitionStageSourceData source = null;
                    if (stage.Property("source") != null)
                    {
                        var sourceObject = Object(stage["source"], sp + ".source", errors, "stageId", "selection");
                        if (sourceObject != null) source = new CompetitionStageSourceData(Id(sourceObject["stageId"], sp + ".source.stageId", errors),
                            Choice(sourceObject["selection"], sp + ".source.selection", errors, "qualified", "winners", "losers"));
                    }
                    stages.Add(new CompetitionStageData(Id(stage["id"], sp + ".id", errors), Name(stage["name"], sp + ".name", errors),
                        Choice(stage["kind"], sp + ".kind", errors, "league", "knockout"), Integer(stage["groupCount"], sp + ".groupCount", 1, 32, errors),
                        Choice(stage["opponents"], sp + ".opponents", errors, "all", "same-group", "cross-group", "authored"), Integer(stage["legs"], sp + ".legs", 1, 2, errors),
                        Integer(stage["roundCount"], sp + ".roundCount", 1, 128, errors), Integer(points["win"], sp + ".points.win", 0, 100, errors),
                        Integer(points["draw"], sp + ".points.draw", 0, 100, errors), Integer(points["loss"], sp + ".points.loss", 0, 100, errors), ties,
                        Choice(qualification["mode"], sp + ".qualification.mode", errors, "overall", "per-group", "winners"), Integer(qualification["count"], sp + ".qualification.count", 1, 64, errors),
                        Choice(stage["pairing"], sp + ".pairing", errors, "seeded", "draw", "cross-group"), Choice(stage["venue"], sp + ".venue", errors, "seeded", "first-listed", "draw", "neutral"),
                        Boolean(stage["awayGoals"], sp + ".awayGoals", errors), Choice(stage["tiedWinner"], sp + ".tiedWinner", errors, "penalties", "higher-seed"), ranking, source));
                });
                var outcomes = new List<CompetitionOutcomeData>();
                ReadBoundedItems(obj["outcomes"], path + ".outcomes", 0, 128, errors, (outcomeToken, op) =>
                {
                    var outcome = Object(outcomeToken, op, errors, "id", "label", "stageId", "kind", "ranking", "fromRank", "toRank"); if (outcome == null) return;
                    outcomes.Add(new CompetitionOutcomeData(Id(outcome["id"], op + ".id", errors), Name(outcome["label"], op + ".label", errors), Id(outcome["stageId"], op + ".stageId", errors),
                        Choice(outcome["kind"], op + ".kind", errors, "qualification", "promotion", "relegation"), Choice(outcome["ranking"], op + ".ranking", errors, "overall", "per-group"),
                        Integer(outcome["fromRank"], op + ".fromRank", 1, 64, errors), Integer(outcome["toRank"], op + ".toRank", 1, 64, errors)));
                });
                formats.Add(new CompetitionFormatData(Id(obj["id"], path + ".id", errors), Name(obj["name"], path + ".name", errors), version,
                    Integer(obj["participantCount"], path + ".participantCount", 2, 64, errors), substitutions, stages, outcomes,
                    obj.Property("championStageId") == null || obj["championStageId"].Type == JTokenType.Null ? null : Id(obj["championStageId"], path + ".championStageId", errors),
                    obj.Property("championStageId") == null || obj["championStageId"].Type != JTokenType.Null));
            });
        }
        private static CompetitionEditionData ReadDeclarativeEdition(JToken item, string path, List<DatabaseImportError> errors)
        {
            var obj = Object(item, path, errors, "id", "competitionId", "name", "formatId", "participantClubIds", "stageSchedules"); if (obj == null) return null;
            var participants = ReadStrings(obj["participantClubIds"], path + ".participantClubIds", 2, 64, errors, Id);
            var schedules = new List<CompetitionStageScheduleData>();
            ReadBoundedItems(obj["stageSchedules"], path + ".stageSchedules", 1, 16, errors, (scheduleToken, sp) =>
            {
                var schedule = ObjectWithOptionalFields(scheduleToken, sp, errors, new[] {"stageId", "roundDates", "groups", "authoredFixtures"}, new[] {"neutralStadiumId", "fixtureDates"}); if (schedule == null) return;
                var dates = ReadStrings(schedule["roundDates"], sp + ".roundDates", 1, 128, errors, CalendarDate);
                var groups = new List<CompetitionGroupData>();
                ReadBoundedItems(schedule["groups"], sp + ".groups", 0, 32, errors, (groupToken, gp) =>
                {
                    var group = ObjectWithOptionalFields(groupToken, gp, errors, new[] {"id", "name"}, new[] {"clubIds", "seedRanks"}); if (group == null) return;
                    if ((group.Property("clubIds") != null) == (group.Property("seedRanks") != null)) Error(errors, "invalid_group", gp, "Specify exactly one of clubIds and seedRanks.");
                    var ids = group.Property("clubIds") == null ? new List<string>() : ReadStrings(group["clubIds"], gp + ".clubIds", 1, 64, errors, Id);
                    var ranks = new List<int>();
                    if (group.Property("seedRanks") != null) ReadBoundedItems(group["seedRanks"], gp + ".seedRanks", 1, 64, errors, (rank, rp) => ranks.Add(Integer(rank, rp, 1, 64, errors)));
                    groups.Add(new CompetitionGroupData(Id(group["id"], gp + ".id", errors), Name(group["name"], gp + ".name", errors), ids, ranks));
                });
                var fixtures = new List<AuthoredFixtureData>();
                ReadBoundedItems(schedule["authoredFixtures"], sp + ".authoredFixtures", 0, 4096, errors, (fixtureToken, fp) =>
                {
                    var fixture = ObjectWithOptionalFields(fixtureToken, fp, errors, new[] {"id", "round", "date", "homeClubId", "awayClubId"}, new[] {"stadiumId"}); if (fixture == null) return;
                    fixtures.Add(new AuthoredFixtureData(Id(fixture["id"], fp + ".id", errors), Integer(fixture["round"], fp + ".round", 1, 128, errors), CalendarDate(fixture["date"], fp + ".date", errors),
                        Id(fixture["homeClubId"], fp + ".homeClubId", errors), Id(fixture["awayClubId"], fp + ".awayClubId", errors), fixture.Property("stadiumId") == null ? null : Id(fixture["stadiumId"], fp + ".stadiumId", errors)));
                });
                var fixtureDates = schedule.Property("fixtureDates") == null ? new List<string>() : ReadStrings(schedule["fixtureDates"], sp + ".fixtureDates", 1, 64, errors, CalendarDate);
                schedules.Add(new CompetitionStageScheduleData(Id(schedule["stageId"], sp + ".stageId", errors), dates, groups, fixtures,
                    schedule.Property("neutralStadiumId") == null ? null : Id(schedule["neutralStadiumId"], sp + ".neutralStadiumId", errors), fixtureDates));
            });
            return new CompetitionEditionData(Id(obj["id"], path + ".id", errors), Id(obj["competitionId"], path + ".competitionId", errors), Name(obj["name"], path + ".name", errors),
                participants, Id(obj["formatId"], path + ".formatId", errors), schedules);
        }
        private static void ValidateDeclarativeReferences(DatabaseDocument document, List<DatabaseImportError> errors)
        {
            var formats = new Dictionary<string, CompetitionFormatData>(StringComparer.Ordinal);
            for (var i = 0; i < document.CompetitionFormats.Count; i++)
            {
                var format = document.CompetitionFormats[i];
                if (formats.ContainsKey(format.Id)) Error(errors, "duplicate_id", "$.competitionFormats[" + i + "].id", "Format ID is repeated."); else formats.Add(format.Id, format);
            }
            var countries = new HashSet<string>(document.Countries.Select(c => c.Code), StringComparer.Ordinal);
            var clubs = new HashSet<string>(document.Clubs.Select(c => c.Id), StringComparer.Ordinal);
            var competitions = new HashSet<string>(document.Competitions.Select(c => c.Id), StringComparer.Ordinal);
            var stadiums = new HashSet<string>(document.Stadiums.Select(s => s.Id), StringComparer.Ordinal);
            for (var i = 0; i < document.Competitions.Count; i++)
            {
                var competition = document.Competitions[i]; var cp = "$.competitions[" + i + "]";
                if (competition.DefaultFormatId != null && !formats.ContainsKey(competition.DefaultFormatId)) Error(errors, "unknown_reference", cp + ".defaultFormatId", "Format does not exist.");
                var eligibility = competition.Eligibility;
                if (eligibility != null)
                {
                    if (eligibility.StateCodes.Count > 0 && eligibility.CountryCodes.Count != 1) Error(errors, "invalid_eligibility", cp + ".eligibility.countryCodes", "State filtering requires exactly one country.");
                    foreach (var code in eligibility.CountryCodes) if (!countries.Contains(code)) Error(errors, "unknown_reference", cp + ".eligibility.countryCodes", "Country does not exist.");
                    foreach (var club in eligibility.AllowedClubIds.Concat(eligibility.ExcludedClubIds)) if (!clubs.Contains(club)) Error(errors, "unknown_reference", cp + ".eligibility", "Club does not exist.");
                }
                var assignedIds = document.CompetitionEditions.Where(e => e.CompetitionId == competition.Id && e.FormatId != null).Select(e => e.FormatId).ToList();
                if (competition.DefaultFormatId != null) assignedIds.Add(competition.DefaultFormatId);
                var assigned = assignedIds.Distinct().Where(formats.ContainsKey).Select(id => formats[id]).ToList();
                for (var r = 0; r < competition.QualificationRoutes.Count; r++)
                {
                    var route = competition.QualificationRoutes[r]; var rp = cp + ".qualificationRoutes[" + r + "]";
                    if (!competitions.Contains(route.TargetCompetitionId) || route.TargetCompetitionId == competition.Id) Error(errors, "unknown_reference", rp + ".targetCompetitionId", "Choose another existing competition.");
                    if (assigned.Count == 0 || assigned.Any(f => !f.Outcomes.Any(o => o.Id == route.OutcomeId))) Error(errors, "unknown_reference", rp + ".outcomeId", "Outcome must exist in every assigned format.");
                }
                if (competition.Prizes != null) foreach (var award in competition.Prizes.RankingAwards)
                    if (assigned.Count == 0 || assigned.Any(f => !f.Stages.Any(s => s.Id == award.StageId))) Error(errors, "unknown_reference", cp + ".prizes.rankingAwards", "Award stage must exist in every assigned format.");
            }
            for (var i = 0; i < document.CompetitionEditions.Count; i++)
            {
                var edition = document.CompetitionEditions[i]; if (edition.FormatId == null) continue;
                var ep = "$.competitionEditions[" + i + "]";
                if (!formats.ContainsKey(edition.FormatId)) Error(errors, "unknown_reference", ep + ".formatId", "Format does not exist.");
                else if (edition.StageSchedules.Count != formats[edition.FormatId].Stages.Count ||
                    edition.StageSchedules.Where((schedule, index) => index >= formats[edition.FormatId].Stages.Count || schedule.StageId != formats[edition.FormatId].Stages[index].Id).Any())
                    Error(errors, "invalid_stage_order", ep + ".stageSchedules", "Supply one calendar per stage in format order.");
                var competition = document.Competitions.FirstOrDefault(c => c.Id == edition.CompetitionId);
                for (var p = 0; p < edition.ParticipantClubIds.Count; p++)
                {
                    var club = document.Clubs.FirstOrDefault(c => c.Id == edition.ParticipantClubIds[p]); var eligibility = competition?.Eligibility;
                    if (club != null && eligibility != null &&
                        ((eligibility.CountryCodes.Count > 0 && !eligibility.CountryCodes.Contains(club.CountryCode)) ||
                         (eligibility.StateCodes.Count > 0 && !eligibility.StateCodes.Contains(club.StateCode)) ||
                         (eligibility.AllowedClubIds.Count > 0 && !eligibility.AllowedClubIds.Contains(club.Id)) || eligibility.ExcludedClubIds.Contains(club.Id)))
                        Error(errors, "ineligible_club", ep + ".participantClubIds[" + p + "]", "Club does not satisfy competition eligibility.");
                }
                for (var s = 0; s < edition.StageSchedules.Count; s++)
                {
                    var schedule = edition.StageSchedules[s]; var sp = ep + ".stageSchedules[" + s + "]";
                    if (schedule.NeutralStadiumId != null && !stadiums.Contains(schedule.NeutralStadiumId)) Error(errors, "unknown_reference", sp + ".neutralStadiumId", "Stadium does not exist.");
                    for (var f = 0; f < schedule.AuthoredFixtures.Count; f++)
                        if (schedule.AuthoredFixtures[f].StadiumId != null && !stadiums.Contains(schedule.AuthoredFixtures[f].StadiumId)) Error(errors, "unknown_reference", sp + ".authoredFixtures[" + f + "].stadiumId", "Stadium does not exist.");
                }
            }
        }
        private static CompetitionFormatDefinition MapFormat(CompetitionFormatData format)
        {
            var stages = format.Stages.Select(s => new CompetitionStageDefinition(s.Id, s.Name, s.Kind, s.GroupCount, s.Opponents, s.Legs, s.RoundCount,
                new CompetitionPointsDefinition(s.WinPoints, s.DrawPoints, s.LossPoints), s.TieBreakers,
                new StageQualificationDefinition(s.QualificationMode, s.QualificationCount, s.RankingStageIds), s.Pairing, s.Venue, s.AwayGoals, s.TiedWinner,
                s.Source == null ? null : new StageSourceDefinition(s.Source.StageId, s.Source.Selection)));
            return new CompetitionFormatDefinition(format.Id, format.Name, format.Version, format.ParticipantCount, stages,
                new CompetitionMatchRules(format.MaxSubstitutions), format.Outcomes.Select(o => new CompetitionOutcomeDefinition(o.Id, o.Label, o.StageId, o.Kind, o.Ranking, o.FromRank, o.ToRank)),
                format.ChampionStageId, format.HasChampionStage);
        }
        private static CompetitionDefinition MapCompetition(CompetitionData item)
        {
            var e = item.Eligibility; var p = item.Prizes;
            return new CompetitionDefinition(item.Id, item.Name, item.DefaultFormatId, item.Level, item.Reputation ?? 0, item.PrizeLevel ?? 0,
                item.LogoUri, item.TrophyImageUri, item.TrophyModelUri,
                e == null ? null : new CompetitionEligibilityDefinition(e.CountryCodes, e.StateCodes, e.AllowedClubIds, e.ExcludedClubIds),
                item.QualificationRoutes.Select(r => new CompetitionQualificationRoute(r.OutcomeId, r.TargetCompetitionId)),
                p == null ? null : new CompetitionPrizeDefinition(p.Currency, p.Participation, p.Win, p.Draw,
                    p.RankingAwards.Select(a => new CompetitionRankingAward(a.StageId, a.Ranking, a.FromRank, a.ToRank, a.Amount))));
        }
        private static IEnumerable<CompetitionStageSchedule> MapStageSchedules(IEnumerable<CompetitionStageScheduleData> schedules)
        {
            foreach (var s in schedules) yield return new CompetitionStageSchedule(s.StageId, MapDates(s.RoundDates),
                s.Groups.Select(g => new CompetitionGroupDefinition(g.Id, g.Name, g.ClubIds, g.SeedRanks)),
                MapAuthoredFixtures(s.AuthoredFixtures), s.NeutralStadiumId, MapDates(s.FixtureDates));
        }
    }
}
