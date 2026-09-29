using System;
using FStudio.FootballWorld.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Presentation
{
    public sealed class HubCareerPlayerRow : MonoBehaviour
    {
        [SerializeField] private Button select;
        [SerializeField] private TMP_Text playerName, description;
        [SerializeField] private Image background;
        [SerializeField] private GameHubTheme theme;
        private Action selected;

        private void OnEnable() { if (UnityEngine.Application.isPlaying) select.onClick.AddListener(Select); }
        private void OnDisable() { if (select != null) select.onClick.RemoveListener(Select); }
        private void Select() => selected?.Invoke();

        public void Bind(PlayerDefinition player, string club, bool active, Action action)
        {
            playerName.text = (active ? "●  " : string.Empty) + player.DisplayName;
            description.text = string.Join(" / ", player.NaturalPositions) + "  ·  " + club;
            background.color = active ? theme.Accent : theme.White;
            selected = action;
        }
    }
}
