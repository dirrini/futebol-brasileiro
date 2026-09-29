using System;
using TMPro;
using UnityEngine;

namespace FStudio.FootballWorld.Presentation
{
    [CreateAssetMenu(menuName = "Football World/Game hub theme")]
    public sealed class GameHubTheme : ScriptableObject
    {
        public TMP_FontAsset Font;
        public Sprite RoundedPanel;
        public Sprite Circle;
        public Color Background = new Color32(24, 61, 71, 255);
        public Color Surface = new Color32(237, 242, 241, 255);
        public Color White = Color.white;
        public Color Ink = new Color32(23, 59, 70, 255);
        public Color Muted = new Color32(91, 118, 123, 255);
        public Color Primary = new Color32(35, 115, 90, 255);
        public Color Accent = new Color32(191, 224, 201, 255);
        public Color Line = new Color32(194, 214, 208, 255);
        public Color Danger = new Color32(170, 51, 65, 255);
        public Color Hover = new Color32(217, 242, 227, 255);
        public Color Selected = new Color32(187, 220, 203, 255);
        public Color Pressed = new Color32(159, 202, 180, 255);
        public Color Disabled = new Color(.65f, .65f, .65f, .6f);
        [Header("Career inspection panels")]
        public Color InspectionPanel = new Color32(16, 39, 51, 255);
        public Color InspectionRow = new Color32(29, 54, 64, 255);
        public Color InspectionText = new Color32(240, 246, 244, 255);
        public Color InspectionMuted = new Color32(174, 197, 197, 255);
        public Color PitchSurface = new Color32(37, 91, 71, 255);
        public Color PitchMarking = new Color32(112, 168, 139, 255);
        public Gradient AttributeColor = new Gradient {
            colorKeys = new[] { new GradientColorKey(new Color32(191, 207, 212, 255), 0),
                new GradientColorKey(new Color32(234, 206, 114, 255), .5f), new GradientColorKey(new Color32(120, 231, 161, 255), 1) },
            alphaKeys = new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) }
        };
        public CoachPortrait[] Portraits = new CoachPortrait[0];
    }

    [Serializable]
    public sealed class CoachPortrait
    {
        public string Id;
        public string LabelKey;
        public Color Skin;
        public Color Hair;
        public Color Shirt;
        public bool HasHair = true;
    }
}
