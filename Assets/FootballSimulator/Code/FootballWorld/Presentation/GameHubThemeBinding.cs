using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Presentation
{
    public enum GameHubColorRole { Background, Surface, White, Ink, Muted, Primary, Accent, Line, Danger,
        InspectionPanel, InspectionRow, InspectionText, InspectionMuted, PitchSurface, PitchMarking }

    // Explicit Editor application preserves authored layout/text. No runtime layout/theme reconstruction occurs.
    public sealed class GameHubThemeBinding : MonoBehaviour
    {
        public GameHubTheme Theme;
        public GameHubColorRole Role;
        public bool UseFont;

        [ContextMenu("Apply theme to this component")]
        public void Apply()
        {
            if (Theme == null) return;
            var graphic = GetComponent<Graphic>();
            if (graphic == null) return;
            switch (Role)
            {
                case GameHubColorRole.Background: graphic.color = Theme.Background; break;
                case GameHubColorRole.Surface: graphic.color = Theme.Surface; break;
                case GameHubColorRole.White: graphic.color = Theme.White; break;
                case GameHubColorRole.Ink: graphic.color = Theme.Ink; break;
                case GameHubColorRole.Muted: graphic.color = Theme.Muted; break;
                case GameHubColorRole.Primary: graphic.color = Theme.Primary; break;
                case GameHubColorRole.Accent: graphic.color = Theme.Accent; break;
                case GameHubColorRole.Line: graphic.color = Theme.Line; break;
                case GameHubColorRole.Danger: graphic.color = Theme.Danger; break;
                case GameHubColorRole.InspectionPanel: graphic.color = Theme.InspectionPanel; break;
                case GameHubColorRole.InspectionRow: graphic.color = Theme.InspectionRow; break;
                case GameHubColorRole.InspectionText: graphic.color = Theme.InspectionText; break;
                case GameHubColorRole.InspectionMuted: graphic.color = Theme.InspectionMuted; break;
                case GameHubColorRole.PitchSurface: graphic.color = Theme.PitchSurface; break;
                case GameHubColorRole.PitchMarking: graphic.color = Theme.PitchMarking; break;
            }
            if (UseFont && graphic is TMP_Text text) text.font = Theme.Font;
            var selectable = GetComponent<Selectable>();
            if (selectable == null) return;
            var colors = selectable.colors;
            colors.normalColor = Color.white; colors.highlightedColor = Theme.Hover; colors.selectedColor = Theme.Selected;
            colors.pressedColor = Theme.Pressed; colors.disabledColor = Theme.Disabled;
            selectable.colors = colors;
        }
    }
}
