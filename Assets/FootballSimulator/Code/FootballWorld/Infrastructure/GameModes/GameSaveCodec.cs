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
    public static class GameSaveCodec
    {
        public static string Championship(CompetitionSession session, string databaseJson)
        {
            var snapshot = session.CaptureSnapshot();
            // Keep every authored field while avoiding doubled whitespace inside the escaped snapshot string.
            using (var reader = new JsonTextReader(new StringReader(databaseJson)) { DateParseHandling = DateParseHandling.None })
                databaseJson = JObject.Load(reader).ToString(Formatting.None);
            var root = new JObject {
                ["version"] = 1, ["databaseJson"] = databaseJson,
                ["seasonId"] = snapshot.SeasonId, ["databaseId"] = snapshot.DatabaseId,
                ["databaseRevision"] = snapshot.DatabaseRevision, ["editionId"] = snapshot.EditionId,
                ["controlledClubId"] = snapshot.ControlledClubId,
                ["fixtures"] = new JArray(snapshot.Fixtures.Select(fixture => new JObject {
                    ["id"] = fixture.Id, ["round"] = fixture.Round,
                    ["year"] = fixture.Date.Year, ["month"] = fixture.Date.Month, ["day"] = fixture.Date.Day,
                    ["homeClubId"] = fixture.HomeClubId, ["awayClubId"] = fixture.AwayClubId })),
                ["results"] = new JArray(snapshot.Results.Select(result => new JObject {
                    ["fixtureId"] = result.FixtureId, ["executionId"] = result.ExecutionId,
                    ["homeClubId"] = result.HomeClubId, ["awayClubId"] = result.AwayClubId,
                    ["homeGoals"] = result.HomeGoals, ["awayGoals"] = result.AwayGoals,
                    ["isSimulated"] = result.IsSimulated })),
                ["usedExecutionIds"] = new JArray(snapshot.UsedExecutionIds)
            };
            return root.ToString(Formatting.None);
        }

        public static RestoredChampionship RestoreChampionship(string json)
        {
            var root = Read(json);
            RequireFields(root, "version", "databaseJson", "seasonId", "databaseId", "databaseRevision",
                "editionId", "controlledClubId", "fixtures", "results", "usedExecutionIds");
            RequireVersion(root);
            var databaseJson = Text(root, "databaseJson");
            var imported = new JsonDatabaseImporter().Import(databaseJson);
            if (!imported.Success) throw new InvalidOperationException("The saved database snapshot is not supported.");
            var fixtures = List(root, "fixtures").Select(token => {
                var item = Object(token);
                RequireFields(item, "id", "round", "year", "month", "day", "homeClubId", "awayClubId");
                return new FixtureDefinition(Text(item, "id"), Number(item, "round"),
                    new GameDate(Number(item, "year"), Number(item, "month"), Number(item, "day")),
                    Text(item, "homeClubId"), Text(item, "awayClubId"));
            }).ToArray();
            var results = List(root, "results").Select(token => {
                var item = Object(token);
                RequireFields(item, "fixtureId", "executionId", "homeClubId", "awayClubId", "homeGoals", "awayGoals", "isSimulated");
                if (item["isSimulated"].Type != JTokenType.Boolean) throw new InvalidOperationException("Invalid result mode.");
                return new FixtureResult(Text(item, "fixtureId"), Text(item, "executionId"),
                    Text(item, "homeClubId"), Text(item, "awayClubId"), Number(item, "homeGoals"), Number(item, "awayGoals"), (bool)item["isSimulated"]);
            }).ToArray();
            var used = List(root, "usedExecutionIds").Select(token => {
                if (token.Type != JTokenType.String) throw new InvalidOperationException("Invalid execution identity.");
                return (string)token;
            }).ToArray();
            var snapshot = new CompetitionSnapshot(Text(root, "seasonId"), Text(root, "databaseId"),
                Number(root, "databaseRevision"), Text(root, "editionId"), Text(root, "controlledClubId"), fixtures, results, used);
            var session = CompetitionSession.Restore(imported.Catalog, snapshot);
            return new RestoredChampionship(session, databaseJson, imported.VisualProfiles);
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
        private static void RequireFields(JObject root, params string[] fields)
        {
            var actual = new HashSet<string>(root.Properties().Select(property => property.Name), StringComparer.Ordinal);
            if (!actual.SetEquals(fields)) throw new InvalidOperationException("Unsupported or missing saved fields.");
        }
    }
}
