using System;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Infrastructure.GameModes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Presentation
{
    public sealed class GameHubCareerTactics : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown formation, mentality;
        [SerializeField] private Button apply;
        [SerializeField] private TMP_Text selectedSummary, startingLineup, feedback;
        private readonly string[] formationIds = { "4-4-2", "4-3-3", "4-2-3-1" };
        private readonly string[] mentalityIds = { "Defensive", "Balanced", "Attacking" };
        private GameHubSession session;
        private CareerSession lastCareer;
        private int formationIndex, mentalityIndex;
        private bool refreshing, dirty;
        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying) return;
            session = GameHubSession.Current;
            session.Changed += Refresh; GameText.Changed += Refresh;
            formation.onValueChanged.AddListener(FormationChanged); mentality.onValueChanged.AddListener(MentalityChanged); apply.onClick.AddListener(Apply);
            Refresh();
        }
        private void OnDisable()
        {
            if (session == null) return;
            session.Changed -= Refresh; GameText.Changed -= Refresh;
            formation.onValueChanged.RemoveListener(FormationChanged); mentality.onValueChanged.RemoveListener(MentalityChanged); apply.onClick.RemoveListener(Apply);
            session = null;
        }
        private void FormationChanged(int value) { if (!refreshing) { formationIndex = value; dirty = true; Refresh(); } }
        private void MentalityChanged(int value) { if (!refreshing) { mentalityIndex = value; dirty = true; Refresh(); } }
        private void Apply()
        {
            if (session.SetCareerTactics(formationIds[formationIndex], mentalityIds[mentalityIndex])) { dirty = false; Refresh(); }
        }
        private void Refresh()
        {
            var career = session?.CareerProgress;
            if (career == null || refreshing) return;
            refreshing = true;
            try
            {
                if (!ReferenceEquals(lastCareer, career)) { lastCareer = career; dirty = false; }
                if (!dirty) { formationIndex = (int)career.Formation; mentalityIndex = (int)career.Mentality; }
                formation.ClearOptions(); formation.AddOptions(formationIds.ToList()); formation.SetValueWithoutNotify(formationIndex); formation.RefreshShownValue();
                mentality.ClearOptions(); mentality.AddOptions(mentalityIds.Select(value => GameText.Get("mentality." + value)).ToList()); mentality.SetValueWithoutNotify(mentalityIndex); mentality.RefreshShownValue();
                selectedSummary.text = GameText.Get("career.savedTactics", formationIds[(int)career.Formation], GameText.Get("mentality." + mentalityIds[(int)career.Mentality]));
                startingLineup.text = string.Join("\n", session.GetCareerStartingLineup().Select((player, index) => (index + 1).ToString("00") + "   " + player.DisplayName + "   ·   " + string.Join(" / ", player.NaturalPositions)));
                var ended = career.CurrentDate.CompareTo(career.EndDate) >= 0;
                feedback.text = GameText.Get(ended ? "career.managementEnded" : dirty ? "career.tacticsDraft" : "career.tacticsCurrent");
                formation.interactable = mentality.interactable = !ended && !session.IsBusy;
                apply.interactable = dirty && !ended && !session.IsBusy;
            }
            finally { refreshing = false; }
        }
    }
}
