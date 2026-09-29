using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FStudio.FootballWorld.Infrastructure.GameModes;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using FStudio.MatchEngine.Enums;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Presentation
{
    // Presentation only: authored prefabs own geometry; the session owns settings, careers and competition state.
    public sealed class GameHubView : MonoBehaviour
    {
        [Header("Canvas ordering")]
        [SerializeField, Tooltip("Above team selection (0) and below loading overlays (50). Applied after this prefab joins the UI Canvas.")]
        private int sortingOrder = 30;
        [Header("Authored pages")]
        [SerializeField] private GameObject backdrop, homePage, quickMatchPage, championshipsPage, championshipPage, careerPage, optionsPage;
        [SerializeField] private GameObject careerOfficePage;
        [SerializeField] private GameObject careerSquadPage, careerTacticsPage, careerMarketPage;
        [SerializeField] private GameObject sharedHeader, sharedFooter;
        [SerializeField] private TMP_Text pageTitle, statusText, saveWarningText;
        [SerializeField] private Button backButton, retryButton, dismissWarningButton;
        [Header("Championship selection")]
        [SerializeField] private TMP_Dropdown editionDropdown, championshipCountryDropdown, championshipClubDropdown;
        [SerializeField] private TMP_Text editionDescription;
        [SerializeField] private Button startChampionshipButton, continueChampionshipButton;
        [Header("Championship dashboard")]
        [SerializeField] private TMP_Text championshipTitle, snapshotText, nextFixtureText;
        [SerializeField] private TMP_Text standingsTitle, rulesHint;
        [SerializeField] private Button playFixtureButton;
        [SerializeField] private Button simulateFixtureButton, continueCareerButton;
        [SerializeField] private HubStandingRow standingTemplate;
        [SerializeField] private HubFixtureRow fixtureTemplate;
        [Header("Career draft")]
        [SerializeField] private TMP_InputField coachName, careerYearInput;
        [SerializeField] private TMP_Dropdown careerMonthDropdown, careerCountryDropdown, careerClubDropdown;
        [SerializeField] private TMP_Text careerSummary;
        [SerializeField] private Button saveCareerButton;
        [SerializeField] private CoachAvatarView careerPortrait;
        [SerializeField] private Button[] avatarButtons;
        [SerializeField] private Image[] avatarSelectionFrames;
        [SerializeField] private GameHubTheme theme;
        [Header("Options")]
        [SerializeField] private TMP_Dropdown languageDropdown, cameraDropdown, difficultyDropdown;
        [Header("Shared confirmation")]
        [SerializeField] private GameObject confirmationPanel;
        [SerializeField] private TMP_Text confirmationTitle, confirmationMessage;
        [SerializeField] private Button confirmButton, cancelButton;

        private readonly List<HubStandingRow> standings = new List<HubStandingRow>();
        private readonly List<HubFixtureRow> fixtures = new List<HubFixtureRow>();
        private readonly string[] cameraIds = { "Stadium", "Tele", "Broadcast", "StadiumHigh" };
        private readonly string[] languages = { "pt", "en" };
        private readonly List<string> editionIds = new List<string>();
        private readonly List<string> championshipClubIds = new List<string>();
        private readonly List<string> careerClubIds = new List<string>();
        private readonly List<string> championshipCountryCodes = new List<string>(), careerCountryCodes = new List<string>();
        private string selectedChampionshipCountryCode, selectedCareerCountryCode;
        private GameHubSession session;
        private bool refreshing, careerInitialized;
        private string selectedEditionId, selectedChampionshipClubId, selectedCareerClubId, selectedAvatarId = "coach-1";
        private int selectedMonth = DateTime.Now.Month, selectedYear = DateTime.Now.Year;
        private HubPage? lastPage;
        private Action pendingConfirmation;
        private Func<bool> pendingCareerOffer;
        private string offerConfirmationMessage;
        private GameObject previousSelection;

        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying) return;
            // Unity normalizes sorting on a standalone prefab Canvas; apply the authored order after parenting.
            var canvas = GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            session = GameHubSession.Current;
            session.Changed += Refresh;
            GameText.Changed += Refresh;
            Wire();
            Refresh();
        }

        private void OnDisable()
        {
            if (session == null) return;
            session.Changed -= Refresh;
            GameText.Changed -= Refresh;
            Unwire();
            session = null;
        }

        private void Wire()
        {
            backButton.onClick.AddListener(Back);
            retryButton.onClick.AddListener(Retry);
            dismissWarningButton.onClick.AddListener(DismissWarning);
            startChampionshipButton.onClick.AddListener(StartChampionship);
            continueChampionshipButton.onClick.AddListener(ContinueChampionship);
            playFixtureButton.onClick.AddListener(PlayNext);
            simulateFixtureButton.onClick.AddListener(SimulateNext);
            continueCareerButton.onClick.AddListener(ContinueCareer);
            saveCareerButton.onClick.AddListener(SaveCareer);
            editionDropdown.onValueChanged.AddListener(SelectEdition);
            championshipCountryDropdown.onValueChanged.AddListener(SelectChampionshipCountry);
            careerCountryDropdown.onValueChanged.AddListener(SelectCareerCountry);
            championshipClubDropdown.onValueChanged.AddListener(SelectChampionshipClub);
            careerClubDropdown.onValueChanged.AddListener(SelectCareerClub);
            careerMonthDropdown.onValueChanged.AddListener(SelectMonth);
            careerYearInput.onValueChanged.AddListener(SelectYear);
            languageDropdown.onValueChanged.AddListener(SelectLanguage);
            cameraDropdown.onValueChanged.AddListener(SelectCamera);
            difficultyDropdown.onValueChanged.AddListener(SelectDifficulty);
            confirmButton.onClick.AddListener(AcceptConfirmation);
            cancelButton.onClick.AddListener(CancelConfirmation);
        }

        private void Unwire()
        {
            backButton.onClick.RemoveListener(Back);
            retryButton.onClick.RemoveListener(Retry);
            dismissWarningButton.onClick.RemoveListener(DismissWarning);
            startChampionshipButton.onClick.RemoveListener(StartChampionship);
            continueChampionshipButton.onClick.RemoveListener(ContinueChampionship);
            playFixtureButton.onClick.RemoveListener(PlayNext);
            simulateFixtureButton.onClick.RemoveListener(SimulateNext);
            continueCareerButton.onClick.RemoveListener(ContinueCareer);
            saveCareerButton.onClick.RemoveListener(SaveCareer);
            editionDropdown.onValueChanged.RemoveListener(SelectEdition);
            championshipCountryDropdown.onValueChanged.RemoveListener(SelectChampionshipCountry);
            careerCountryDropdown.onValueChanged.RemoveListener(SelectCareerCountry);
            championshipClubDropdown.onValueChanged.RemoveListener(SelectChampionshipClub);
            careerClubDropdown.onValueChanged.RemoveListener(SelectCareerClub);
            careerMonthDropdown.onValueChanged.RemoveListener(SelectMonth);
            careerYearInput.onValueChanged.RemoveListener(SelectYear);
            languageDropdown.onValueChanged.RemoveListener(SelectLanguage);
            cameraDropdown.onValueChanged.RemoveListener(SelectCamera);
            difficultyDropdown.onValueChanged.RemoveListener(SelectDifficulty);
            confirmButton.onClick.RemoveListener(AcceptConfirmation);
            cancelButton.onClick.RemoveListener(CancelConfirmation);
        }

        public void OpenQuickMatch() { session.Navigate(HubPage.QuickMatch); }
        public void OpenChampionships() { session.Navigate(HubPage.Championships); }
        public void OpenCareer() { session.OpenCareer(); }
        public void OpenCareerSquad() { session.Navigate(HubPage.CareerSquad); }
        public void OpenCareerTactics() { session.Navigate(HubPage.CareerTactics); }
        public void OpenCareerMarket() { session.Navigate(HubPage.CareerMarket); }
        public void OpenOptions() { session.Navigate(HubPage.Options); }
        public void GoHome() { session.Navigate(HubPage.Home); }
        public void SelectAvatar(int index)
        {
            if (theme == null || theme.Portraits == null || index < 0 || index >= theme.Portraits.Length || theme.Portraits[index] == null) return;
            selectedAvatarId = theme.Portraits[index].Id;
            RefreshAvatar();
        }

        private void Back() { session.Navigate(session.Page == HubPage.Championship
            ? (session.IsCareerCalendar ? HubPage.CareerOffice : HubPage.Championships)
            : session.Page == HubPage.CareerSquad || session.Page == HubPage.CareerTactics || session.Page == HubPage.CareerMarket ? HubPage.CareerOffice : HubPage.Home); }
        private void SimulateNext() { session.SimulateChampionship(); }
        private void ContinueCareer() { session.ContinueCareer(); }
        private void Retry() { session.RetryDatabase(); }
        private void DismissWarning() { session.DismissSaveWarning(); }
        private void ContinueChampionship() { session.Navigate(HubPage.Championship); }
        private void StartChampionship()
        {
            if (session.HasChampionshipSave)
                Confirm("dialog.replaceChampionshipTitle", "dialog.replaceChampionshipBody", () => session.StartChampionship(selectedEditionId, selectedChampionshipClubId));
            else session.StartChampionship(selectedEditionId, selectedChampionshipClubId);
        }
        private async void PlayNext()
        {
            if (session.IsBusy) return;
            try { await session.PlayNextFixture(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        private void SaveCareer()
        {
            if (string.IsNullOrWhiteSpace(coachName.text) || selectedYear < 1 || selectedYear > 9999)
            {
                session.SaveCareer(coachName.text, selectedAvatarId, selectedMonth, selectedYear, selectedCareerClubId);
                var input = string.IsNullOrWhiteSpace(coachName.text) ? coachName : careerYearInput;
                EventSystem.current?.SetSelectedGameObject(input.gameObject);
                input.ActivateInputField();
                return;
            }
            if (session.HasCareerSave)
                Confirm("dialog.replaceCareerTitle", "dialog.replaceDailyCareerBody", () => session.SaveCareer(coachName.text, selectedAvatarId, selectedMonth, selectedYear, selectedCareerClubId));
            else session.SaveCareer(coachName.text, selectedAvatarId, selectedMonth, selectedYear, selectedCareerClubId);
        }
        private void SelectEdition(int index) { if (!refreshing && index < editionIds.Count) { selectedEditionId = editionIds[index]; selectedChampionshipClubId = null; Refresh(); } }
        private void SelectChampionshipClub(int index) { if (!refreshing && index < championshipClubIds.Count) selectedChampionshipClubId = championshipClubIds[index]; }
        private void SelectCareerClub(int index) { if (!refreshing && index < careerClubIds.Count) selectedCareerClubId = careerClubIds[index]; }
        private void SelectChampionshipCountry(int index) { if (!refreshing && index >= 0 && index < championshipCountryCodes.Count) { selectedChampionshipCountryCode = championshipCountryCodes[index]; Refresh(); } }
        private void SelectCareerCountry(int index) { if (!refreshing && index >= 0 && index < careerCountryCodes.Count) { selectedCareerCountryCode = careerCountryCodes[index]; Refresh(); } }
        private void SelectMonth(int index) { if (!refreshing) selectedMonth = index + 1; }
        private void SelectYear(string value) { if (!refreshing) selectedYear = int.TryParse(value, out var year) ? year : 0; }
        private void SelectLanguage(int index) { if (!refreshing && index < languages.Length) session.SetLanguage(languages[index]); }
        private void SelectCamera(int index) { if (!refreshing && index < cameraIds.Length) session.SetCamera(cameraIds[index]); }
        private void SelectDifficulty(int index) { if (!refreshing) session.SetDifficulty((AILevel)index); }

        private void Refresh()
        {
            if (session == null || refreshing) return;
            refreshing = true;
            try
            {
                var page = session.Page;
                backdrop.SetActive(page != HubPage.QuickMatch);
                sharedHeader.SetActive(page != HubPage.Home && page != HubPage.QuickMatch);
                sharedFooter.SetActive(page != HubPage.QuickMatch);
                homePage.SetActive(page == HubPage.Home);
                quickMatchPage.SetActive(page == HubPage.QuickMatch);
                championshipsPage.SetActive(page == HubPage.Championships);
                championshipPage.SetActive(page == HubPage.Championship);
                careerPage.SetActive(page == HubPage.Career);
                careerOfficePage.SetActive(page == HubPage.CareerOffice);
                careerSquadPage.SetActive(page == HubPage.CareerSquad);
                careerTacticsPage.SetActive(page == HubPage.CareerTactics);
                careerMarketPage.SetActive(page == HubPage.CareerMarket);
                optionsPage.SetActive(page == HubPage.Options);
                pageTitle.text = GameText.Get(page == HubPage.CareerSquad ? "career.squad" : page == HubPage.CareerTactics ? "career.tactics" : page == HubPage.CareerMarket ? "career.market"
                    : page == HubPage.CareerOffice ? "career.office" : page == HubPage.Career ? "career.title" : page == HubPage.Options ? "options.title" : "hub.championships");
                statusText.text = session.IsBusy ? GameText.Get("hub.busy") : session.StatusMessage;
                saveWarningText.text = session.SaveWarning ?? string.Empty;
                dismissWarningButton.gameObject.SetActive(!string.IsNullOrEmpty(session.SaveWarning));
                retryButton.gameObject.SetActive(!session.DatabaseReady && !session.IsBusy && !string.IsNullOrEmpty(session.StatusMessage));
                RefreshChampionships();
                RefreshCareer();
                RefreshOptions();
                var group = GetComponent<CanvasGroup>();
                group.interactable = !session.IsBusy;
                if (lastPage != page)
                {
                    lastPage = page;
                    FocusPage(page);
                }
            }
            finally { refreshing = false; }
        }

        private void RefreshChampionships()
        {
            editionIds.Clear();
            editionIds.AddRange(session.Editions.Select(edition => edition.Id));
            if (!editionIds.Contains(selectedEditionId)) selectedEditionId = editionIds.FirstOrDefault();
            SetOptions(editionDropdown, session.Editions.Select(edition => edition.CompetitionName + " · " + edition.Name), editionIds.IndexOf(selectedEditionId), "hub.noEditions");
            var selected = session.Editions.FirstOrDefault(edition => edition.Id == selectedEditionId);
            var participants = session.Teams.Where(team => selected != null && selected.ParticipantClubIds.Contains(team.ClubId)).ToArray();
            var countries = session.Countries.Where(country => participants.Any(team => team.CountryCode == country.Code)).ToArray();
            selectedChampionshipCountryCode = CatalogCountryFilter.RetainCountry(countries, selectedChampionshipCountryCode, participants, selectedChampionshipClubId);
            championshipCountryCodes.Clear(); championshipCountryCodes.AddRange(countries.Select(country => country.Code));
            SetOptions(championshipCountryDropdown, countries.Select(country => GameText.CountryName(country.Code, country.Name)), championshipCountryCodes.IndexOf(selectedChampionshipCountryCode), "country.none");
            var teams = CatalogCountryFilter.Teams(participants, selectedChampionshipCountryCode);
            championshipClubIds.Clear(); championshipClubIds.AddRange(teams.Select(team => team.ClubId));
            if (!championshipClubIds.Contains(selectedChampionshipClubId)) selectedChampionshipClubId = championshipClubIds.FirstOrDefault();
            SetOptions(championshipClubDropdown, teams.Select(team => team.Name), championshipClubIds.IndexOf(selectedChampionshipClubId), "hub.noClubs");
            editionDescription.text = selected == null ? GameText.Get("hub.noEditions") : GameText.Get("hub.dates", GameText.FormatDate(selected.FirstDate), GameText.FormatDate(selected.LastDate));
            startChampionshipButton.interactable = session.DatabaseReady && selected != null && selectedChampionshipClubId != null && !session.IsBusy;
            continueChampionshipButton.gameObject.SetActive(session.Championship != null);
            var championship = session.Championship;
            if (championship == null) return;
            championshipTitle.text = championship.Name + " · " + championship.EditionName;
            standingsTitle.GetComponent<LocalizedText>().Key = championship.IsPaulista ? "hub.cumulative" : "hub.standings";
            rulesHint.gameObject.SetActive(championship.IsPaulista && string.IsNullOrEmpty(session.StatusMessage));
            snapshotText.text = championship.UserClubName + "  ·  " + GameText.Get("hub.revision", championship.DatabaseRevision)
                + "  ·  " + GameText.Get("phase." + championship.Phase);
            for (var index = 0; index < championship.Standings.Count; index++)
            {
                if (index == standings.Count) standings.Add(Instantiate(standingTemplate, standingTemplate.transform.parent));
                standings[index].gameObject.SetActive(true);
                standings[index].Bind(championship.Standings[index], championship.Standings[index].ClubId == championship.UserClubId);
            }
            for (var index = championship.Standings.Count; index < standings.Count; index++) standings[index].gameObject.SetActive(false);
            for (var index = 0; index < championship.Fixtures.Count; index++)
            {
                if (index == fixtures.Count) fixtures.Add(Instantiate(fixtureTemplate, fixtureTemplate.transform.parent));
                fixtures[index].gameObject.SetActive(true);
                fixtures[index].Bind(championship.Fixtures[index]);
            }
            for (var index = championship.Fixtures.Count; index < fixtures.Count; index++) fixtures[index].gameObject.SetActive(false);
            var next = championship.NextFixture;
            nextFixtureText.text = championship.IsComplete ? (championship.ChampionName == null ? GameText.Get("hub.complete") : GameText.Get("hub.champion", championship.ChampionName))
                : next == null ? GameText.Get(session.IsCareerCalendar ? "hub.awaitingPhase" : "hub.eliminated") : GameText.FormatDate(next.Date) + "\n" + next.HomeName + " × " + next.AwayName;
            if (championship.RelegatedNames.Count > 0 && championship.IsComplete)
                nextFixtureText.text += "\n" + GameText.Get("hub.relegated", string.Join(", ", championship.RelegatedNames));
            playFixtureButton.interactable = championship.CanPlayNext && !session.IsBusy && (!session.IsCareerCalendar || session.CareerCanPlay);
            simulateFixtureButton.interactable = !championship.IsComplete && !session.IsBusy && (!session.IsCareerCalendar || session.CareerCanPlay);
            simulateFixtureButton.GetComponentInChildren<LocalizedText>(true).Key = next == null ? "hub.simulateRound" : "hub.simulateMatch";
        }

        private void RefreshCareer()
        {
            if (!careerInitialized && session.DatabaseReady && session.Teams.Count > 0)
            {
                var profile = session.Career;
                if (profile != null)
                {
                    coachName.SetTextWithoutNotify(profile.CoachName);
                    selectedAvatarId = profile.AvatarId;
                    selectedMonth = profile.StartMonth; selectedYear = profile.StartYear; selectedCareerClubId = profile.ClubId;
                }
                else
                {
                    selectedMonth = session.DefaultCareerDate.Month;
                    selectedYear = session.DefaultCareerDate.Year;
                }
                careerYearInput.SetTextWithoutNotify(selectedYear.ToString());
                careerInitialized = true;
            }
            var countries = session.Countries;
            selectedCareerCountryCode = CatalogCountryFilter.RetainCountry(countries, selectedCareerCountryCode, session.Teams, selectedCareerClubId);
            careerCountryCodes.Clear(); careerCountryCodes.AddRange(countries.Select(country => country.Code));
            SetOptions(careerCountryDropdown, countries.Select(country => GameText.CountryName(country.Code, country.Name)), careerCountryCodes.IndexOf(selectedCareerCountryCode), "country.none");
            var teams = CatalogCountryFilter.Teams(session.Teams, selectedCareerCountryCode);
            careerClubIds.Clear(); careerClubIds.AddRange(teams.Select(team => team.ClubId));
            if (!careerClubIds.Contains(selectedCareerClubId)) selectedCareerClubId = careerClubIds.FirstOrDefault();
            SetOptions(careerClubDropdown, teams.Select(team => team.Name), careerClubIds.IndexOf(selectedCareerClubId), "hub.noClubs");
            var culture = CultureInfo.GetCultureInfo(session.Settings.Language == "en" ? "en-US" : "pt-BR");
            SetOptions(careerMonthDropdown, Enumerable.Range(1, 12).Select(month => culture.DateTimeFormat.GetMonthName(month)), selectedMonth - 1, "");
            saveCareerButton.interactable = session.DatabaseReady && selectedCareerClubId != null;
            saveCareerButton.GetComponentInChildren<LocalizedText>(true).Key = "career.startDaily";
            continueCareerButton.gameObject.SetActive(session.HasDailyCareer);
            careerSummary.text = session.Career == null ? string.Empty : GameText.Get("career.created") + "\n" + session.Career.CoachName + " · " + session.Career.ClubName + "\n" + session.Career.StartMonth.ToString("00") + "/" + session.Career.StartYear;
            RefreshAvatar();
        }

        private void RefreshAvatar()
        {
            careerPortrait.Bind(selectedAvatarId);
            for (var index = 0; index < avatarSelectionFrames.Length; index++)
            {
                var portrait = theme.Portraits != null && index < theme.Portraits.Length ? theme.Portraits[index] : null;
                if (avatarSelectionFrames[index] != null)
                    avatarSelectionFrames[index].color = portrait != null && portrait.Id == selectedAvatarId ? theme.Primary : theme.Line;
            }
        }

        private void RefreshOptions()
        {
            SetOptions(languageDropdown, new[] { "Português", "English" }, Array.IndexOf(languages, session.Settings.Language), "");
            SetOptions(cameraDropdown, cameraIds.Select(camera => GameText.Get("camera." + camera)), Array.IndexOf(cameraIds, session.Settings.CameraId), "");
            SetOptions(difficultyDropdown, Enum.GetNames(typeof(AILevel)).Select(difficulty => GameText.Get("difficulty." + difficulty)), (int)session.Settings.Difficulty, "");
        }

        private static void SetOptions(TMP_Dropdown dropdown, IEnumerable<string> labels, int selected, string emptyKey)
        {
            var items = labels.ToList();
            dropdown.ClearOptions();
            dropdown.AddOptions(items.Count == 0 ? new List<string> { GameText.Get(emptyKey) } : items);
            dropdown.SetValueWithoutNotify(Mathf.Max(0, selected));
            dropdown.RefreshShownValue();
            dropdown.interactable = items.Count > 0;
        }

        private void Confirm(string title, string message, Action action)
        {
            previousSelection = EventSystem.current?.currentSelectedGameObject;
            pendingCareerOffer = null;
            pendingConfirmation = action;
            confirmButton.GetComponentInChildren<LocalizedText>(true).Key = "dialog.confirm";
            confirmButton.GetComponent<Image>().color = theme.Danger;
            confirmationTitle.text = GameText.Get(title);
            confirmationMessage.text = GameText.Get(message);
            confirmationPanel.SetActive(true);
            SetPageInteraction(false);
            EventSystem.current?.SetSelectedGameObject(cancelButton.gameObject);
        }

        private void AcceptConfirmation()
        {
            if (pendingCareerOffer != null)
            {
                if (session.IsBusy) return;
                if (pendingCareerOffer()) CancelConfirmation();
                else confirmationMessage.text = offerConfirmationMessage + "\n\n" + session.StatusMessage;
                return;
            }
            var action = pendingConfirmation;
            CancelConfirmation();
            action?.Invoke();
        }

        private void CancelConfirmation()
        {
            pendingConfirmation = null;
            pendingCareerOffer = null;
            confirmationPanel.SetActive(false);
            SetPageInteraction(true);
            if (previousSelection != null && previousSelection.activeInHierarchy)
                EventSystem.current?.SetSelectedGameObject(previousSelection);
        }

        public void ConfirmCareerOffer(string message, Func<bool> submit)
        {
            previousSelection = EventSystem.current?.currentSelectedGameObject;
            pendingConfirmation = null; pendingCareerOffer = submit; offerConfirmationMessage = message;
            confirmationTitle.text = GameText.Get("career.offerConfirmTitle"); confirmationMessage.text = message;
            confirmButton.GetComponentInChildren<LocalizedText>(true).Key = "career.submitOffer";
            confirmButton.GetComponent<Image>().color = theme.Primary;
            confirmationPanel.SetActive(true); SetPageInteraction(false);
            EventSystem.current?.SetSelectedGameObject(cancelButton.gameObject);
        }

        private void SetPageInteraction(bool value)
        {
            foreach (var page in new[] { homePage, quickMatchPage, championshipsPage, championshipPage, careerPage, careerOfficePage, careerSquadPage, careerTacticsPage, careerMarketPage, optionsPage, sharedHeader, sharedFooter })
            {
                var group = page.GetComponent<CanvasGroup>();
                if (group == null) continue;
                group.interactable = value;
                group.blocksRaycasts = value;
            }
        }

        private void FocusPage(HubPage page)
        {
            Selectable selected = null;
            switch (page)
            {
                case HubPage.Home: selected = homePage.GetComponentInChildren<Button>(); break;
                case HubPage.Career: selected = coachName; break;
                case HubPage.CareerOffice: selected = careerOfficePage.GetComponentInChildren<Button>(); break;
                case HubPage.CareerSquad: selected = careerSquadPage.GetComponentInChildren<TMP_InputField>(); break;
                case HubPage.CareerMarket: selected = careerMarketPage.GetComponentInChildren<Selectable>(); break;
                case HubPage.CareerTactics: selected = careerTacticsPage.GetComponentInChildren<TMP_Dropdown>(); break;
                case HubPage.Options: selected = languageDropdown; break;
                case HubPage.Championships: selected = editionDropdown.interactable ? (Selectable)editionDropdown : backButton; break;
                case HubPage.Championship: selected = playFixtureButton.interactable ? playFixtureButton : backButton; break;
            }
            EventSystem.current?.SetSelectedGameObject(selected == null ? null : selected.gameObject);
        }

        private void Update()
        {
            if (session == null || confirmationPanel == null) return;
            if (confirmationPanel.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                CancelConfirmation();
        }
    }
}
