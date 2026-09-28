using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FStudio.FootballWorld.DataContracts;
using FStudio.FootballWorld.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FStudio.FootballWorld.Infrastructure.Importing
{
    public sealed class JsonDatabaseImporter
    {
        public const int MaximumDocumentBytes = 1024 * 1024;
        public const int MaximumDepth = 32;
        private static readonly Regex IdPattern = new Regex(@"\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z",
            RegexOptions.CultureInvariant);
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly string[] AttributeNames = {
            "strength", "acceleration", "topSpeed", "dribbleSpeed", "jump", "tackling", "ballKeeping",
            "passing", "longBall", "agility", "shooting", "shootPower", "positioning", "reaction", "ballControl"
        };
        private static readonly Dictionary<string, PlayerPosition> Positions = new Dictionary<string, PlayerPosition>(StringComparer.Ordinal)
        {
            {"GK", PlayerPosition.GK}, {"RB", PlayerPosition.RB}, {"LB", PlayerPosition.LB},
            {"CB", PlayerPosition.CB}, {"DM", PlayerPosition.DM}, {"CM", PlayerPosition.CM},
            {"RM", PlayerPosition.RM}, {"LM", PlayerPosition.LM}, {"AM", PlayerPosition.AM},
            {"LW", PlayerPosition.LW}, {"RW", PlayerPosition.RW}, {"ST", PlayerPosition.ST}
        };

        // Imports an in-memory snapshot only. The caller activates it after Success;
        // this adapter neither reads files nor changes an existing catalog/session.
        public DatabaseImportResult Import(string json)
        {
            var errors = new List<DatabaseImportError>();
            if (string.IsNullOrWhiteSpace(json)) return Failure("invalid_json", "$", "A JSON document is required.");
            try
            {
                if (json.Length > MaximumDocumentBytes || Utf8.GetByteCount(json) > MaximumDocumentBytes)
                    return Failure("document_too_large", "$", "The UTF-8 document exceeds 1 MiB.");
                StrictJsonSyntax.Validate(json, MaximumDepth);
            }
            catch (FormatException exception) { return Failure("invalid_json", "$", exception.Message); }
            catch (EncoderFallbackException) { return Failure("invalid_json", "$", "The document contains invalid Unicode."); }

            JToken token;
            try
            {
                using (var input = new StringReader(json))
                using (var reader = new JsonTextReader(input)
                {
                    MaxDepth = MaximumDepth,
                    DateParseHandling = DateParseHandling.None,
                    FloatParseHandling = FloatParseHandling.Decimal
                })
                {
                    token = JToken.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        LineInfoHandling = LineInfoHandling.Load
                    });
                }
            }
            catch (JsonException exception) { return Failure("invalid_json", "$", exception.Message); }

            var document = ReadDocument(token, errors);
            if (errors.Count != 0) return DatabaseImportResult.Rejected(errors);
            ValidateReferences(document, errors);
            if (errors.Count != 0) return DatabaseImportResult.Rejected(errors);

            try
            {
                return DatabaseImportResult.Accepted(Map(document), document.VisualProfiles);
            }
            catch (ArgumentException exception)
            {
                return Failure("invalid_catalog", "$", exception.Message);
            }
        }

        private static DatabaseDocument ReadDocument(JToken token, List<DatabaseImportError> errors)
        {
            var root = Object(token, "$", errors, "schemaVersion", "databaseId", "databaseRevision",
                "clubs", "players", "memberships", "visualProfiles");
            if (root == null) return null;
            var schemaVersion = Integer(root["schemaVersion"], "$.schemaVersion", int.MinValue, int.MaxValue, errors);
            if (root["schemaVersion"] != null && root["schemaVersion"].Type == JTokenType.Integer &&
                schemaVersion != 1 && schemaVersion != 2)
                Error(errors, "unsupported_schema_version", "$.schemaVersion", "Only schemaVersion 1 and 2 are supported.");
            var databaseId = Id(root["databaseId"], "$.databaseId", errors);
            var revision = Integer(root["databaseRevision"], "$.databaseRevision", 1, int.MaxValue, errors);
            var clubs = new List<ClubData>();
            var players = new List<PlayerData>();
            var memberships = new List<MembershipData>();
            var visuals = new List<VisualProfileData>();

            ReadItems(root["clubs"], "$.clubs", 1, errors, (item, path) =>
            {
                var obj = Object(item, path, errors, "id", "name");
                if (obj != null) clubs.Add(new ClubData(Id(obj["id"], path + ".id", errors), Name(obj["name"], path + ".name", errors)));
            });
            ReadItems(root["players"], "$.players", 1, errors, (item, path) =>
            {
                var obj = Object(item, path, errors, "id", "name", "naturalPositions", "heightCm", "weightKg", "attributes");
                if (obj == null) return;
                var id = Id(obj["id"], path + ".id", errors);
                var name = Name(obj["name"], path + ".name", errors);
                var positions = new List<string>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                ReadItems(obj["naturalPositions"], path + ".naturalPositions", 1, errors, (position, positionPath) =>
                {
                    var value = String(position, positionPath, errors);
                    if (value == null) return;
                    if (!Positions.ContainsKey(value)) Error(errors, "invalid_position", positionPath, "Unknown natural position.");
                    else if (!seen.Add(value)) Error(errors, "duplicate_position", positionPath, "Natural positions must be unique.");
                    else positions.Add(value);
                });
                var height = Integer(obj["heightCm"], path + ".heightCm", 150, 210, errors);
                var weight = Integer(obj["weightKg"], path + ".weightKg", 45, 100, errors);
                var attributes = Object(obj["attributes"], path + ".attributes", errors, AttributeNames);
                var values = new int[AttributeNames.Length];
                if (attributes != null)
                    for (var i = 0; i < values.Length; i++)
                        values[i] = Integer(attributes[AttributeNames[i]], path + ".attributes." + AttributeNames[i], 0, 100, errors);
                players.Add(new PlayerData(id, name, positions, height, weight, new PlayerAttributesData(
                    values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7],
                    values[8], values[9], values[10], values[11], values[12], values[13], values[14])));
            });
            ReadItems(root["memberships"], "$.memberships", 0, errors, (item, path) =>
            {
                var obj = Object(item, path, errors, "clubId", "playerId");
                if (obj != null) memberships.Add(new MembershipData(Id(obj["clubId"], path + ".clubId", errors),
                    Id(obj["playerId"], path + ".playerId", errors)));
            });
            ReadItems(root["visualProfiles"], "$.visualProfiles", 0, errors, (item, path) =>
            {
                var required = new[] {"playerId", "skin"};
                var obj = ObjectWithOptionalFields(item, path, errors, required,
                    schemaVersion == 2 ? new[] {"appearance"} : Array.Empty<string>());
                if (obj == null) return;
                var playerId = Id(obj["playerId"], path + ".playerId", errors);
                var skin = Object(obj["skin"], path + ".skin", errors, "skinId", "revision", "compatibilityProfile");
                var appearance = schemaVersion == 2 && obj.Property("appearance", StringComparison.Ordinal) != null
                    ? ReadAppearance(obj["appearance"], path + ".appearance", errors)
                    : null;
                if (skin == null) return;
                var skinReference = new SkinReferenceData(
                    Id(skin["skinId"], path + ".skin.skinId", errors),
                    Integer(skin["revision"], path + ".skin.revision", 1, int.MaxValue, errors),
                    Id(skin["compatibilityProfile"], path + ".skin.compatibilityProfile", errors));
                if (appearance != null &&
                    (skinReference.SkinId != "builtin-player" || skinReference.Revision != 1 ||
                     skinReference.CompatibilityProfile != "football-player-v1"))
                    Error(errors, "unsupported_appearance_skin", path + ".appearance",
                        "Built-in appearance requires builtin-player revision 1 with compatibilityProfile football-player-v1.");
                visuals.Add(new VisualProfileData(playerId, skinReference, appearance));
            });
            return new DatabaseDocument(schemaVersion, databaseId, revision, clubs, players, memberships, visuals);
        }

        private static void ValidateReferences(DatabaseDocument document, List<DatabaseImportError> errors)
        {
            var clubs = new HashSet<string>(StringComparer.Ordinal);
            var players = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < document.Clubs.Count; i++)
                if (!clubs.Add(document.Clubs[i].Id)) Error(errors, "duplicate_id", "$.clubs[" + i + "].id", "Club ID is repeated.");
            for (var i = 0; i < document.Players.Count; i++)
                if (!players.Add(document.Players[i].Id)) Error(errors, "duplicate_id", "$.players[" + i + "].id", "Player ID is repeated.");
            var rosterPlayers = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < document.Memberships.Count; i++)
            {
                var item = document.Memberships[i];
                var path = "$.memberships[" + i + "]";
                if (!clubs.Contains(item.ClubId)) Error(errors, "unknown_reference", path + ".clubId", "Club does not exist.");
                if (!players.Contains(item.PlayerId)) Error(errors, "unknown_reference", path + ".playerId", "Player does not exist.");
                if (!rosterPlayers.Add(item.PlayerId)) Error(errors, "duplicate_membership", path + ".playerId", "A player can have only one initial club membership.");
            }
            var visualPlayers = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < document.VisualProfiles.Count; i++)
            {
                var item = document.VisualProfiles[i];
                var path = "$.visualProfiles[" + i + "].playerId";
                if (!players.Contains(item.PlayerId)) Error(errors, "unknown_reference", path, "Player does not exist.");
                if (!visualPlayers.Add(item.PlayerId)) Error(errors, "duplicate_visual_profile", path, "A player can have only one visual profile.");
            }
        }

        private static DatabaseCatalog Map(DatabaseDocument document)
        {
            var clubs = new List<ClubDefinition>();
            foreach (var item in document.Clubs) clubs.Add(new ClubDefinition(item.Id, item.Name));
            var players = new List<PlayerDefinition>();
            foreach (var item in document.Players)
            {
                var positions = new List<PlayerPosition>();
                foreach (var position in item.NaturalPositions) positions.Add(Positions[position]);
                var a = item.Attributes;
                players.Add(new PlayerDefinition(item.Id, item.Name, positions, item.HeightCm, item.WeightKg,
                    new PlayerAttributes(a.Strength, a.Acceleration, a.TopSpeed, a.DribbleSpeed, a.Jump,
                        a.Tackling, a.BallKeeping, a.Passing, a.LongBall, a.Agility, a.Shooting,
                        a.ShootPower, a.Positioning, a.Reaction, a.BallControl)));
            }
            var memberships = new List<RosterMembership>();
            foreach (var item in document.Memberships) memberships.Add(new RosterMembership(item.ClubId, item.PlayerId));
            return new DatabaseCatalog(document.DatabaseId, document.DatabaseRevision, clubs, players, memberships);
        }

        private static JObject Object(JToken token, string path, List<DatabaseImportError> errors, params string[] fields)
            => ObjectWithOptionalFields(token, path, errors, fields, Array.Empty<string>());

        private static JObject ObjectWithOptionalFields(JToken token, string path, List<DatabaseImportError> errors,
            string[] required, string[] optional)
        {
            if (token == null) return null; // Missing fields are reported by their parent object.
            if (!(token is JObject result))
            {
                Error(errors, "invalid_type", path, "Expected an object.");
                return null;
            }
            var allowed = new HashSet<string>(required, StringComparer.Ordinal);
            allowed.UnionWith(optional);
            foreach (var property in result.Properties())
                if (!allowed.Contains(property.Name)) Error(errors, "unknown_property", path + "." + property.Name,
                    "Property is not supported by this schema version.");
            foreach (var field in required)
                if (result.Property(field, StringComparison.Ordinal) == null) Error(errors, "required", path + "." + field, "Required property is missing.");
            return result;
        }

        private static BuiltinAppearanceData ReadAppearance(JToken token, string path, List<DatabaseImportError> errors)
        {
            var obj = Object(token, path, errors, "skinTone", "hairStyle", "hairColor", "beardStyle",
                "beardColor", "bootsColor", "sockAccessoryColor");
            if (obj == null) return null;
            return new BuiltinAppearanceData(
                Preset(obj["skinTone"], path + ".skinTone", BuiltinAppearancePresets.SkinTones, errors),
                Preset(obj["hairStyle"], path + ".hairStyle", BuiltinAppearancePresets.HairStyles, errors),
                Preset(obj["hairColor"], path + ".hairColor", BuiltinAppearancePresets.HairColors, errors),
                Preset(obj["beardStyle"], path + ".beardStyle", BuiltinAppearancePresets.BeardStyles, errors),
                Preset(obj["beardColor"], path + ".beardColor", BuiltinAppearancePresets.HairColors, errors),
                Preset(obj["bootsColor"], path + ".bootsColor", BuiltinAppearancePresets.BootsColors, errors),
                Preset(obj["sockAccessoryColor"], path + ".sockAccessoryColor", BuiltinAppearancePresets.SockAccessoryColors, errors));
        }

        private static string Preset(JToken token, string path, IReadOnlyList<string> allowed,
            List<DatabaseImportError> errors)
        {
            var value = String(token, path, errors);
            if (value == null) return null;
            for (var i = 0; i < allowed.Count; i++)
                if (string.Equals(allowed[i], value, StringComparison.Ordinal)) return value;
            Error(errors, "unknown_appearance_preset", path,
                "Unknown appearance preset. Supported IDs: " + string.Join(", ", allowed) + ".");
            return value;
        }

        private static void ReadItems(JToken token, string path, int minimum, List<DatabaseImportError> errors, Action<JToken, string> read)
        {
            if (token == null) return;
            if (!(token is JArray array)) { Error(errors, "invalid_type", path, "Expected an array."); return; }
            if (array.Count < minimum) Error(errors, "too_few_items", path, "At least " + minimum + " item(s) are required.");
            for (var i = 0; i < array.Count; i++) read(array[i], path + "[" + i + "]");
        }

        private static string String(JToken token, string path, List<DatabaseImportError> errors)
        {
            if (token == null) return null;
            if (token.Type == JTokenType.String) return token.Value<string>();
            Error(errors, "invalid_type", path, "Expected a string.");
            return null;
        }

        private static string Id(JToken token, string path, List<DatabaseImportError> errors)
        {
            var value = String(token, path, errors);
            if (value != null && !IdPattern.IsMatch(value)) Error(errors, "invalid_id", path, "Use 1-64 ASCII letters, digits, dots, underscores or hyphens, starting with a letter or digit.");
            return value;
        }

        private static string Name(JToken token, string path, List<DatabaseImportError> errors)
        {
            var value = String(token, path, errors);
            if (value != null && (string.IsNullOrWhiteSpace(value) || UnicodeLength(value) > 100))
                Error(errors, "invalid_name", path, "Name must contain non-whitespace text and have at most 100 Unicode code points.");
            return value;
        }

        private static int UnicodeLength(string value)
        {
            var count = 0;
            for (var i = 0; i < value.Length; i++, count++)
                if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1])) i++;
            return count;
        }

        private static int Integer(JToken token, string path, int minimum, int maximum, List<DatabaseImportError> errors)
        {
            if (token == null) return 0;
            if (token.Type != JTokenType.Integer)
            {
                Error(errors, "invalid_type", path, "Expected an integer token without a decimal point or exponent.");
                return 0;
            }
            if (!int.TryParse(token.ToString(Formatting.None), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ||
                value < minimum || value > maximum)
            {
                Error(errors, "out_of_range", path, "Value must be an integer between " + minimum + " and " + maximum + ".");
                return 0;
            }
            return value;
        }

        private static void Error(List<DatabaseImportError> errors, string code, string path, string message)
            => errors.Add(new DatabaseImportError(code, path, message));

        private static DatabaseImportResult Failure(string code, string path, string message)
            => DatabaseImportResult.Rejected(new[] {new DatabaseImportError(code, path, message)});
    }
}
