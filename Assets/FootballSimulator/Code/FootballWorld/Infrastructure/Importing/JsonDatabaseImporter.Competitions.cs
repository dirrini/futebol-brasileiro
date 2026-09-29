using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using FStudio.FootballWorld.DataContracts;
using FStudio.FootballWorld.Domain;
using Newtonsoft.Json.Linq;

namespace FStudio.FootballWorld.Infrastructure.Importing
{
    public sealed partial class JsonDatabaseImporter
    {
        private static readonly string[] SupportedTieBreakers = {"wins", "goal-difference", "goals-for"};
        private static readonly string[] PaulistaTieBreakers = {"wins", "goal-difference", "goals-for", "red-cards", "yellow-cards", "drawing-lots"};

        private static void ReadCompetitions(JObject root, int schemaVersion, List<CompetitionData> competitions,
            List<CompetitionEditionData> editions, List<DatabaseImportError> errors)
        {
            ReadBoundedItems(root["competitions"], "$.competitions", 0, 128, errors, (item, path) =>
            {
                var obj = Object(item, path, errors, "id", "name");
                if (obj != null) competitions.Add(new CompetitionData(Id(obj["id"], path + ".id", errors),
                    Name(obj["name"], path + ".name", errors)));
            });
            ReadBoundedItems(root["competitionEditions"], "$.competitionEditions", 0, 128, errors, (item, path) =>
            {
                var obj = ObjectWithOptionalFields(item, path, errors,
                    new[] {"id", "competitionId", "name", "participantClubIds", "roundDates", "rules"},
                    schemaVersion >= 5 ? new[] {"authoredFixtures", "playoffDates"} : Array.Empty<string>());
                if (obj == null) return;
                var participants = new List<string>();
                ReadBoundedItems(obj["participantClubIds"], path + ".participantClubIds", 2, 64, errors,
                    (value, valuePath) => participants.Add(Id(value, valuePath, errors)));
                var dates = new List<string>();
                ReadBoundedItems(obj["roundDates"], path + ".roundDates", 1, 128, errors, (value, valuePath) =>
                {
                    var text = String(value, valuePath, errors);
                    if (text != null && (!Regex.IsMatch(text, @"\A[0-9]{4}-[0-9]{2}-[0-9]{2}\z") ||
                        !DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
                        Error(errors, "invalid_date", valuePath, "Use a real calendar date in yyyy-MM-dd format (years 0001-9999).");
                    dates.Add(text);
                });
                var rules = ReadRules(obj["rules"], path + ".rules", schemaVersion, errors);
                var fixtures = new List<AuthoredFixtureData>();
                var playoffs = new List<string>();
                if (rules?.Type == "paulista-2026")
                {
                    foreach (var field in new[] {"authoredFixtures", "playoffDates"})
                        if (obj.Property(field) == null) Error(errors, "required", path + "." + field, "Paulista 2026 requires this calendar field.");
                    ReadBoundedItems(obj["authoredFixtures"], path + ".authoredFixtures", 64, 64, errors, (value, valuePath) =>
                    {
                        var fixture = ObjectWithOptionalFields(value, valuePath, errors,
                            new[] {"id", "round", "date", "homeClubId", "awayClubId"}, new[] {"stadiumId"});
                        if (fixture == null) return;
                        fixtures.Add(new AuthoredFixtureData(Id(fixture["id"], valuePath + ".id", errors),
                            Integer(fixture["round"], valuePath + ".round", 1, 8, errors), CalendarDate(fixture["date"], valuePath + ".date", errors),
                            Id(fixture["homeClubId"], valuePath + ".homeClubId", errors), Id(fixture["awayClubId"], valuePath + ".awayClubId", errors),
                            fixture.Property("stadiumId") == null ? null : Id(fixture["stadiumId"], valuePath + ".stadiumId", errors)));
                    });
                    ReadBoundedItems(obj["playoffDates"], path + ".playoffDates", 8, 8, errors,
                        (value, valuePath) => playoffs.Add(CalendarDate(value, valuePath, errors)));
                }
                else
                    foreach (var field in new[] {"authoredFixtures", "playoffDates"})
                        if (obj.Property(field) != null) Error(errors, "unsupported_rule_field", path + "." + field, "This field requires paulista-2026 rules.");
                editions.Add(new CompetitionEditionData(Id(obj["id"], path + ".id", errors),
                    Id(obj["competitionId"], path + ".competitionId", errors), Name(obj["name"], path + ".name", errors),
                    participants, dates, rules, fixtures, playoffs));
            });
        }

        private static LeagueRulesData ReadRules(JToken token, string path, int schemaVersion, List<DatabaseImportError> errors)
        {
            var obj = Object(token, path, errors, "type", "version", "legs", "points", "tieBreakers");
            if (obj == null) return null;
            var type = String(obj["type"], path + ".type", errors);
            var paulista = type == "paulista-2026" && schemaVersion >= 5;
            if (type != null && type != "round-robin" && !paulista)
                Error(errors, "unsupported_rule_type", path + ".type", "Supported rules are round-robin v1 and, from database v5, paulista-2026 v1.");
            var version = Integer(obj["version"], path + ".version", 1, int.MaxValue, errors);
            if (version != 1) Error(errors, "unsupported_rule_version", path + ".version", "Only rule version 1 is supported.");
            var legs = Integer(obj["legs"], path + ".legs", 1, 2, errors);
            var points = Object(obj["points"], path + ".points", errors, "win", "draw", "loss");
            var win = 0; var draw = 0; var loss = 0;
            if (points != null)
            {
                win = Integer(points["win"], path + ".points.win", 0, 100, errors);
                draw = Integer(points["draw"], path + ".points.draw", 0, 100, errors);
                loss = Integer(points["loss"], path + ".points.loss", 0, 100, errors);
                if (win <= draw || draw < loss)
                    Error(errors, "invalid_points", path + ".points", "Points must satisfy win > draw >= loss.");
            }
            var tieBreakers = new List<string>();
            var supported = paulista ? PaulistaTieBreakers : SupportedTieBreakers;
            ReadBoundedItems(obj["tieBreakers"], path + ".tieBreakers", supported.Length, supported.Length, errors,
                (value, valuePath) => tieBreakers.Add(String(value, valuePath, errors)));
            for (var i = 0; i < tieBreakers.Count && i < supported.Length; i++)
                if (tieBreakers[i] != supported[i])
                    Error(errors, "unsupported_tiebreaker", path + ".tieBreakers[" + i + "]",
                        "Supported order is " + string.Join(", ", supported) + ".");
            if (paulista && (legs != 1 || win != 3 || draw != 1 || loss != 0))
                Error(errors, "unsupported_rule_parameters", path, "Paulista 2026 requires legs 1 and points 3/1/0.");
            return new LeagueRulesData(type, version, legs, win, draw, loss, tieBreakers);
        }

        private static void ValidateCompetitionReferences(DatabaseDocument document, HashSet<string> clubs,
            List<DatabaseImportError> errors)
        {
            var competitionIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < document.Competitions.Count; i++)
                if (!competitionIds.Add(document.Competitions[i].Id))
                    Error(errors, "duplicate_id", "$.competitions[" + i + "].id", "Competition ID is repeated.");
            var editionIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < document.CompetitionEditions.Count; i++)
            {
                var edition = document.CompetitionEditions[i];
                var path = "$.competitionEditions[" + i + "]";
                if (!editionIds.Add(edition.Id)) Error(errors, "duplicate_id", path + ".id", "Edition ID is repeated.");
                if (!competitionIds.Contains(edition.CompetitionId))
                    Error(errors, "unknown_reference", path + ".competitionId", "Competition does not exist.");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var j = 0; j < edition.ParticipantClubIds.Count; j++)
                {
                    var clubId = edition.ParticipantClubIds[j];
                    var participantPath = path + ".participantClubIds[" + j + "]";
                    if (!clubs.Contains(clubId)) Error(errors, "unknown_reference", participantPath, "Club does not exist.");
                    if (!seen.Add(clubId)) Error(errors, "duplicate_participant", participantPath, "Edition participants must be unique.");
                }
                var count = edition.ParticipantClubIds.Count;
                var expectedRounds = edition.Rules.Type == "paulista-2026" ? 8 : (count % 2 == 0 ? count - 1 : count) * edition.Rules.Legs;
                if (edition.RoundDates.Count != expectedRounds)
                    Error(errors, "invalid_round_count", path + ".roundDates", "Supply exactly " + expectedRounds + " round dates.");
                for (var j = 1; j < edition.RoundDates.Count; j++)
                    if (string.CompareOrdinal(edition.RoundDates[j - 1], edition.RoundDates[j]) >= 0)
                        Error(errors, "invalid_date_order", path + ".roundDates[" + j + "]", "Round dates must be strictly increasing.");
                if (edition.Rules.Type == "paulista-2026") ValidateAuthoredFixtures(document, edition, path, seen, errors);
            }
        }

