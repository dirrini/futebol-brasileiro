using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.GameModes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Presentation
{
    public sealed class GameHubCareerMarket : GameHubCareerPlayers
    {
        [SerializeField] private GameHubView hub;
        [SerializeField] private GameObject searchPanel, offersPanel;
        [SerializeField] private Button searchTab, offersTab, submit, previousOffers, nextOffers;
        [SerializeField] private TMP_InputField amount;
        [SerializeField] private TMP_Text offerHint, budget, offerCount, offerPages, emptyOffers;
        [SerializeField] private HubCareerOfferRow offerTemplate;
        [SerializeField] private ScrollRect offerScroll;
        [SerializeField, Min(1)] private int offersPerPage = 5;
        [SerializeField] private GameHubTheme theme;
        private readonly List<HubCareerOfferRow> offerRows = new List<HubCareerOfferRow>();
        private string amountPlayerId;
        private bool showingOffers;
        private int offersPage;
        protected override IEnumerable<PlayerDefinition> SourcePlayers(CareerSession career) => career.EffectiveCatalog.Players;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Session == null) return;
            searchTab.onClick.AddListener(Search); offersTab.onClick.AddListener(Offers);
            submit.onClick.AddListener(Submit); previousOffers.onClick.AddListener(PreviousOffers); nextOffers.onClick.AddListener(NextOffers);
            amount.onValueChanged.AddListener(AmountChanged);
        }
        protected override void OnDisable()
        {
            if (Session == null) return;
            searchTab.onClick.RemoveListener(Search); offersTab.onClick.RemoveListener(Offers);
            submit.onClick.RemoveListener(Submit); previousOffers.onClick.RemoveListener(PreviousOffers); nextOffers.onClick.RemoveListener(NextOffers);
            amount.onValueChanged.RemoveListener(AmountChanged); base.OnDisable();
        }
        private void Search() { showingOffers = false; Refresh(); }
        private void Offers() { showingOffers = true; Refresh(); }
        private void PreviousOffers() { offersPage--; Refresh(); offerScroll.verticalNormalizedPosition = 1; }
        private void NextOffers() { offersPage++; Refresh(); offerScroll.verticalNormalizedPosition = 1; }
        private void AmountChanged(string value) { if (Session?.CareerProgress != null) RefreshOfferForm(Session.CareerProgress); }
        protected override void RefreshExtra(CareerSession career)
        {
            searchPanel.SetActive(!showingOffers); offersPanel.SetActive(showingOffers);
            searchTab.GetComponent<Image>().color = showingOffers ? theme.Surface : theme.Accent;
            offersTab.GetComponent<Image>().color = showingOffers ? theme.Accent : theme.Surface;
            if (amountPlayerId != SelectedPlayer?.Id)
            {
                amountPlayerId = SelectedPlayer?.Id;
                amount.SetTextWithoutNotify(SelectedPlayer == null ? string.Empty : career.EstimateTransferValue(SelectedPlayer.Id).ToString(CultureInfo.InvariantCulture));
            }
            RefreshOfferForm(career);
            budget.text = GameText.Get("career.transferBudget", Session.CareerMoney(career.AvailableTransferBudget), Session.CareerMoney(career.ReservedTransferBudget));
            var offers = career.Offers.Reverse().ToArray();
            var pages = Math.Max(1, (offers.Length + offersPerPage - 1) / offersPerPage);
            offersPage = Math.Max(0, Math.Min(offersPage, pages - 1));
            var visible = offers.Skip(offersPage * offersPerPage).Take(offersPerPage).ToArray();
            for (var index = 0; index < visible.Length; index++)
            {
                if (index == offerRows.Count) offerRows.Add(Instantiate(offerTemplate, offerTemplate.transform.parent));
                var offer = visible[index]; offerRows[index].gameObject.SetActive(true);
                offerRows[index].Bind(offer, Session, () => Session.CancelCareerOffer(offer.Id));
            }
            for (var index = visible.Length; index < offerRows.Count; index++) offerRows[index].gameObject.SetActive(false);
            offerCount.text = GameText.Get("career.offerCount", offers.Length);
            offerPages.text = GameText.Get("career.pageCount", offersPage + 1, pages);
            emptyOffers.gameObject.SetActive(offers.Length == 0);
            previousOffers.interactable = offersPage > 0; nextOffers.interactable = offersPage + 1 < pages;
        }
        private void RefreshOfferForm(CareerSession career)
        {
            var ended = career.CurrentDate.CompareTo(career.EndDate) >= 0;
            var owned = SelectedPlayer != null && career.GetCurrentClubId(SelectedPlayer.Id) == career.Competition.ControlledClubId;
            var pending = SelectedPlayer != null && career.Offers.Any(offer => offer.PlayerId == SelectedPlayer.Id && offer.Status == CareerTransferStatus.Pending);
            amount.interactable = SelectedPlayer != null && !owned && !pending && !ended && !Session.IsBusy;
            submit.interactable = amount.interactable;
            offerHint.text = ended ? GameText.Get("career.managementEnded") : SelectedPlayer == null ? GameText.Get("career.selectPlayer") : owned ? GameText.Get("career.alreadyOwned")
                : pending ? GameText.Get("career.offerPending") : GameText.Get("career.valuation", Session.CareerMoney(career.EstimateTransferValue(SelectedPlayer.Id)),
                    Session.CareerMoney(career.AvailableTransferBudget));
        }
        private void Submit()
        {
            if (SelectedPlayer == null || Session.IsBusy) return;
            if (!long.TryParse(amount.text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 0 || value > int.MaxValue)
            {
                offerHint.text = GameText.Get("career.offerInvalid"); amount.Select(); amount.ActivateInputField(); return;
            }
            if (value > Session.CareerProgress.AvailableTransferBudget)
            {
                offerHint.text = GameText.Get("career.offerInsufficientFunds"); amount.Select(); amount.ActivateInputField(); return;
            }
            var playerId = SelectedPlayer.Id;
            hub.ConfirmCareerOffer(GameText.Get("career.offerConfirmBody", SelectedPlayer.DisplayName, ClubName(SelectedPlayer), Session.CareerMoney(value)),
                () => Session.SubmitCareerOffer(playerId, value));
        }
    }
}
