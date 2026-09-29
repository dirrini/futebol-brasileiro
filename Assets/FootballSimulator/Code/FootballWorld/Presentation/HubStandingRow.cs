using FStudio.FootballWorld.Infrastructure.GameModes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Presentation
{
    public sealed class HubStandingRow : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text rank, club, played, won, drawn, lost, difference, points;
        [SerializeField] private GameHubTheme theme;

        public void Bind(HubStandingView value, bool isUser)
        {
            rank.text = value.Rank.ToString();
            club.text = value.GroupName == null ? value.ClubName : value.GroupName + " · " + value.ClubName;
            played.text = value.Played.ToString();
            won.text = value.Won.ToString();
            drawn.text = value.Drawn.ToString();
            lost.text = value.Lost.ToString();
            difference.text = value.GoalDifference.ToString();
            points.text = value.Points.ToString();
            background.color = isUser ? theme.Accent : theme.White;
            club.fontStyle = isUser ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
