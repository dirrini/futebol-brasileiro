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

        private static void ReadCompetitions(JObject root, List<CompetitionData> competitions,
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
                var obj = Object(item, path, errors, "id", "competitionId", "name", "participantClubIds", "roundDates", "rules");
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
                var rules = ReadRules(obj["rules"], path + ".rules", errors);
                editions.Add(new CompetitionEditionData(Id(obj["id"], path + ".id", errors),
                    Id(obj["competitionId"], path + ".competitionId", errors), Name(obj["name"], path + ".name", errors),
                    participants, dates, rules));
            });
        }

        private static LeagueRulesData ReadRules(JToken token, string path, List<DatabaseImportError> errors)
        {
            var obj = Object(token, path, errors, "type", "version", "legs", "points", "tieBreakers");
            if (obj == null) return null;
            var type = String(obj["type"], path + ".type", errors);
            if (type != null && type != "round-robin")
                Error(errors, "unsupported_rule_type", path + ".type", "Only round-robin rules are supported.");
            var version = Integer(obj["version"], path + ".version", 1, int.MaxValue, errors);
            if (version != 1) Error(errors, "unsupported_rule_version", path + ".version", "Only round-robin version 1 is supported.");
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
            ReadBoundedItems(obj["tieBreakers"], path + ".tieBreakers", 3, 3, errors,
                (value, valuePath) => tieBreakers.Add(String(value, valuePath, errors)));
            for (var i = 0; i < tieBreakers.Count && i < SupportedTieBreakers.Length; i++)
                if (tieBreakers[i] != SupportedTieBreakers[i])
                    Error(errors, "unsupported_tiebreaker", path + ".tieBreakers[" + i + "]",
                        "Supported order is wins, goal-difference, goals-for.");
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
                var expectedRounds = (count % 2 == 0 ? count - 1 : count) * edition.Rules.Legs;
                if (edition.RoundDates.Count != expectedRounds)
                    Error(errors, "invalid_round_count", path + ".roundDates", "Supply exactly " + expectedRounds + " round dates.");
                for (var j = 1; j < edition.RoundDates.Count; j++)
                    if (string.CompareOrdinal(edition.RoundDates[j - 1], edition.RoundDates[j]) >= 0)
                        Error(errors, "invalid_date_order", path + ".roundDates[" + j + "]", "Round dates must be strictly increasing.");
            }
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
