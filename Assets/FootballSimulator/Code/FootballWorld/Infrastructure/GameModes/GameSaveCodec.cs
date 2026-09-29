using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.DataContracts;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.Importing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FStudio.FootballWorld.Infrastructure.GameModes
{
    public sealed class RestoredChampionship
    {
        public CompetitionSession Session { get; }
        public string DatabaseJson { get; }
        public IReadOnlyList<VisualProfileData> Profiles { get; }
        internal RestoredChampionship(CompetitionSession session, string json, IReadOnlyList<VisualProfileData> profiles)
        { Session = session; DatabaseJson = json; Profiles = profiles; }
    }

    // Explicit storage DTO mapping. Authored content is re-imported and the pure
    // application restores validated snapshots; JSON never populates domain objects.
    public static partial class GameSaveCodec
    {
        public static string Championship(CompetitionSession session, string databaseJson)
        {
            var snapshot = session.CaptureSnapshot();
            // Keep every authored field while avoiding doubled whitespace inside the escaped snapshot string.
            using (var reader = new JsonTextReader(new StringReader(databaseJson)) { DateParseHandling = DateParseHandling.None })
                databaseJson = JObject.Load(reader).ToString(Formatting.None);
            var root = new JObject {
                ["version"] = session.Edition.IsDeclarative ? 3 : 2, ["databaseJson"] = databaseJson,
                ["seasonId"] = snapshot.SeasonId, ["databaseId"] = snapshot.DatabaseId,
                ["databaseRevision"] = snapshot.DatabaseRevision, ["editionId"] = snapshot.EditionId,
                ["controlledClubId"] = snapshot.ControlledClubId,
                ["dailyProgress"] = snapshot.DailyProgress,
                ["currentDate"] = snapshot.CurrentDate?.ToString(),
                ["fixtures"] = new JArray(snapshot.Fixtures.Select(fixture => WriteFixture(fixture, session.Edition.IsDeclarative))),
                ["results"] = new JArray(snapshot.Results.Select(result => new JObject {
                    ["fixtureId"] = result.FixtureId, ["executionId"] = result.ExecutionId,
                    ["homeClubId"] = result.HomeClubId, ["awayClubId"] = result.AwayClubId,
                    ["homeGoals"] = result.HomeGoals, ["awayGoals"] = result.AwayGoals,
                    ["isSimulated"] = result.IsSimulated,
                    ["homeYellowCards"] = result.HomeYellowCards, ["awayYellowCards"] = result.AwayYellowCards,
                    ["homeRedCards"] = result.HomeRedCards, ["awayRedCards"] = result.AwayRedCards,
                    ["homePenalties"] = result.HomePenalties, ["awayPenalties"] = result.AwayPenalties,
                    ["hasSimulatedSupplement"] = result.HasSimulatedSupplement })),
                ["usedExecutionIds"] = new JArray(snapshot.UsedExecutionIds)
            };
            return root.ToString(Formatting.None);
        }

        public static RestoredChampionship RestoreChampionship(string json)
        {
            var root = Read(json);
            var version = Number(root, "version");
            if (version < 1 || version > 3) throw new InvalidOperationException("Unsupported championship save version.");
            var fields = new[] { "version", "databaseJson", "seasonId", "databaseId", "databaseRevision",
                "editionId", "controlledClubId", "fixtures", "results", "usedExecutionIds" };
            RequireFields(root, version == 1 ? fields : fields.Concat(new[] { "dailyProgress", "currentDate" }).ToArray());
            var databaseJson = Text(root, "databaseJson");
            var imported = new JsonDatabaseImporter().Import(databaseJson);
            if (!imported.Success) throw new InvalidOperationException("The saved database snapshot is not supported.");
            if (imported.Catalog.GetCompetitionEdition(Text(root, "editionId")).IsDeclarative != (version == 3))
                throw new InvalidOperationException("The saved competition format requires its matching fixture schema.");
            var fixtures = List(root, "fixtures").Select(token => {
                var item = Object(token);
                var fixtureFields = new[] { "id", "round", "year", "month", "day", "homeClubId", "awayClubId" };
                var additional = version == 1 ? Array.Empty<string>() : version == 2 ? new[] { "stadiumId" }
                    : new[] { "stadiumId", "stageId", "tieId", "leg", "isNeutral" };
                RequireFields(item, fixtureFields.Concat(additional).ToArray());
                return new FixtureDefinition(Text(item, "id"), Number(item, "round"),
                    new GameDate(Number(item, "year"), Number(item, "month"), Number(item, "day")),
                    Text(item, "homeClubId"), Text(item, "awayClubId"), version == 1 ? null : NullableText(item, "stadiumId"),
                    version < 3 ? null : NullableText(item, "stageId"), version < 3 ? null : NullableText(item, "tieId"),
                    version < 3 ? 1 : Number(item, "leg"), version >= 3 && Boolean(item, "isNeutral"));
            }).ToArray();
            var results = List(root, "results").Select(token => {
                var item = Object(token);
                var resultFields = new[] { "fixtureId", "executionId", "homeClubId", "awayClubId", "homeGoals", "awayGoals", "isSimulated" };
                RequireFields(item, version == 1 ? resultFields : resultFields.Concat(new[] {
                    "homeYellowCards", "awayYellowCards", "homeRedCards", "awayRedCards", "homePenalties", "awayPenalties", "hasSimulatedSupplement" }).ToArray());
                if (item["isSimulated"].Type != JTokenType.Boolean) throw new InvalidOperationException("Invalid result mode.");
                return new FixtureResult(Text(item, "fixtureId"), Text(item, "executionId"),
                    Text(item, "homeClubId"), Text(item, "awayClubId"), Number(item, "homeGoals"), Number(item, "awayGoals"), (bool)item["isSimulated"],
                    version == 1 ? 0 : Number(item, "homeYellowCards"), version == 1 ? 0 : Number(item, "awayYellowCards"),
                    version == 1 ? 0 : Number(item, "homeRedCards"), version == 1 ? 0 : Number(item, "awayRedCards"),
                    version == 1 ? null : NullableNumber(item, "homePenalties"), version == 1 ? null : NullableNumber(item, "awayPenalties"),
                    version != 1 && Boolean(item, "hasSimulatedSupplement"));
            }).ToArray();
            var used = List(root, "usedExecutionIds").Select(token => {
                if (token.Type != JTokenType.String) throw new InvalidOperationException("Invalid execution identity.");
                return (string)token;
            }).ToArray();
            var snapshot = new CompetitionSnapshot(Text(root, "seasonId"), Text(root, "databaseId"),
                Number(root, "databaseRevision"), Text(root, "editionId"), Text(root, "controlledClubId"), fixtures, results, used,
                version != 1 && Boolean(root, "dailyProgress"), version == 1 ? null : NullableDate(root, "currentDate"));
            var session = CompetitionSession.Restore(imported.Catalog, snapshot);
            return new RestoredChampionship(session, databaseJson, imported.VisualProfiles);
        }

        private static JObject WriteFixture(FixtureDefinition fixture, bool declarative)
        {
            var value = new JObject {
                ["id"] = fixture.Id, ["round"] = fixture.Round,
                ["year"] = fixture.Date.Year, ["month"] = fixture.Date.Month, ["day"] = fixture.Date.Day,
                ["homeClubId"] = fixture.HomeClubId, ["awayClubId"] = fixture.AwayClubId,
                ["stadiumId"] = fixture.StadiumId
            };
            if (declarative)
            {
                value["stageId"] = fixture.StageId; value["tieId"] = fixture.TieId;
                value["leg"] = fixture.Leg; value["isNeutral"] = fixture.IsNeutral;
            }
            return value;
        }

        public static string Career(HubCareerProfile profile) => new JObject {
            ["version"] = 1, ["coachName"] = profile.CoachName, ["avatarId"] = profile.AvatarId,
            ["startMonth"] = profile.StartMonth, ["startYear"] = profile.StartYear,
            ["clubId"] = profile.ClubId, ["clubName"] = profile.ClubName,
            ["databaseId"] = profile.DatabaseId, ["databaseRevision"] = profile.DatabaseRevision
        }.ToString(Formatting.None);

        public static HubCareerProfile RestoreCareer(string json)
        {
            var root = Read(json);
            RequireFields(root, "version", "coachName", "avatarId", "startMonth", "startYear", "clubId", "clubName", "databaseId", "databaseRevision");
            RequireVersion(root);
            return new HubCareerProfile(Text(root, "coachName"), Text(root, "avatarId"), Number(root, "startMonth"),
                Number(root, "startYear"), Text(root, "clubId"), Text(root, "clubName"), Text(root, "databaseId"), Number(root, "databaseRevision"));
        }

        private static JObject Read(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > LocalGameSaveStore.MaxPayloadBytes)
                throw new InvalidOperationException("Invalid saved content size.");
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, MaxDepth = 32 })
            {
                var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new InvalidOperationException("Unexpected data after save.");
                return root;
            }
        }

        private static void RequireVersion(JObject root)
        { if (Number(root, "version") != 1) throw new InvalidOperationException("Unsupported saved content version."); }
        private static JObject Object(JToken token) => token as JObject ?? throw new InvalidOperationException("Expected a saved object.");
        private static JArray List(JObject root, string name) => root[name] as JArray ?? throw new InvalidOperationException("Expected a saved list.");
        private static string Text(JObject root, string name)
        { if (root[name]?.Type != JTokenType.String) throw new InvalidOperationException("Invalid saved text: " + name); return (string)root[name]; }
        private static int Number(JObject root, string name)
        { if (root[name]?.Type != JTokenType.Integer) throw new InvalidOperationException("Invalid saved number: " + name); return checked((int)root[name]); }
        private static bool Boolean(JObject root, string name)
        { if (root[name]?.Type != JTokenType.Boolean) throw new InvalidOperationException("Invalid saved boolean: " + name); return (bool)root[name]; }
        private static string NullableText(JObject root, string name) => root[name]?.Type == JTokenType.Null ? null : Text(root, name);
        private static int? NullableNumber(JObject root, string name) => root[name]?.Type == JTokenType.Null ? (int?)null : Number(root, name);
        private static GameDate? NullableDate(JObject root, string name)
        {
            var value = NullableText(root, name);
            if (value == null) return null;
            if (!DateTime.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date)) throw new InvalidOperationException("Invalid saved date.");
            return new GameDate(date.Year, date.Month, date.Day);
        }
        private static void RequireFields(JObject root, params string[] fields)
        {
            var actual = new HashSet<string>(root.Properties().Select(property => property.Name), StringComparer.Ordinal);
            if (!actual.SetEquals(fields)) throw new InvalidOperationException("Unsupported or missing saved fields.");
        }
    }
}
