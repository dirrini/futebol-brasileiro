using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.GameModes;
using TMPro;
using UnityEngine;

namespace FStudio.FootballWorld.Presentation
{
    public sealed class GameHubCareerPlayerDetails : MonoBehaviour
    {
        [SerializeField] private TMP_Text playerName, club, biography;
        [SerializeField] private TMP_Text[] attributeValues;
        [SerializeField] private GameHubTheme theme;
        [SerializeField] private CareerNaturalPositionMap positionMap;

        public void Bind(PlayerDefinition player, string clubName)
        {
            playerName.text = player == null ? GameText.Get("career.selectPlayer") : player.DisplayName;
            club.text = player == null ? string.Empty : clubName + "  ·  " + string.Join(" / ", player.NaturalPositions);
            biography.text = player == null ? string.Empty : (player.FullName ?? player.Name) + "\n"
                + GameText.Get("career.playerBio", player.BirthDate.HasValue ? GameText.FormatDate(player.BirthDate.Value.ToDateTime()) : "—",
                    GameText.Get("foot." + (player.PreferredFoot ?? "unknown")), player.HeightCm, player.WeightKg,
                    player.NationalityCode ?? "—");
            var a = player?.Attributes;
            var values = a == null ? null : new[] { a.Strength, a.Acceleration, a.TopSpeed, a.DribbleSpeed, a.Jump,
                a.Tackling, a.BallKeeping, a.Passing, a.LongBall, a.Agility, a.Shooting, a.ShootPower, a.Positioning, a.Reaction, a.BallControl };
            for (var index = 0; index < attributeValues.Length; index++)
            {
                if (attributeValues[index] == null) continue;
                attributeValues[index].text = values == null || index >= values.Length ? "—" : values[index].ToString(GameText.Culture);
                if (theme != null) attributeValues[index].color = values == null || index >= values.Length
                    ? theme.InspectionMuted : theme.AttributeColor.Evaluate(Mathf.Clamp01(values[index] / 100f));
            }
            if (positionMap != null) positionMap.Bind(player);
        }
    }
}
