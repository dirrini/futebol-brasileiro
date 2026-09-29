using System;
using System.Collections.Generic;
using FStudio.FootballWorld.Infrastructure.GameModes;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using FStudio.UI;
using TMPro;
using UnityEngine;

namespace FStudio.FootballWorld.Presentation
{
    // Uses the same legacy arrow controls as the adjacent team card, including gamepad snapping.
    public sealed class TeamCountrySelection : MonoBehaviour
    {
        [SerializeField] private TMP_Text countryName;
        [SerializeField] private InteractiveUIElement previousButton, nextButton;
        [SerializeField, Range(0f, 1f)] private float disabledNavigationAlpha = .4f;
        private IReadOnlyList<CatalogCountryOption> options = Array.Empty<CatalogCountryOption>();
        private int selected;
        private bool canSelect;
        public event Action<string> SelectionChanged;

        public void Bind(IReadOnlyList<CatalogCountryOption> countries, string selectedCode, bool interactable)
        {
            options = countries ?? Array.Empty<CatalogCountryOption>();
            selected = -1;
            for (var index = 0; index < options.Count; index++)
                if (options[index].Code == selectedCode) { selected = index; break; }
            countryName.richText = false;
            countryName.text = selected < 0 ? "—" : GameText.CountryName(options[selected].Code, options[selected].Name);
            canSelect = interactable && options.Count > 1;
            UpdateNavigation();
        }

        public void Next() { Move(1); }
        public void Previous() { Move(-1); }
        private void Move(int delta)
        {
            if (!canSelect) return;
            selected = (selected + delta + options.Count) % options.Count;
            SelectionChanged?.Invoke(options[selected].Code);
        }
        private void OnEnable()
        {
            if (previousButton != null) previousButton.onAppeared.AddListener(UpdateNavigation);
            if (nextButton != null) nextButton.onAppeared.AddListener(UpdateNavigation);
        }
        private void OnDisable()
        {
            if (previousButton != null) previousButton.onAppeared.RemoveListener(UpdateNavigation);
            if (nextButton != null) nextButton.onAppeared.RemoveListener(UpdateNavigation);
        }
        private void UpdateNavigation()
        {
            SetButton(previousButton); SetButton(nextButton);
        }
        private void SetButton(InteractiveUIElement button)
        {
            if (button == null) return;
            button.isInteractionEverEnabled = canSelect;
            button.IsInteractable = canSelect;
            button.SetRaycast(canSelect);
            var group = button.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = canSelect ? 1 : disabledNavigationAlpha;
        }
    }
}