        private static IEnumerable<GameDate> MapDates(IEnumerable<string> dates)
        {
            foreach (var date in dates) yield return ParseGameDate(date);
        }

        private static IEnumerable<FixtureDefinition> MapAuthoredFixtures(IEnumerable<AuthoredFixtureData> fixtures)
        {
            foreach (var fixture in fixtures) yield return new FixtureDefinition(fixture.Id, fixture.Round, ParseGameDate(fixture.Date),
                fixture.HomeClubId, fixture.AwayClubId, fixture.StadiumId);
        }

        private static void ValidateAuthoredFixtures(DatabaseDocument document, CompetitionEditionData edition, string path,
            HashSet<string> participants, List<DatabaseImportError> errors)
        {
            if (participants.Count != 16) Error(errors, "invalid_participant_count", path + ".participantClubIds", "Paulista 2026 requires 16 clubs.");
            for (var i = 0; i < edition.PlayoffDates.Count; i++)
            {
                var priorStart = i < 4 ? -1 : i < 6 ? 0 : i == 6 ? 4 : 6;
                var priorEnd = i < 4 ? -1 : i < 6 ? 4 : i == 6 ? 6 : 7;
                var invalid = i < 4 && edition.RoundDates.Count > 0 && string.CompareOrdinal(edition.PlayoffDates[i], edition.RoundDates[edition.RoundDates.Count - 1]) <= 0;
                for (var j = priorStart; j >= 0 && j < priorEnd; j++)
                    if (string.CompareOrdinal(edition.PlayoffDates[i], edition.PlayoffDates[j]) <= 0) invalid = true;
                if (invalid) Error(errors, "invalid_date_order", path + ".playoffDates[" + i + "]", "Each playoff fixture must follow every fixture of the previous phase.");
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var roundClubs = new HashSet<string>(StringComparer.Ordinal);
            var pairs = new HashSet<string>(StringComparer.Ordinal);
            var homes = new Dictionary<string, int>(StringComparer.Ordinal);
            var appearances = new Dictionary<string, int>(StringComparer.Ordinal);
            var stadiumIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stadium in document.Stadiums) stadiumIds.Add(stadium.Id);
            foreach (var club in participants) { homes[club] = 0; appearances[club] = 0; }
            for (var i = 0; i < edition.AuthoredFixtures.Count; i++)
            {
                var fixture = edition.AuthoredFixtures[i]; var fpath = path + ".authoredFixtures[" + i + "]";
                if (!ids.Add(fixture.Id)) Error(errors, "duplicate_id", fpath + ".id", "Fixture ID is repeated in this edition.");
                if (fixture.HomeClubId == fixture.AwayClubId) Error(errors, "invalid_fixture", fpath, "A fixture requires distinct clubs.");
                foreach (var club in new[] {fixture.HomeClubId, fixture.AwayClubId})
                {
                    if (!participants.Contains(club)) Error(errors, "unknown_reference", fpath, "Fixture club is not an edition participant.");
                    else appearances[club]++;
                    if (!roundClubs.Add(fixture.Round + ":" + club)) Error(errors, "duplicate_round_club", fpath, "A club cannot play twice in the same round.");
                }
                if (homes.ContainsKey(fixture.HomeClubId)) homes[fixture.HomeClubId]++;
                var pair = string.CompareOrdinal(fixture.HomeClubId, fixture.AwayClubId) < 0
                    ? fixture.HomeClubId + ":" + fixture.AwayClubId : fixture.AwayClubId + ":" + fixture.HomeClubId;
                if (!pairs.Add(pair)) Error(errors, "duplicate_opponents", fpath, "First-phase opponents must be distinct.");
                if (fixture.StadiumId != null && !stadiumIds.Contains(fixture.StadiumId)) Error(errors, "unknown_reference", fpath + ".stadiumId", "Stadium does not exist.");
                if (fixture.Round <= edition.RoundDates.Count && edition.PlayoffDates.Count != 0)
                {
                    var firstPlayoff = edition.PlayoffDates[0];
                    for (var j = 1; j < Math.Min(4, edition.PlayoffDates.Count); j++)
                        if (string.CompareOrdinal(edition.PlayoffDates[j], firstPlayoff) < 0) firstPlayoff = edition.PlayoffDates[j];
                    var next = fixture.Round < edition.RoundDates.Count ? edition.RoundDates[fixture.Round] : firstPlayoff;
                    if (string.CompareOrdinal(fixture.Date, edition.RoundDates[fixture.Round - 1]) < 0 || string.CompareOrdinal(fixture.Date, next) >= 0)
                        Error(errors, "invalid_fixture_date", fpath + ".date", "Fixture date must fit within its round's window.");
                }
            }
            foreach (var club in participants)
                if (appearances[club] != 8 || homes[club] != 4) Error(errors, "invalid_club_schedule", path + ".authoredFixtures", "Each club requires eight games, four at home and four away.");
        }

        private static GameDate ParseGameDate(string text)
        {
            var value = DateTime.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            return new GameDate(value.Year, value.Month, value.Day);
        }

        private static void ReadBoundedItems(JToken token, string path, int minimum, int maximum,
            List<DatabaseImportError> errors, Action<JToken, string> read)
        {
            if (token is JArray array && array.Count > maximum)
                Error(errors, "too_many_items", path, "At most " + maximum + " items are supported.");
            ReadItems(token, path, minimum, errors, read);
        }
    }
}
