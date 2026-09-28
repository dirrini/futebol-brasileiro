using System;
using System.Collections.Generic;
using FStudio.Database;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using FStudio.UI.Graphics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FStudio.UI.Utilities {
    public class TeamSelectionTeam : MonoBehaviour {
        [SerializeField] private Selector selector;
        [SerializeField] private TextMeshProUGUI teamNameText;
        [SerializeField] private TextMeshProUGUI overallText;
        [SerializeField] private Image teamLogoImage;
        [SerializeField] private ImageFiller overallFiller;
        [SerializeField] private InteractiveUIElement previousButton, nextButton;
        [SerializeField, Range(0f, 1f)] private float disabledNavigationAlpha = 0.4f;

        private IReadOnlyList<CatalogTeamOption> teams = Array.Empty<CatalogTeamOption>();
        private bool canSelect;
        private bool initialized;
        private LogoEntry currentLogo;
        private Material logoMaterial;

        public string SelectedClubId { get; private set; }
        public event Action<string> SelectionChanged;

        private void Awake() {
            Initialize();
        }

        private void Initialize() {
            if (initialized) return;
            initialized = true;
            selector.OnSelectionUpdate += OnSelectionChanged;
            teamNameText.richText = false;
            if (previousButton != null) previousButton.onAppeared.AddListener(UpdateNavigation);
            if (nextButton != null) nextButton.onAppeared.AddListener(UpdateNavigation);
        }

        public void Bind(IReadOnlyList<CatalogTeamOption> options, string selectedClubId, bool interactable) {
            Initialize();
            teams = options ?? Array.Empty<CatalogTeamOption>();
            selector.Max = teams.Count;
            canSelect = interactable && teams.Count > 1;
            var selected = -1;
            for (var i = 0; i < teams.Count; i++) {
                if (string.Equals(teams[i].ClubId, selectedClubId, StringComparison.Ordinal)) {
                    selected = i;
                    break;
                }
            }
            selector.SetSelectedSilent(Mathf.Max(0, selected));
            ShowTeam(selected >= 0 ? teams[selected] : null);
            UpdateNavigation();
        }

        public void SetTeam(int teamIndex) {
            if (!canSelect || teamIndex < 0 || teamIndex >= teams.Count) return;
            selector.SetSelected(teamIndex);
        }

        private void OnSelectionChanged(int index) {
            if (!canSelect || index < 0 || index >= teams.Count) return;
            var option = teams[index];
            ShowTeam(option);
            SelectionChanged?.Invoke(option.ClubId);
        }

        private void ShowTeam(CatalogTeamOption option) {
            SelectedClubId = option?.ClubId;
            teamNameText.text = option?.Name ?? "—";
            SetLogo(option?.Logo);

            if (option?.Preview == null) {
                overallText.text = "—";
                overallFiller.Image.fillAmount = 0;
                overallFiller.FillTo(0);
                return;
            }

            var overall = option.Preview.Overall;
            var clamped = Mathf.Clamp(overall, 60, 84);
            var fill = (clamped - 60) / 3;
            overallFiller.FillTo(0.21f + fill * 0.1f);
            overallText.text = overall.ToString();
        }

        private void SetLogo(LogoEntry logo) {
            teamLogoImage.enabled = logo != null;
            if (currentLogo == logo) return;
            currentLogo = logo;
            teamLogoImage.material = null;
            if (logoMaterial != null) Destroy(logoMaterial);
            logoMaterial = logo != null ? TeamLogoMaterial.Current.GetColoredMaterial(logo) : null;
            teamLogoImage.material = logoMaterial;
        }

        private void UpdateNavigation() {
            SetNavigation(previousButton);
            SetNavigation(nextButton);
        }

        private void SetNavigation(InteractiveUIElement button) {
            if (button == null) return;
            // The additional gate survives appear animations resetting IsInteractable.
            button.isInteractionEverEnabled = canSelect;
            button.IsInteractable = canSelect;
            button.SetRaycast(canSelect);
            var group = button.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = canSelect ? 1f : disabledNavigationAlpha;
        }

        private void OnDestroy() {
            if (selector != null) selector.OnSelectionUpdate -= OnSelectionChanged;
            if (previousButton != null) previousButton.onAppeared.RemoveListener(UpdateNavigation);
            if (nextButton != null) nextButton.onAppeared.RemoveListener(UpdateNavigation);
            if (logoMaterial != null) Destroy(logoMaterial);
        }
    }
}
