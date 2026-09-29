using System;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Infrastructure.GameModes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Presentation
{
    public sealed class HubCareerOfferRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text heading, description;
        [SerializeField] private Button cancel;
        private Action cancelled;
        private void OnEnable() { if (UnityEngine.Application.isPlaying) cancel.onClick.AddListener(Cancel); }
        private void OnDisable() { if (cancel != null) cancel.onClick.RemoveListener(Cancel); }
        private void Cancel() => cancelled?.Invoke();
        public void Bind(CareerTransferOffer offer, GameHubSession session, Action action)
        {
            var player = session.CareerProgress.EffectiveCatalog.GetPlayer(offer.PlayerId);
            heading.text = player.DisplayName + "  ·  " + session.CareerMoney(offer.Amount) + "  ·  " + GameText.Get("offerStatus." + offer.Status);
            description.text = GameText.Get("career.offerDates", GameText.FormatDate(offer.SubmittedDate.ToDateTime()),
                offer.DecisionDate.HasValue ? GameText.FormatDate(offer.DecisionDate.Value.ToDateTime()) : GameText.Get("career.tomorrow"))
                + "\n" + GameText.Get(offer.Status == CareerTransferStatus.Accepted ? "career.offerCompleted" : "offerReason." + offer.Reason);
            cancel.gameObject.SetActive(offer.Status == CareerTransferStatus.Pending);
            cancel.interactable = !session.IsBusy && session.CareerProgress.CurrentDate.CompareTo(session.CareerProgress.EndDate) < 0;
            cancelled = action;
        }
    }
}
