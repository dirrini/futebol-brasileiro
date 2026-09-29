using FStudio.FootballWorld.Infrastructure.GameModes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Presentation
{
    public sealed class HubFixtureRow : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text date, home, score, away;
        [SerializeField] private GameHubTheme theme;

        public void Bind(HubFixtureView value)
        {
            date.text = GameText.FormatDate(value.Date) + " · " + GameText.Get("hub.round", value.Round);
            if (value.IsCompleted) date.text += " · " + GameText.Get(value.IsSimulated ? "hub.simulated" : "hub.played");
            home.text = value.HomeName;
            away.text = value.AwayName;
            score.text = value.IsCompleted ? value.HomeGoals + " – " + value.AwayGoals : "×";
            background.color = value.IsUserFixture ? theme.Accent : theme.White;
        }
    }
}
