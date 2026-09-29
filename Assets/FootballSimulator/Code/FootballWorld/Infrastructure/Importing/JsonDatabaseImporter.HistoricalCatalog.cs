using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using FStudio.FootballWorld.DataContracts;
using Newtonsoft.Json.Linq;

namespace FStudio.FootballWorld.Infrastructure.Importing
{
    public sealed partial class JsonDatabaseImporter
    {
        private static ClubData ReadClub(JToken token, string path, int version, List<DatabaseImportError> errors)
        {
            var obj = ObjectWithOptionalFields(token, path, errors,
                version >= 4 ? new[] {"id", "name", "countryCode", "city"} : new[] {"id", "name"},
                version >= 4 ? new[] {"officialName", "shortName", "stadiumId", "reputation", "supporterCount", "transferBudget",
                    "monthlyWageBudget", "currency", "sponsorship", "notes"} : Array.Empty<string>());
            if (obj == null) return null;
            var currency = OptionalCode(obj, "currency", path, 3, errors);
            if ((obj.Property("transferBudget") != null || obj.Property("monthlyWageBudget") != null) && obj.Property("currency") == null)
                Error(errors, "required", path + ".currency", "Budgets require a currency code.");
            return new ClubData(Id(obj["id"], path + ".id", errors), Name(obj["name"], path + ".name", errors),
                version >= 4 ? Code(obj["countryCode"], path + ".countryCode", 2, errors) : null,
                version >= 4 ? Name(obj["city"], path + ".city", errors) : null,
                OptionalText(obj, "officialName", path, 200, false, errors), OptionalText(obj, "shortName", path, 100, false, errors),
                obj.Property("stadiumId") == null ? null : Id(obj["stadiumId"], path + ".stadiumId", errors),
                OptionalInteger(obj, "reputation", path, 0, 100, errors), OptionalInteger(obj, "supporterCount", path, 0, int.MaxValue, errors),
                OptionalInteger(obj, "transferBudget", path, 0, int.MaxValue, errors), OptionalInteger(obj, "monthlyWageBudget", path, 0, int.MaxValue, errors),
                currency, OptionalText(obj, "sponsorship", path, 200, false, errors), OptionalText(obj, "notes", path, 4000, true, errors));
        }

        private static DatabaseSnapshotData ReadHistoricalCatalog(JObject root, List<CountryData> countries,
            List<StadiumData> stadiums, List<DatabaseImportError> errors)
        {
            ReadBoundedItems(root["countries"], "$.countries", 1, 300, errors, (item, path) =>
            {
                var obj = Object(item, path, errors, "code", "name");
                if (obj != null) countries.Add(new CountryData(Code(obj["code"], path + ".code", 2, errors), Name(obj["name"], path + ".name", errors)));
            });
            ReadBoundedItems(root["stadiums"], "$.stadiums", 0, 1024, errors, (item, path) =>
            {
                var obj = ObjectWithOptionalFields(item, path, errors, new[] {"id", "name", "countryCode", "city"}, new[] {"capacity"});
                if (obj != null) stadiums.Add(new StadiumData(Id(obj["id"], path + ".id", errors), Name(obj["name"], path + ".name", errors),
                    Code(obj["countryCode"], path + ".countryCode", 2, errors), Name(obj["city"], path + ".city", errors),
                    OptionalInteger(obj, "capacity", path, 1, 1000000, errors)));
            });
            var snapshot = Object(root["snapshot"], "$.snapshot", errors, "date", "label", "rosterScope", "notes", "sources");
            if (snapshot == null) return null;
            var date = CalendarDate(snapshot["date"], "$.snapshot.date", errors);
            var scope = String(snapshot["rosterScope"], "$.snapshot.rosterScope", errors);
            if (scope != null && scope != "matchday-squads" && scope != "full-squads")
                Error(errors, "unsupported_roster_scope", "$.snapshot.rosterScope", "Use matchday-squads or full-squads.");
            var sources = new List<DatabaseSourceData>();
            ReadBoundedItems(snapshot["sources"], "$.snapshot.sources", 1, 128, errors, (item, path) =>
            {
                var obj = Object(item, path, errors, "id", "title", "url");
                if (obj == null) return;
                var url = Text(obj["url"], path + ".url", 2048, false, errors);
                if (url != null && !Regex.IsMatch(url, @"\Ahttps?://[^\u0009-\u000D\u0020\u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF/?#]+(?:[/?#][^\u0009-\u000D\u0020\u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF]*)?\z", RegexOptions.CultureInvariant))
                    Error(errors, "invalid_url", path + ".url", "Use an absolute HTTP(S) source URL without whitespace.");
                sources.Add(new DatabaseSourceData(Id(obj["id"], path + ".id", errors), Text(obj["title"], path + ".title", 200, false, errors), url));
            });
            return new DatabaseSnapshotData(date, Name(snapshot["label"], "$.snapshot.label", errors), scope,
                Text(snapshot["notes"], "$.snapshot.notes", 4000, true, errors), sources);
        }

