using System;
using System.Linq;
using FStudio.FootballWorld.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Presentation
{
    public sealed class CareerNaturalPositionMap : MonoBehaviour
    {
        [SerializeField] private GameHubTheme theme;
        [SerializeField] private CareerPositionMarker[] positions = Array.Empty<CareerPositionMarker>();
        public void Bind(PlayerDefinition player)
        {
            if (theme == null) return;
            foreach (var position in positions)
            {
                var natural = player != null && player.NaturalPositions.Contains(position.Position);
                if (position.Marker != null) position.Marker.color = natural ? theme.Accent : theme.InspectionRow;
                if (position.Label != null)
                {
                    position.Label.color = natural ? theme.Ink : theme.InspectionMuted;
                    position.Label.fontStyle = natural ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }
    }
    [Serializable]
    public sealed class CareerPositionMarker
    {
        public PlayerPosition Position;
        public Image Marker;
        public TMP_Text Label;
    }
}
