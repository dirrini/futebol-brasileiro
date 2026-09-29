using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.GameModes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Presentation
{
    // Geometry and each formation's eleven slots are authored in GameHub.prefab.
    // Slot input produces an immutable draft; only the page's Save action persists it.
    public sealed class CareerTacticalBoard : MonoBehaviour
    {
        [SerializeField] private CareerTacticalLayout[] layouts = Array.Empty<CareerTacticalLayout>();
        [SerializeField] private TMP_Text[] lineupNames = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] lineupPositions = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text emptyState;
        [SerializeField] private GameObject emptyPanel;
        [SerializeField] private RectTransform pitch;
        [SerializeField] private TMP_Dropdown role;
        [SerializeField] private TMP_Text selectedSlotLabel, roleEffect;
        [SerializeField] private Button resetSlot, resetAll;
        [SerializeField] private Button[] lineupButtons = Array.Empty<Button>();
        [SerializeField] private Outline[] lineupHighlights = Array.Empty<Outline>();
        [SerializeField] private Vector2 markerAnchorOffset = new Vector2(62, 26);
        private CareerTacticPlan plan;
        private CareerTacticalLayout selected;
        private int selectedSlot;
        private bool editable, binding;
        private UnityAction[] lineupActions;
        public event Action<CareerTacticPlan> DraftChanged;

        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying) return;
            role?.onValueChanged.AddListener(ChangeRole);
            resetSlot?.onClick.AddListener(ResetSelected);
            resetAll?.onClick.AddListener(ResetFormation);
            lineupActions = new UnityAction[lineupButtons.Length];
            for (var i = 0; i < lineupButtons.Length; i++)
            {
                var index = i; lineupActions[i] = () => FocusSlot(index);
                lineupButtons[i]?.onClick.AddListener(lineupActions[i]);
            }
        }

        private void OnDisable()
        {
            role?.onValueChanged.RemoveListener(ChangeRole);
            resetSlot?.onClick.RemoveListener(ResetSelected);
            resetAll?.onClick.RemoveListener(ResetFormation);
            if (lineupActions == null) return;
            for (var i = 0; i < lineupButtons.Length && i < lineupActions.Length; i++)
                lineupButtons[i]?.onClick.RemoveListener(lineupActions[i]);
        }

        public void Bind(CareerTacticPlan draft, IReadOnlyList<PlayerDefinition> players, bool canEdit)
        {
            if (draft == null) return;
            plan = draft; editable = canEdit; selected = null;
            foreach (var layout in layouts)
            {
                var active = layout.Formation == draft.Formation;
                if (layout.Root != null) layout.Root.SetActive(active);
                if (active) selected = layout;
            }
            var complete = players != null && players.Count == 11;
            if (emptyPanel != null) emptyPanel.SetActive(!complete);
            if (emptyState != null) emptyState.gameObject.SetActive(!complete);
            for (var index = 0; index < 11; index++)
            {
                var name = complete ? players[index].DisplayName : "—";
                if (index < lineupNames.Length && lineupNames[index] != null) lineupNames[index].text = name;
                if (index < lineupPositions.Length && lineupPositions[index] != null)
                    lineupPositions[index].text = selected != null && index < selected.SlotPositions.Length
                        ? selected.SlotPositions[index].ToString() : "—";
                if (selected != null && index < selected.PlayerNames.Length && selected.PlayerNames[index] != null)
                    selected.PlayerNames[index].text = name;
                if (index < lineupButtons.Length && lineupButtons[index] != null) lineupButtons[index].interactable = canEdit;
                if (selected != null && index < selected.Markers.Length && selected.Markers[index] != null)
                {
                    selected.Markers[index].interactable = canEdit;
                    PlaceMarker(index);
                }
            }
            RefreshSelection();
        }

        public void SelectSlot(int index)
        {
            if (plan == null || index < 0 || index >= 11) return;
            selectedSlot = index; RefreshSelection();
        }

        private void FocusSlot(int index)
        {
            SelectSlot(index);
            if (selected != null && index < selected.Markers.Length && selected.Markers[index] != null)
                EventSystem.current?.SetSelectedGameObject(selected.Markers[index].gameObject);
        }

        public void FocusRoleSelector()
        {
            if (role != null && role.interactable) EventSystem.current?.SetSelectedGameObject(role.gameObject);
            else if (resetSlot != null && resetSlot.interactable) EventSystem.current?.SetSelectedGameObject(resetSlot.gameObject);
        }

        public bool TryGetPointerPosition(PointerEventData eventData, out Vector2 position)
        {
            position = Vector2.zero;
            if (pitch == null || pitch.rect.width <= 0 || pitch.rect.height <= 0 ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(pitch, eventData.position, eventData.pressEventCamera, out var point)) return false;
            position = new Vector2((point.x - pitch.rect.xMin) / pitch.rect.width, (pitch.rect.yMax - point.y) / pitch.rect.height);
            return true;
        }

        public Vector2 GetSlotPosition(int index)
        {
            if (plan == null || index < 0 || index >= 11) return Vector2.zero;
            var slot = plan.GetSlot(CareerTacticPlan.GetSlotDefinitions(plan.Formation)[index].SlotId);
            return new Vector2(slot.X, 1 - slot.Depth);
        }

        public void MoveSlot(int index, Vector2 position)
        {
            if (!editable || plan == null || index < 0 || index >= 11) return;
            var definition = CareerTacticPlan.GetSlotDefinitions(plan.Formation)[index];
            var slot = plan.GetSlot(definition.SlotId);
            var x = Mathf.Clamp(position.x, definition.MinX, definition.MaxX);
            var depth = Mathf.Clamp(1 - position.y, definition.MinDepth, definition.MaxDepth);
            if (Mathf.Approximately(x, slot.X) && Mathf.Approximately(depth, slot.Depth)) return;
            plan = plan.WithSlot(slot.SlotId, x, depth, slot.Role);
            PlaceMarker(index); DraftChanged?.Invoke(plan);
        }

        private void PlaceMarker(int index)
        {
            if (pitch == null || selected == null || index >= selected.Markers.Length || selected.Markers[index] == null) return;
            var position = GetSlotPosition(index);
            selected.Markers[index].GetComponent<RectTransform>().anchoredPosition =
                new Vector2(position.x * pitch.rect.width - markerAnchorOffset.x, -(position.y * pitch.rect.height - markerAnchorOffset.y));
        }

        private void RefreshSelection()
        {
            if (plan == null) return;
            binding = true;
            try
            {
                var definition = CareerTacticPlan.GetSlotDefinitions(plan.Formation)[selectedSlot];
                var slot = plan.GetSlot(definition.SlotId);
                for (var i = 0; i < lineupHighlights.Length; i++) if (lineupHighlights[i] != null) lineupHighlights[i].enabled = i == selectedSlot;
                foreach (var layout in layouts)
                    for (var i = 0; i < layout.SelectionOutlines.Length; i++)
                        if (layout.SelectionOutlines[i] != null) layout.SelectionOutlines[i].enabled = layout == selected && i == selectedSlot;
                if (selectedSlotLabel != null) selectedSlotLabel.text = GameText.Get("career.selectedTacticSlot", (selectedSlot + 1).ToString("00"), definition.Position.ToString());
                if (role != null)
                {
                    role.ClearOptions(); role.AddOptions(definition.AllowedRoles.Select(value => GameText.Get("career.role." + value)).ToList());
                    role.SetValueWithoutNotify(Array.IndexOf(definition.AllowedRoles.ToArray(), slot.Role)); role.RefreshShownValue();
                    role.interactable = editable && definition.AllowedRoles.Count > 1;
                }
                if (roleEffect != null) roleEffect.text = GameText.Get("career.roleEffect." + slot.Role);
                if (resetSlot != null) resetSlot.interactable = editable;
                if (resetAll != null) resetAll.interactable = editable;
            }
            finally { binding = false; }
        }

        private void ChangeRole(int index)
        {
            if (binding || !editable || plan == null) return;
            var definition = CareerTacticPlan.GetSlotDefinitions(plan.Formation)[selectedSlot];
            if (index < 0 || index >= definition.AllowedRoles.Count) return;
            var slot = plan.GetSlot(definition.SlotId);
            plan = plan.WithSlot(slot.SlotId, slot.X, slot.Depth, definition.AllowedRoles[index]);
            RefreshSelection(); DraftChanged?.Invoke(plan);
        }

        private void ResetSelected()
        {
            if (!editable || plan == null) return;
            var definition = CareerTacticPlan.GetSlotDefinitions(plan.Formation)[selectedSlot];
            plan = plan.WithSlot(definition.SlotId, definition.DefaultX, definition.DefaultDepth, CareerPlayerRole.Standard);
            PlaceMarker(selectedSlot); RefreshSelection(); DraftChanged?.Invoke(plan);
        }

        private void ResetFormation()
        {
            if (!editable || plan == null) return;
            plan = CareerTacticPlan.CreateDefault(plan.Formation);
            for (var i = 0; i < 11; i++) PlaceMarker(i);
            RefreshSelection(); DraftChanged?.Invoke(plan);
        }
    }

    [Serializable]
    public sealed class CareerTacticalLayout
    {
        public CareerFormation Formation;
        public GameObject Root;
        public PlayerPosition[] SlotPositions = Array.Empty<PlayerPosition>();
        public TMP_Text[] PlayerNames = Array.Empty<TMP_Text>();
        public CareerTacticalSlot[] Markers = Array.Empty<CareerTacticalSlot>();
        public Outline[] SelectionOutlines = Array.Empty<Outline>();
    }
}
