using System;
using System.Linq;
using FStudio.FootballWorld.Infrastructure.GameModes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Presentation
{
    public sealed class GameHubCareerOffice : MonoBehaviour
    {
        [SerializeField] private TMP_Text heading, calendar, finance, fitness, news, ledger;
        [SerializeField] private TMP_Dropdown training;
        [SerializeField] private Button advance, next, play, simulate, competition, newCareer;
        private GameHubSession session;
        private bool refreshing;

        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying) return;
            session = GameHubSession.Current;
            session.Changed += Refresh; GameText.Changed += Refresh;
            advance.onClick.AddListener(Advance); next.onClick.AddListener(Next);
            play.onClick.AddListener(Play); simulate.onClick.AddListener(Simulate);
            competition.onClick.AddListener(Calendar); newCareer.onClick.AddListener(NewCareer);
            training.onValueChanged.AddListener(Train);
            Refresh();
        }
        private void OnDisable()
        {
            if (session == null) return;
            session.Changed -= Refresh; GameText.Changed -= Refresh;
            advance.onClick.RemoveListener(Advance); next.onClick.RemoveListener(Next);
            play.onClick.RemoveListener(Play); simulate.onClick.RemoveListener(Simulate);
            competition.onClick.RemoveListener(Calendar); newCareer.onClick.RemoveListener(NewCareer);
            training.onValueChanged.RemoveListener(Train); session = null;
        }
        private void Advance() => session.AdvanceCareerDay();
        private void Next() => session.AdvanceCareerToFixture();
        private void Simulate() => session.SimulateCareerFixture();
        private void Calendar() => session.OpenCareerCalendar();
        private void NewCareer() => session.OpenCareerDraft();
        private void Train(int index) { if (!refreshing) session.SetCareerTraining(index); }
        private async void Play()
        {
            try { await session.PlayCareerFixture(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        private void Refresh()
        {
            if (session?.CareerProgress == null || refreshing) return;
            refreshing = true;
            try
            {
                var progress = session.CareerProgress;
                var season = progress.Competition;
                heading.text = GameText.FormatDate(progress.CurrentDate.ToDateTime()) + "  ·  " + session.Career.ClubName
                    + "\n" + session.Career.CoachName + "  ·  " + GameText.Get("phase." + season.Phase);
                var fixture = season.NextFixture;
                calendar.text = fixture == null ? GameText.Get(season.IsComplete ? "career.calendarEnded" : "career.followFinals")
                    : GameText.FormatDate(fixture.Date.ToDateTime()) + "\n" + season.Catalog.GetClub(fixture.HomeClubId).Name
                        + " × " + season.Catalog.GetClub(fixture.AwayClubId).Name;
                finance.text = GameText.Get("career.balance", session.CareerMoney(progress.FinanceBalance)) + "\n"
                    + GameText.Get("career.monthly", session.CareerMoney(progress.MonthlyIncome), session.CareerMoney(progress.MonthlyWages));
                fitness.text = GameText.Get("career.fitness", progress.Condition, progress.Preparation, progress.MatchPerformancePercent);
                training.ClearOptions();
                training.AddOptions(new[] { "Balanced", "Recovery", "Intensive" }.Select(value => GameText.Get("training." + value)).ToList());
                training.SetValueWithoutNotify((int)progress.Training); training.RefreshShownValue();
                news.text = session.CareerNewsText(); ledger.text = session.CareerLedgerText();
                advance.interactable = next.interactable = progress.CanAdvance && !session.IsBusy;
                play.interactable = simulate.interactable = session.CareerCanPlay;
                competition.interactable = newCareer.interactable = !session.IsBusy;
                training.interactable = progress.CanAdvance && !session.IsBusy;
            }
            finally { refreshing = false; }
        }
    }
}
