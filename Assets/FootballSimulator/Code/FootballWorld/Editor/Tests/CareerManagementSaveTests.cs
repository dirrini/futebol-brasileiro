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
    public sealed class CareerManagementSaveTests
    {
        [Test]
        public void PendingOfferReservesBudgetAcrossReloadAndCancellationReleasesItWithoutPayment()
        {
            var fixture = CreateCareer();
            var career = fixture.Career;
            var player = Candidate(career);
            var openingBalance = career.FinanceBalance;
            var amount = career.EstimateTransferValue(player.Id);
            var offer = career.SubmitOffer(player.Id, amount);
            var pending = Restore(fixture, career);
            Assert.That(pending.Session.Offers.Single().Status, Is.EqualTo(CareerTransferStatus.Pending));
            Assert.That(pending.Session.ReservedTransferBudget, Is.EqualTo(amount));
            Assert.That(pending.Session.AvailableTransferBudget, Is.EqualTo(openingBalance - amount));
            Assert.That(pending.Session.FinanceBalance, Is.EqualTo(openingBalance));
            Assert.That(pending.Session.GetCurrentClubId(player.Id), Is.EqualTo(offer.SellerClubId));

            Assert.That(pending.Session.CancelOffer(offer.Id), Is.True);
            var cancelled = GameSaveCodec.RestoreDailyCareer(GameSaveCodec.DailyCareer(pending.Profile, pending.Session, pending.DatabaseJson));
            Assert.That(cancelled.Session.Offers.Single().Status, Is.EqualTo(CareerTransferStatus.Cancelled));
            Assert.That(cancelled.Session.ReservedTransferBudget, Is.Zero);
            Assert.That(cancelled.Session.AvailableTransferBudget, Is.EqualTo(openingBalance));
            Assert.That(cancelled.Session.AdvanceDay(), Is.True);
            Assert.That(cancelled.Session.Ledger.Any(value => value.EventKey == "transfer-fee"), Is.False);
            Assert.That(cancelled.Session.GetCurrentClubId(player.Id), Is.EqualTo(offer.SellerClubId));
            Assert.That(JToken.DeepEquals(JToken.Parse(cancelled.DatabaseJson), JToken.Parse(fixture.Source)), Is.True);
        }

        [Test]
        public void AcceptedHistoricalPlayerResumesOnceWithEffectiveOwnershipPinnedSourceAndTactics()
        {
            var fixture = CreateCareer();
            var career = fixture.Career;
            var catalog = career.Competition.Catalog;
            var buyer = career.Competition.ControlledClubId;
            var recruit = Candidate(career);
            var seller = career.GetCurrentClubId(recruit.Id);
            var buyerCount = catalog.GetRoster(buyer).Count;
            var sellerCount = catalog.GetRoster(seller).Count;
            var amount = career.EstimateTransferValue(recruit.Id);
            var openingBalance = career.FinanceBalance;
            career.SetTactics(CareerFormation.FourThreeThree, CareerMentality.Attacking);
            career.SetTraining(CareerTraining.Intensive);
            var offer = career.SubmitOffer(recruit.Id, amount);
            Assert.That(career.AdvanceDay(), Is.True);
            Assert.That(career.Offers.Single().Status, Is.EqualTo(CareerTransferStatus.Accepted));

            var once = Restore(fixture, career);
            var twice = GameSaveCodec.RestoreDailyCareer(GameSaveCodec.DailyCareer(once.Profile, once.Session, once.DatabaseJson));
            var restored = twice.Session;
            Assert.That(restored.Formation, Is.EqualTo(CareerFormation.FourThreeThree));
            Assert.That(restored.Mentality, Is.EqualTo(CareerMentality.Attacking));
            Assert.That(restored.Training, Is.EqualTo(CareerTraining.Intensive));
            Assert.That(restored.Condition, Is.EqualTo(career.Condition));
            Assert.That(restored.Preparation, Is.EqualTo(career.Preparation));
            Assert.That(restored.CurrentDate, Is.EqualTo(new GameDate(2026, 1, 2)));
            Assert.That(restored.Offers.Single().Id, Is.EqualTo(offer.Id));
            Assert.That(restored.Offers.Single().DecisionDate, Is.EqualTo(new GameDate(2026, 1, 2)));
            Assert.That(restored.FinanceBalance, Is.EqualTo(openingBalance - amount));
            Assert.That(restored.ReservedTransferBudget, Is.Zero);
            Assert.That(restored.Ledger.Count(value => value.EventKey == "transfer-fee"), Is.EqualTo(1));
            Assert.That(restored.Ledger.Single(value => value.EventKey == "transfer-fee").Amount, Is.EqualTo(-amount));
            Assert.That(restored.News.Count(value => value.EventKey == "transfer-accepted" && value.PlayerId == recruit.Id), Is.EqualTo(1));
            Assert.That(restored.GetCurrentClubId(recruit.Id), Is.EqualTo(buyer));
            Assert.That(restored.GetRoster().Count, Is.EqualTo(buyerCount + 1));
            Assert.That(restored.GetRoster(seller).Count, Is.EqualTo(sellerCount - 1));
            Assert.That(restored.GetRoster().Single(value => value.Id == recruit.Id).DisplayName, Is.EqualTo(recruit.DisplayName));
            Assert.That(restored.Competition.Catalog.GetRoster(seller).Any(value => value.Id == recruit.Id), Is.True);
            Assert.That(restored.Competition.Catalog.GetRoster(buyer).Count, Is.EqualTo(buyerCount));
            Assert.That(catalog.GetRoster(seller).Count, Is.EqualTo(sellerCount));
            Assert.That(JToken.DeepEquals(JToken.Parse(twice.DatabaseJson), JToken.Parse(fixture.Source)), Is.True);
            Assert.That(twice.Profiles.Count, Is.EqualTo(fixture.VisualProfileCount));
            Assert.That(twice.Profile.DatabaseRevision, Is.EqualTo(catalog.DatabaseRevision));

            Assert.That(restored.AdvanceDay(), Is.True);
            Assert.That(restored.FinanceBalance, Is.EqualTo(openingBalance - amount));
            Assert.That(restored.Ledger.Count(value => value.EventKey == "transfer-fee"), Is.EqualTo(1));
            Assert.That(restored.News.Count(value => value.EventKey == "transfer-accepted"), Is.EqualTo(1));
        }

        [Test]
        public void VersionTwoDailySaveRemainsReadableWithDefaultTacticsAndNoInventedOffers()
        {
            var fixture = CreateCareer();
            var career = fixture.Career;
            career.SetTraining(CareerTraining.Recovery);
            career.AdvanceToNextFixture();
            career.SimulateNextFixture();
            var old = JObject.Parse(GameSaveCodec.DailyCareer(fixture.Profile, career, fixture.Source));
            old["version"] = 2;
            old.Remove("formation"); old.Remove("mentality"); old.Remove("offers");
            foreach (var item in old["news"].OfType<JObject>()) item.Remove("playerId");
            var restored = GameSaveCodec.RestoreDailyCareer(old.ToString(Formatting.None));
            Assert.That(restored.Session.Formation, Is.EqualTo(CareerFormation.FourFourTwo));
            Assert.That(restored.Session.Mentality, Is.EqualTo(CareerMentality.Balanced));
            Assert.That(restored.Session.Offers, Is.Empty);
            Assert.That(restored.Session.ReservedTransferBudget, Is.Zero);
            Assert.That(restored.Session.AvailableTransferBudget, Is.EqualTo(career.FinanceBalance));
            Assert.That(restored.Session.CurrentDate, Is.EqualTo(career.CurrentDate));
            Assert.That(restored.Session.Training, Is.EqualTo(career.Training));
            Assert.That(restored.Session.Condition, Is.EqualTo(career.Condition));
            Assert.That(restored.Session.FinanceBalance, Is.EqualTo(career.FinanceBalance));
            Assert.That(restored.Session.Competition.Results.Count, Is.EqualTo(career.Competition.Results.Count));
            Assert.That(JToken.DeepEquals(JToken.Parse(restored.DatabaseJson), JToken.Parse(fixture.Source)), Is.True);
            var upgraded = JObject.Parse(GameSaveCodec.DailyCareer(restored.Profile, restored.Session, restored.DatabaseJson));
            Assert.That((int)upgraded["version"], Is.EqualTo(3));
            Assert.That(GameSaveCodec.RestoreDailyCareer(upgraded.ToString(Formatting.None)).Session.Offers, Is.Empty);
        }

        [Test]
        public void CodecRejectsIndividuallyTamperedOfferAmountStatusOrderPlayerAndDecisionDate()
        {
            var fixture = CreateCareer();
            var career = fixture.Career;
            var recruit = Candidate(career);
            var seller = career.GetCurrentClubId(recruit.Id);
            var otherPlayer = career.GetRoster(seller).First(value => value.Id != recruit.Id && !value.NaturalPositions.Contains(PlayerPosition.GK));
            career.SubmitOffer(recruit.Id, career.EstimateTransferValue(recruit.Id));
            career.AdvanceDay();
            var saved = JObject.Parse(GameSaveCodec.DailyCareer(fixture.Profile, career, fixture.Source));
            var mutations = new Action<JObject>[] {
                offer => offer["amount"] = (long)offer["amount"] + 1,
                offer => { offer["status"] = (int)CareerTransferStatus.Rejected; offer["reason"] = (int)CareerTransferReason.BelowValuation; },
                offer => offer["submissionOrder"] = (int)offer["submissionOrder"] + 1,
                offer => offer["playerId"] = otherPlayer.Id,
                offer => offer["decisionDate"] = "2026-01-03"
            };
            var labels = new[] { "amount", "status", "command order", "player identity", "decision date" };
            for (var i = 0; i < mutations.Length; i++)
            {
                var changed = (JObject)saved.DeepClone();
                mutations[i]((JObject)changed["offers"][0]);
                Assert.That(() => GameSaveCodec.RestoreDailyCareer(changed.ToString(Formatting.None)), Throws.Exception,
                    "A tampered " + labels[i] + " must not change persisted ownership or finances.");
            }
            // Invalid imports do not mutate the valid source string or the live career.
            var stillValid = GameSaveCodec.RestoreDailyCareer(saved.ToString(Formatting.None));
            Assert.That(stillValid.Session.GetCurrentClubId(recruit.Id), Is.EqualTo(career.Competition.ControlledClubId));
            Assert.That(stillValid.Session.FinanceBalance, Is.EqualTo(career.FinanceBalance));
        }

        [Test]
        public void CodecRejectsUnsupportedTacticalValuesInsteadOfSilentlyChoosingDefaults()
        {
            var fixture = CreateCareer();
            var saved = JObject.Parse(GameSaveCodec.DailyCareer(fixture.Profile, fixture.Career, fixture.Source));
            foreach (var key in new[] { "formation", "mentality" })
            {
                var changed = (JObject)saved.DeepClone();
                changed[key] = 999;
                Assert.That(() => GameSaveCodec.RestoreDailyCareer(changed.ToString(Formatting.None)), Throws.Exception, key);
            }
        }

        private static RestoredCareer Restore(Fixture fixture, CareerSession career)
            => GameSaveCodec.RestoreDailyCareer(GameSaveCodec.DailyCareer(fixture.Profile, career, fixture.Source));

        private static PlayerDefinition Candidate(CareerSession career)
        {
            var seller = career.EffectiveCatalog.Clubs.First(club => club.Id != career.Competition.ControlledClubId && career.GetRoster(club.Id).Count >= 12);
            return career.GetRoster(seller.Id).First(player => !player.NaturalPositions.Contains(PlayerPosition.GK));
        }

        private static Fixture CreateCareer()
        {
            var source = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
                "FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json"));
            var imported = new JsonDatabaseImporter().Import(source);
            Assert.That(imported.Success, Is.True, string.Join("; ", imported.Errors.Select(value => value.Message)));
            var edition = imported.Catalog.CompetitionEditions.First(value => value.Rules.IsPaulista2026);
            var club = imported.Catalog.GetClub(edition.ParticipantClubIds[0]);
            var career = CareerSession.Create(imported.Catalog, edition.Id, "career-management-codec", club.Id, new GameDate(2026, 1, 1));
            Assert.That(career.FinanceBalance, Is.EqualTo(5000000));
            return new Fixture {
                Source = source, Career = career, VisualProfileCount = imported.VisualProfiles.Count,
                Profile = new HubCareerProfile("Treinador de gestão", "coach-2", 1, 2026,
                    club.Id, club.Name, imported.Catalog.DatabaseId, imported.Catalog.DatabaseRevision)
            };
        }

        private sealed class Fixture
        {
            public string Source;
            public CareerSession Career;
            public HubCareerProfile Profile;
            public int VisualProfileCount;
        }
    }
}
#endif
