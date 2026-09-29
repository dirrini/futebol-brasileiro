#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.GameModes;
using FStudio.FootballWorld.Infrastructure.Importing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FStudio.FootballWorld.Editor.Tests
{
    public sealed class CareerTacticsSaveTests
    {
        [Test]
        public void VersionFourRestoresSemanticSlotsAndRolesAlongsideTransfersResultsAndPinnedDatabase()
        {
            var fixture = Create();
            var career = fixture.Session;
            var seller = career.EffectiveCatalog.Clubs.First(club => club.Id != career.Competition.ControlledClubId && career.GetRoster(club.Id).Count >= 12);
            var player = career.GetRoster(seller.Id).First(value => !value.NaturalPositions.Contains(PlayerPosition.GK));
            career.SubmitOffer(player.Id, career.EstimateTransferValue(player.Id));
            career.AdvanceDay();
            var plan = CareerTacticPlan.CreateDefault(CareerFormation.FourTwoThreeOne)
                .WithSlot("left-centre-back", .4f, .4f, CareerPlayerRole.AdvancingCentreBack)
                .WithSlot("left-back", .2f, .42f, CareerPlayerRole.InvertedFullBack)
                .WithSlot("left-holding-midfielder", .4f, .45f, CareerPlayerRole.BallWinningMidfielder)
                .WithSlot("attacking-midfielder", .55f, .8f, CareerPlayerRole.CreativePlaymaker)
                .WithSlot("striker", .46f, .9f, CareerPlayerRole.TargetForward);
            career.SetTactics(plan.Formation, CareerMentality.Attacking, plan);
            career.AdvanceToNextFixture();
            career.SimulateNextFixture();
            var saved = JObject.Parse(GameSaveCodec.DailyCareer(fixture.Profile, career, fixture.Source));
            Assert.That((int)saved["version"], Is.EqualTo(4));
            Assert.That((string)saved["tactics"]["slots"][0]["slotId"], Is.EqualTo("goalkeeper"));
            Assert.That((string)saved["tactics"]["slots"][0]["role"], Is.EqualTo("standard"));
            saved["tactics"]["slots"] = new JArray(((JArray)saved["tactics"]["slots"]).Reverse().Select(value => value.DeepClone()));
            var restored = GameSaveCodec.RestoreDailyCareer(saved.ToString(Formatting.None));
            for (var i = 0; i < 3; i++) restored = GameSaveCodec.RestoreDailyCareer(GameSaveCodec.DailyCareer(restored.Profile, restored.Session, restored.DatabaseJson));
            Assert.That(restored.Session.TacticPlan.SameConfiguration(plan), Is.True);
            Assert.That(restored.Session.TacticPlan.Slots.Select(value => value.SlotId), Is.EqualTo(plan.Slots.Select(value => value.SlotId)));
            Assert.That(restored.Session.Formation, Is.EqualTo(plan.Formation));
            Assert.That(restored.Session.Mentality, Is.EqualTo(CareerMentality.Attacking));
            Assert.That(restored.Session.FinanceBalance, Is.EqualTo(career.FinanceBalance));
            Assert.That(restored.Session.Competition.Results.Count, Is.EqualTo(career.Competition.Results.Count));
            Assert.That(restored.Session.Ledger.Count(value => value.EventKey == "transfer-fee"), Is.EqualTo(1));
            Assert.That(restored.Session.GetCurrentClubId(player.Id), Is.EqualTo(career.Competition.ControlledClubId));
            Assert.That(restored.Session.Competition.Catalog.GetRoster(seller.Id).Any(value => value.Id == player.Id), Is.True);
            Assert.That(JToken.DeepEquals(JToken.Parse(restored.DatabaseJson), JToken.Parse(fixture.Source)), Is.True);
        }

        [Test]
        public void VersionThreeRetainsFormationAndMentalityAndSuppliesOnlyDefaultSlots()
        {
            var fixture = Create();
            fixture.Session.SetTactics(CareerFormation.FourThreeThree, CareerMentality.Defensive);
            fixture.Session.AdvanceToNextFixture();
            fixture.Session.SimulateNextFixture();
            var old = JObject.Parse(GameSaveCodec.DailyCareer(fixture.Profile, fixture.Session, fixture.Source));
            old["version"] = 3; old.Remove("tactics");
            var restored = GameSaveCodec.RestoreDailyCareer(old.ToString(Formatting.None));
            Assert.That(restored.Session.Formation, Is.EqualTo(CareerFormation.FourThreeThree));
            Assert.That(restored.Session.Mentality, Is.EqualTo(CareerMentality.Defensive));
            Assert.That(restored.Session.TacticPlan.IsDefault, Is.True);
            Assert.That(restored.Session.TacticPlan.Formation, Is.EqualTo(CareerFormation.FourThreeThree));
            Assert.That(restored.Session.FinanceBalance, Is.EqualTo(fixture.Session.FinanceBalance));
            Assert.That(restored.Session.Competition.Results.Count, Is.EqualTo(fixture.Session.Competition.Results.Count));
            var upgraded = JObject.Parse(GameSaveCodec.DailyCareer(restored.Profile, restored.Session, restored.DatabaseJson));
            Assert.That((int)upgraded["version"], Is.EqualTo(4));
            Assert.That(GameSaveCodec.RestoreDailyCareer(upgraded.ToString(Formatting.None)).Session.TacticPlan.IsDefault, Is.True);
        }

        [TestCase(CareerFormation.FourFourTwo)]
        [TestCase(CareerFormation.FourThreeThree)]
        [TestCase(CareerFormation.FourTwoThreeOne)]
        public void ExactFloatAdjustmentBoundsRemainValidAcrossRepeatedJsonRoundTrips(CareerFormation formation)
        {
            var fixture = Create();
            var definitions = CareerTacticPlan.GetSlotDefinitions(formation);
            foreach (var maximum in new[] { false, true })
            {
                var plan = new CareerTacticPlan(formation, definitions.Select(value => new CareerTacticSlot(value.SlotId,
                    maximum ? value.MaxX : value.MinX, maximum ? value.MaxDepth : value.MinDepth, value.AllowedRoles.Last())));
                fixture.Session.SetTactics(formation, CareerMentality.Balanced, plan);
                var restored = GameSaveCodec.RestoreDailyCareer(GameSaveCodec.DailyCareer(fixture.Profile, fixture.Session, fixture.Source));
                for (var i = 0; i < 3; i++) restored = GameSaveCodec.RestoreDailyCareer(GameSaveCodec.DailyCareer(restored.Profile, restored.Session, restored.DatabaseJson));
                Assert.That(restored.Session.TacticPlan.SameConfiguration(plan), Is.True);
            }
        }

        [Test]
        public void InvalidSlotSetsRolesCoordinatesAndVersionsAreRejectedWithoutChangingLiveCareer()
        {
            var fixture = Create();
            var original = fixture.Session.TacticPlan;
            var saved = JObject.Parse(GameSaveCodec.DailyCareer(fixture.Profile, fixture.Session, fixture.Source));
            var mutations = new Action<JObject>[] {
                root => root.Remove("tactics"),
                root => root["tactics"] = null,
                root => root["tactics"]["version"] = 2,
                root => root["tactics"]["version"] = "1",
                root => root["tactics"]["extra"] = 1,
                root => ((JArray)root["tactics"]["slots"]).RemoveAt(0),
                root => root["tactics"]["slots"][1] = root["tactics"]["slots"][0].DeepClone(),
                root => root["tactics"]["slots"][0]["slotId"] = "player-1",
                root => root["tactics"]["slots"][0]["role"] = "Standard",
                root => root["tactics"]["slots"][0]["role"] = "sweeper-keeper",
                root => root["tactics"]["slots"][0]["role"] = 0,
                root => root["tactics"]["slots"][0]["role"] = "target-forward",
                root => root["tactics"]["slots"][0]["x"] = "0.5",
                root => root["tactics"]["slots"][0]["x"] = true,
                root => root["tactics"]["slots"][0]["x"] = -0.1,
                root => root["tactics"]["slots"][0]["x"] = 1.1,
                root => root["tactics"]["slots"][0]["x"] = double.NaN,
                root => root["tactics"]["slots"][0]["x"] = double.PositiveInfinity,
                root => root["tactics"]["slots"][0]["depth"] = .21,
                root => root["tactics"]["slots"][1]["x"] = .5,
                root => root["tactics"]["slots"][1]["depth"] = .6,
                root => ((JObject)root["tactics"]["slots"][0]).Remove("depth"),
                root => root["tactics"]["slots"][0]["playerId"] = "player-1",
                root => root["formation"] = (int)CareerFormation.FourThreeThree
            };
            for (var i = 0; i < mutations.Length; i++)
            {
                var changed = (JObject)saved.DeepClone();
                mutations[i](changed);
                Assert.That(() => GameSaveCodec.RestoreDailyCareer(changed.ToString(Formatting.None)), Throws.Exception, "Mutation " + i);
            }
            Assert.That(fixture.Session.TacticPlan, Is.SameAs(original));
            Assert.That(GameSaveCodec.RestoreDailyCareer(saved.ToString(Formatting.None)).Session.TacticPlan.SameConfiguration(original), Is.True);
        }

        private static Fixture Create()
        {
            var source = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json"));
            var imported = new JsonDatabaseImporter().Import(source);
            Assert.That(imported.Success, Is.True, string.Join("; ", imported.Errors.Select(value => value.Message)));
            var edition = imported.Catalog.CompetitionEditions.First();
            var club = imported.Catalog.GetClub(edition.ParticipantClubIds[0]);
            return new Fixture {
                Source = source,
                Session = CareerSession.Create(imported.Catalog, edition.Id, "career-tactics-codec", club.Id, new GameDate(2026, 1, 1)),
                Profile = new HubCareerProfile("Treinador tático", "coach-2", 1, 2026, club.Id, club.Name, imported.Catalog.DatabaseId, imported.Catalog.DatabaseRevision)
            };
        }
        private sealed class Fixture { public string Source; public CareerSession Session; public HubCareerProfile Profile; }
    }
}
#endif
