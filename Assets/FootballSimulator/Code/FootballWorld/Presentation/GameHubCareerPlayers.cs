using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.GameModes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Presentation
{
    // Filtering and selection are transient presentation state. The career remains the roster authority.
    public abstract class GameHubCareerPlayers : MonoBehaviour
    {
        [SerializeField] private TMP_InputField search;
        [SerializeField] private TMP_Dropdown positionFilter, clubFilter;
        [SerializeField] private Button clear, previous, next;
        [SerializeField] private TMP_Text resultCount, pageCount, emptyState;
        [SerializeField] private ScrollRect playerScroll;
        [SerializeField] private HubCareerPlayerRow rowTemplate;
        [SerializeField] private GameHubCareerPlayerDetails details;
        [SerializeField, Min(1)] private int pageSize = 10;
        protected GameHubSession Session { get; private set; }
        protected PlayerDefinition SelectedPlayer { get; private set; }
        private readonly List<HubCareerPlayerRow> rows = new List<HubCareerPlayerRow>();
        private readonly List<string> clubIds = new List<string>();
        private CareerSession lastCareer;
        private string selectedId, selectedClub;
        private int selectedPosition, page;
        private bool refreshing;
        protected abstract IEnumerable<PlayerDefinition> SourcePlayers(CareerSession career);

        protected virtual void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying) return;
            Session = GameHubSession.Current;
            Session.Changed += Refresh; GameText.Changed += Refresh;
            search.onValueChanged.AddListener(SearchChanged); positionFilter.onValueChanged.AddListener(PositionChanged);
            if (clubFilter != null) clubFilter.onValueChanged.AddListener(ClubChanged);
            clear.onClick.AddListener(Clear); previous.onClick.AddListener(Previous); next.onClick.AddListener(Next);
            Refresh();
        }
        protected virtual void OnDisable()
        {
            if (Session == null) return;
            Session.Changed -= Refresh; GameText.Changed -= Refresh;
            search.onValueChanged.RemoveListener(SearchChanged); positionFilter.onValueChanged.RemoveListener(PositionChanged);
            if (clubFilter != null) clubFilter.onValueChanged.RemoveListener(ClubChanged);
            clear.onClick.RemoveListener(Clear); previous.onClick.RemoveListener(Previous); next.onClick.RemoveListener(Next);
            Session = null;
        }
        private void SearchChanged(string value) { if (!refreshing) { page = 0; Refresh(); playerScroll.verticalNormalizedPosition = 1; } }
        private void PositionChanged(int value) { if (!refreshing) { selectedPosition = value; page = 0; Refresh(); } }
        private void ClubChanged(int value) { if (!refreshing && value >= 0 && value < clubIds.Count) { selectedClub = clubIds[value]; page = 0; Refresh(); } }
        private void Clear()
        {
            search.SetTextWithoutNotify(string.Empty); selectedPosition = 0; selectedClub = null; page = 0; Refresh();
            playerScroll.verticalNormalizedPosition = 1; search.Select();
        }
        private void Previous() { page--; Refresh(); playerScroll.verticalNormalizedPosition = 1; }
        private void Next() { page++; Refresh(); playerScroll.verticalNormalizedPosition = 1; }
        protected string ClubName(PlayerDefinition player)
        {
            if (player == null || Session?.CareerProgress == null) return string.Empty;
            var id = Session.CareerProgress.GetCurrentClubId(player.Id);
            return id == null ? GameText.Get("career.freeAgent") : Session.CareerProgress.EffectiveCatalog.GetClub(id).Name;
        }
        protected void Refresh()
        {
            var career = Session?.CareerProgress;
            if (career == null || refreshing) return;
            refreshing = true;
            try
            {
                if (!ReferenceEquals(lastCareer, career))
                {
                    lastCareer = career; selectedId = selectedClub = null; selectedPosition = page = 0;
                    search.SetTextWithoutNotify(string.Empty);
                }
                positionFilter.ClearOptions();
                positionFilter.AddOptions(new[] { GameText.Get("career.allPositions") }.Concat(Enum.GetNames(typeof(PlayerPosition)).Select(value => GameText.Get("position." + value))).ToList());
                positionFilter.SetValueWithoutNotify(selectedPosition); positionFilter.RefreshShownValue();
                if (clubFilter != null)
                {
                    var clubs = career.EffectiveCatalog.Clubs.OrderBy(item => item.Name, StringComparer.Create(GameText.Culture, true)).ToArray();
                    clubIds.Clear(); clubIds.Add(null); clubIds.Add(string.Empty); clubIds.AddRange(clubs.Select(item => item.Id));
                    if (!clubIds.Contains(selectedClub)) selectedClub = null;
                    clubFilter.ClearOptions(); clubFilter.AddOptions(new[] { GameText.Get("career.allClubs"), GameText.Get("career.freeAgent") }.Concat(clubs.Select(item => item.Name)).ToList());
                    clubFilter.SetValueWithoutNotify(clubIds.IndexOf(selectedClub)); clubFilter.RefreshShownValue();
                }
                var query = search.text.Trim();
                var players = SourcePlayers(career).Where(player => (selectedPosition == 0 || player.NaturalPositions.Contains((PlayerPosition)(selectedPosition - 1)))
                    && (selectedClub == null || (career.GetCurrentClubId(player.Id) ?? string.Empty) == selectedClub)
                    && (query.Length == 0 || Matches(player.DisplayName, query) || Matches(player.Name, query) || Matches(player.FullName, query)))
                    .OrderBy(player => player.DisplayName, StringComparer.Create(GameText.Culture, true)).ThenBy(player => player.Id, StringComparer.Ordinal).ToArray();
                var count = Math.Max(1, (players.Length + pageSize - 1) / pageSize);
                page = Math.Max(0, Math.Min(page, count - 1));
                var visible = players.Skip(page * pageSize).Take(pageSize).ToArray();
                if (!players.Any(player => player.Id == selectedId)) selectedId = visible.FirstOrDefault()?.Id;
                SelectedPlayer = players.FirstOrDefault(player => player.Id == selectedId);
                for (var index = 0; index < visible.Length; index++)
                {
                    if (index == rows.Count) rows.Add(Instantiate(rowTemplate, rowTemplate.transform.parent));
                    var player = visible[index]; rows[index].gameObject.SetActive(true);
                    rows[index].Bind(player, ClubName(player), player.Id == selectedId, () => { selectedId = player.Id; Refresh(); });
                }
                for (var index = visible.Length; index < rows.Count; index++) rows[index].gameObject.SetActive(false);
                details.Bind(SelectedPlayer, ClubName(SelectedPlayer));
                resultCount.text = GameText.Get("career.playerCount", players.Length);
                pageCount.text = GameText.Get("career.pageCount", page + 1, count);
                emptyState.gameObject.SetActive(players.Length == 0);
                previous.interactable = page > 0; next.interactable = page + 1 < count;
                clear.interactable = query.Length != 0 || selectedPosition != 0 || selectedClub != null;
                RefreshExtra(career);
            }
            finally { refreshing = false; }
        }
        private static bool Matches(string value, string query) => value != null && GameText.Culture.CompareInfo.IndexOf(value, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        protected virtual void RefreshExtra(CareerSession career) { }
    }
}
