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
        [SerializeField] private CareerTacticalBoard tacticalBoard;
        private readonly string[] formationIds = { "4-4-2", "4-3-3", "4-2-3-1" };
        private readonly string[] mentalityIds = { "Defensive", "Balanced", "Attacking" };
        private GameHubSession session;
        private CareerSession lastCareer;
        private CareerTacticPlan draftPlan;
        private int formationIndex, mentalityIndex;
        private bool refreshing, dirty;
        private bool CanEdit => session?.CareerProgress != null && !session.IsBusy
            && session.CareerProgress.Competition.ActiveExecution == null
            && session.CareerProgress.CurrentDate.CompareTo(session.CareerProgress.EndDate) < 0;
        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying) return;
            session = GameHubSession.Current;
            session.Changed += Refresh; GameText.Changed += Refresh;
            formation.onValueChanged.AddListener(FormationChanged); mentality.onValueChanged.AddListener(MentalityChanged); apply.onClick.AddListener(Apply);
            if (tacticalBoard != null) tacticalBoard.DraftChanged += TacticDraftChanged;
            Refresh();
        }
        private void OnDisable()
        {
            if (session == null) return;
            session.Changed -= Refresh; GameText.Changed -= Refresh;
            formation.onValueChanged.RemoveListener(FormationChanged); mentality.onValueChanged.RemoveListener(MentalityChanged); apply.onClick.RemoveListener(Apply);
            if (tacticalBoard != null) tacticalBoard.DraftChanged -= TacticDraftChanged;
            session = null;
        }
        private void FormationChanged(int value)
        {
            if (refreshing || !CanEdit || value == formationIndex) return;
            formationIndex = value; draftPlan = CareerTacticPlan.CreateDefault((CareerFormation)value);
            UpdateDirty(); Refresh();
        }
        private void MentalityChanged(int value) { if (!refreshing && CanEdit) { mentalityIndex = value; UpdateDirty(); Refresh(); } }
        private void TacticDraftChanged(CareerTacticPlan plan)
        {
            if (refreshing || !CanEdit) return;
            draftPlan = plan; UpdateDirty(); RefreshFeedback();
        }
        private void Apply()
        {
            if (session.SetCareerTactics(formationIds[formationIndex], mentalityIds[mentalityIndex], draftPlan)) { dirty = false; Refresh(); }
        }
        private void UpdateDirty()
        {
            var career = session?.CareerProgress;
            if (career == null || draftPlan == null) return;
            dirty = formationIndex != (int)career.Formation || mentalityIndex != (int)career.Mentality || !draftPlan.SameConfiguration(career.TacticPlan);
        }
        private void RefreshFeedback()
        {
            var career = session?.CareerProgress;
            if (career == null) return;
            var ended = career.CurrentDate.CompareTo(career.EndDate) >= 0;
            feedback.text = GameText.Get(ended ? "career.managementEnded" : dirty ? "career.tacticsDraft" : "career.tacticsCurrent");
            formation.interactable = mentality.interactable = CanEdit;
            apply.interactable = dirty && CanEdit;
        }
        private void Refresh()
        {
            var career = session?.CareerProgress;
            if (career == null || refreshing) return;
            refreshing = true;
            try
            {
                if (!ReferenceEquals(lastCareer, career)) { lastCareer = career; dirty = false; }
                if (!dirty) { formationIndex = (int)career.Formation; mentalityIndex = (int)career.Mentality; draftPlan = career.TacticPlan; }
                formation.ClearOptions(); formation.AddOptions(formationIds.ToList()); formation.SetValueWithoutNotify(formationIndex); formation.RefreshShownValue();
                mentality.ClearOptions(); mentality.AddOptions(mentalityIds.Select(value => GameText.Get("mentality." + value)).ToList()); mentality.SetValueWithoutNotify(mentalityIndex); mentality.RefreshShownValue();
                selectedSummary.text = GameText.Get("career.savedTactics", formationIds[(int)career.Formation], GameText.Get("mentality." + mentalityIds[(int)career.Mentality]));
                var preview = session.GetCareerLineupPreview((CareerFormation)formationIndex);
                if (tacticalBoard != null) tacticalBoard.Bind(draftPlan, preview, CanEdit);
                if (startingLineup != null && startingLineup.gameObject.activeInHierarchy)
                    startingLineup.text = string.Join("\n", preview.Select((player, index) => (index + 1).ToString("00") + "   " + player.DisplayName + "   ·   " + string.Join(" / ", player.NaturalPositions)));
                RefreshFeedback();
            }
            finally { refreshing = false; }
        }
    }
}
