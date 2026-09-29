using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.DataContracts;
using FStudio.FootballWorld.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FStudio.FootballWorld.Infrastructure.GameModes
{
    public sealed class RestoredCareer
    {
        public HubCareerProfile Profile { get; }
        public CareerSession Session { get; }
        public string DatabaseJson { get; }
        public IReadOnlyList<VisualProfileData> Profiles { get; }
        internal RestoredCareer(HubCareerProfile profile, CareerSession session = null,
            string databaseJson = null, IReadOnlyList<VisualProfileData> profiles = null)
        { Profile = profile; Session = session; DatabaseJson = databaseJson; Profiles = profiles; }
    }

    public static partial class GameSaveCodec
    {
        public static string DailyCareer(HubCareerProfile profile, CareerSession session, string databaseJson)
        {
            var state = session.CaptureSnapshot();
            var rules = state.Rules;
            return new JObject {
                ["version"] = 4, ["profile"] = JObject.Parse(Career(profile)),
                ["competition"] = JObject.Parse(Championship(session.Competition, databaseJson)),
                ["startDate"] = state.StartDate.ToString(), ["training"] = (int)state.Training,
                ["condition"] = state.Condition, ["preparation"] = state.Preparation,
                ["rules"] = new JObject { ["version"] = rules.Version, ["defaultInitialBalance"] = rules.DefaultInitialBalance,
                    ["defaultMonthlyWages"] = rules.DefaultMonthlyWages, ["monthlyIncome"] = rules.MonthlyIncome, ["homeMatchIncome"] = rules.HomeMatchIncome },
                ["trainingChanges"] = new JArray(state.TrainingChanges.Select(item => new JObject {
                    ["date"] = item.Date.ToString(), ["training"] = (int)item.Training })),
                ["ledger"] = new JArray(state.Ledger.Select(item => new JObject {
                    ["id"] = item.Id, ["date"] = item.Date.ToString(), ["eventKey"] = item.EventKey,
                    ["amount"] = item.Amount, ["fixtureId"] = item.FixtureId })),
                ["news"] = new JArray(state.News.Select(item => new JObject {
                    ["id"] = item.Id, ["date"] = item.Date.ToString(), ["outletId"] = item.OutletId, ["eventKey"] = item.EventKey,
                    ["fixtureId"] = item.FixtureId, ["amount"] = item.Amount, ["condition"] = item.Condition, ["preparation"] = item.Preparation,
                    ["playerId"] = item.PlayerId })),
                ["processedFixtureIds"] = new JArray(state.ProcessedFixtureIds),
                ["formation"] = (int)state.Formation, ["mentality"] = (int)state.Mentality,
                ["tactics"] = WriteTactics(state.TacticPlan),
                ["offers"] = new JArray(state.Offers.Select(item => new JObject {
                    ["id"] = item.Id, ["playerId"] = item.PlayerId, ["sellerClubId"] = item.SellerClubId,
                    ["amount"] = item.Amount, ["submittedDate"] = item.SubmittedDate.ToString(),
                    ["submissionOrder"] = item.SubmissionOrder, ["submittedLedgerCount"] = item.SubmittedLedgerCount,
                    ["status"] = (int)item.Status, ["decisionDate"] = item.DecisionDate?.ToString(),
                    ["reason"] = (int)item.Reason, ["cancellationOrder"] = item.CancellationOrder }))
            }.ToString(Formatting.None);
        }

        public static RestoredCareer RestoreDailyCareer(string json)
        {
            var root = Read(json);
            var version = Number(root, "version");
            if (version == 1) return new RestoredCareer(RestoreCareer(json));
            if (version < 2 || version > 4) throw new InvalidOperationException("Unsupported career save version.");
            var fields = new[] { "version", "profile", "competition", "startDate", "training", "condition", "preparation",
                "rules", "trainingChanges", "ledger", "news", "processedFixtureIds" };
            var expectedFields = version == 2 ? fields : fields.Concat(new[] { "formation", "mentality", "offers" }).ToArray();
            RequireFields(root, version == 4 ? expectedFields.Concat(new[] { "tactics" }).ToArray() : expectedFields);
            var profile = RestoreCareer(Object(root["profile"]).ToString(Formatting.None));
            var season = RestoreChampionship(Object(root["competition"]).ToString(Formatting.None));
            var rules = Object(root["rules"]);
            RequireFields(rules, "version", "defaultInitialBalance", "defaultMonthlyWages", "monthlyIncome", "homeMatchIncome");
            var changes = List(root, "trainingChanges").Select(token => {
                var item = Object(token); RequireFields(item, "date", "training");
                return new CareerTrainingChange(Date(item, "date"), (CareerTraining)Number(item, "training"));
            }).ToArray();
            var ledger = List(root, "ledger").Select(token => {
                var item = Object(token); RequireFields(item, "id", "date", "eventKey", "amount", "fixtureId");
                return new CareerLedgerEntry(Text(item, "id"), Date(item, "date"), Text(item, "eventKey"),
                    Long(item, "amount"), NullableText(item, "fixtureId"));
            }).ToArray();
            var news = List(root, "news").Select(token => {
                var item = Object(token);
                var newsFields = new[] { "id", "date", "outletId", "eventKey", "fixtureId", "amount", "condition", "preparation" };
                RequireFields(item, version == 2 ? newsFields : newsFields.Concat(new[] { "playerId" }).ToArray());
                return new CareerNewsItem(Text(item, "id"), Date(item, "date"), Text(item, "outletId"), Text(item, "eventKey"),
                    NullableText(item, "fixtureId"), Long(item, "amount"), Number(item, "condition"), Number(item, "preparation"),
                    version == 2 ? null : NullableText(item, "playerId"));
            }).ToArray();
            var offers = version == 2 ? Array.Empty<CareerTransferOffer>() : List(root, "offers").Select(token => {
                var item = Object(token);
                RequireFields(item, "id", "playerId", "sellerClubId", "amount", "submittedDate", "submissionOrder", "submittedLedgerCount",
                    "status", "decisionDate", "reason", "cancellationOrder");
                return new CareerTransferOffer(Text(item, "id"), Text(item, "playerId"), NullableText(item, "sellerClubId"),
                    Long(item, "amount"), Date(item, "submittedDate"), Number(item, "submissionOrder"), Number(item, "submittedLedgerCount"),
                    (CareerTransferStatus)Number(item, "status"), NullableDate(item, "decisionDate"),
                    (CareerTransferReason)Number(item, "reason"), Number(item, "cancellationOrder"));
            }).ToArray();
            var processed = List(root, "processedFixtureIds").Select(token => {
                if (token.Type != JTokenType.String) throw new InvalidOperationException("Invalid processed fixture identity.");
                return (string)token;
            }).ToArray();
            var snapshot = new CareerSnapshot(season.Session.CaptureSnapshot(), Date(root, "startDate"),
                new CareerManagementRules(Number(rules, "version"), Long(rules, "defaultInitialBalance"),
                    Long(rules, "defaultMonthlyWages"), Long(rules, "monthlyIncome"), Long(rules, "homeMatchIncome")),
                (CareerTraining)Number(root, "training"), Number(root, "condition"), Number(root, "preparation"), changes, ledger, news, processed,
                version == 2 ? CareerFormation.FourFourTwo : (CareerFormation)Number(root, "formation"),
                version == 2 ? CareerMentality.Balanced : (CareerMentality)Number(root, "mentality"), offers,
                version < 4 ? null : ReadTactics(Object(root["tactics"]), (CareerFormation)Number(root, "formation")));
            if (profile.DatabaseId != season.Session.Catalog.DatabaseId || profile.DatabaseRevision != season.Session.Catalog.DatabaseRevision ||
                profile.ClubId != season.Session.ControlledClubId || profile.StartYear != snapshot.StartDate.Year || profile.StartMonth != snapshot.StartDate.Month ||
                profile.ClubName != season.Session.Catalog.GetClub(profile.ClubId).Name)
                throw new InvalidOperationException("Career profile and pinned season do not match.");
            var session = CareerSession.Restore(season.Session.Catalog, snapshot);
            return new RestoredCareer(profile, session, season.DatabaseJson, season.Profiles);
        }

        private static GameDate Date(JObject root, string name) => NullableDate(root, name) ?? throw new InvalidOperationException("Missing saved date.");
        private static long Long(JObject root, string name)
        { if (root[name]?.Type != JTokenType.Integer) throw new InvalidOperationException("Invalid saved integer: " + name); return checked((long)root[name]); }
    }
}