        private static void ValidateHistoricalReferences(DatabaseDocument document, List<DatabaseImportError> errors)
        {
            var countries = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < document.Countries.Count; i++)
                if (!countries.Add(document.Countries[i].Code)) Error(errors, "duplicate_id", "$.countries[" + i + "].code", "Country code is repeated.");
            var stadiums = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < document.Stadiums.Count; i++)
            {
                var stadium = document.Stadiums[i]; var path = "$.stadiums[" + i + "]";
                if (!stadiums.Add(stadium.Id)) Error(errors, "duplicate_id", path + ".id", "Stadium ID is repeated.");
                if (!countries.Contains(stadium.CountryCode)) Error(errors, "unknown_reference", path + ".countryCode", "Country does not exist.");
            }
            for (var i = 0; i < document.Clubs.Count; i++)
            {
                var club = document.Clubs[i]; var path = "$.clubs[" + i + "]";
                if (!countries.Contains(club.CountryCode)) Error(errors, "unknown_reference", path + ".countryCode", "Country does not exist.");
                if (club.StadiumId != null && !stadiums.Contains(club.StadiumId)) Error(errors, "unknown_reference", path + ".stadiumId", "Stadium does not exist.");
            }
            for (var i = 0; i < document.Players.Count; i++)
            {
                var player = document.Players[i]; var path = "$.players[" + i + "]";
                if (player.NationalityCode != null && !countries.Contains(player.NationalityCode)) Error(errors, "unknown_reference", path + ".nationalityCode", "Country does not exist.");
                if (player.BirthDate != null && string.CompareOrdinal(player.BirthDate, document.Snapshot.Date) > 0)
                    Error(errors, "birth_after_snapshot", path + ".birthDate", "Birth date cannot be after the observation date.");
            }
            var sources = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < document.Snapshot.Sources.Count; i++)
                if (!sources.Add(document.Snapshot.Sources[i].Id)) Error(errors, "duplicate_id", "$.snapshot.sources[" + i + "].id", "Source ID is repeated.");
        }

        private static string CalendarDate(JToken token, string path, List<DatabaseImportError> errors)
        {
            var value = String(token, path, errors);
            if (value != null && (!Regex.IsMatch(value, @"\A[0-9]{4}-[0-9]{2}-[0-9]{2}\z") ||
                !DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
                Error(errors, "invalid_date", path, "Use a real calendar date in yyyy-MM-dd format (years 0001-9999).");
            return value;
        }
        private static string Code(JToken token, string path, int length, List<DatabaseImportError> errors)
        {
            var value = String(token, path, errors);
            if (value != null && !Regex.IsMatch(value, "\\A[A-Z]{" + length + "}\\z"))
                Error(errors, "invalid_code", path, "Use exactly " + length + " uppercase ASCII letters.");
            return value;
        }
        private static string Text(JToken token, string path, int maximum, bool allowEmpty, List<DatabaseImportError> errors)
        {
            var value = String(token, path, errors);
            if (value != null && ((!allowEmpty && string.IsNullOrWhiteSpace(value)) || UnicodeLength(value) > maximum))
                Error(errors, "invalid_text", path, "Text must respect the " + maximum + " Unicode code point limit and required content.");
            return value;
        }
        private static string OptionalText(JObject obj, string field, string path, int maximum, bool allowEmpty, List<DatabaseImportError> errors)
            => obj.Property(field) == null ? null : Text(obj[field], path + "." + field, maximum, allowEmpty, errors);
        private static string OptionalCode(JObject obj, string field, string path, int length, List<DatabaseImportError> errors)
            => obj.Property(field) == null ? null : Code(obj[field], path + "." + field, length, errors);
        private static string OptionalDate(JObject obj, string field, string path, List<DatabaseImportError> errors)
            => obj.Property(field) == null ? null : CalendarDate(obj[field], path + "." + field, errors);
        private static int? OptionalInteger(JObject obj, string field, string path, int min, int max, List<DatabaseImportError> errors)
            => obj.Property(field) == null ? (int?)null : Integer(obj[field], path + "." + field, min, max, errors);
        private static string OptionalFoot(JObject obj, string path, List<DatabaseImportError> errors)
        {
            if (obj.Property("preferredFoot") == null) return null;
            var value = String(obj["preferredFoot"], path + ".preferredFoot", errors);
            if (value != null && value != "right" && value != "left" && value != "both")
                Error(errors, "unsupported_preferred_foot", path + ".preferredFoot", "Use right, left or both, or omit unknown data.");
            return value;
        }
    }
}
