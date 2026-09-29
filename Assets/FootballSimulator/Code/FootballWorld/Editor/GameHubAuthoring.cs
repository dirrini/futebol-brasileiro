using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FStudio.FootballWorld.Infrastructure.GameModes;
using FStudio.FootballWorld.Presentation;
using FStudio.Graphics.Cameras;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using FStudio.UI;
using FStudio.UI.Panels;
using FStudio.UI.Utilities;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Editor
{
    // This command authors assets once. Player builds instantiate the saved prefab; they never construct its layout.
    public static partial class GameHubAuthoring
    {
        private const string Root = "Assets/FootballSimulator/Resources/FootballWorld/";
        private const string PrefabPath = Root + "GameHub.prefab";
        private static GameHubTheme theme;
        private static TMP_DefaultControls.Resources controls;
        private static SerializedObject view;

        [MenuItem("Tools/Futebol Brasileiro/Create game hub assets")]
        public static void CreateAssets()
        {
            EnsureTheme();
            GameHubTextSeed.MergeCatalog();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) CreatePrefab();
            else Debug.Log("[GameHub] Existing prefab preserved. Edit the prefab directly or use the explicit rebuild command.");
            EnsureCanvasCameraBinding();
            EnsureCountryFilters();
            EnsureCareerOffice();
            EnsureCareerManagement();
            EnsureCareerInspectionLayout();
            LocalizeLegacyPrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GameHub] Editable menu assets ready: " + PrefabPath);
        }

        [MenuItem("Tools/Futebol Brasileiro/Rebuild generated game hub layout")]
        public static void RebuildLayout()
        {
            if (!UnityEngine.Application.isBatchMode && !EditorUtility.DisplayDialog("Rebuild game hub layout", "This replaces manual layout edits in GameHub.prefab. Theme and localization assets are preserved.", "Rebuild", "Cancel")) return;
            EnsureTheme();
            GameHubTextSeed.MergeCatalog();
            CreatePrefab();
            EnsureCountryFilters();
            EnsureCareerOffice();
            EnsureCareerManagement();
            EnsureCareerInspectionLayout();
            LocalizeLegacyPrefabs();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Futebol Brasileiro/Apply game hub theme")]
        public static void ApplyTheme()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) return;
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (var binding in root.GetComponentsInChildren<GameHubThemeBinding>(true)) binding.Apply();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[GameHub] Theme applied without changing geometry, text or event bindings.");
        }

        private static void EnsureTheme()
        {
            Directory.CreateDirectory(Root);
            theme = AssetDatabase.LoadAssetAtPath<GameHubTheme>(Root + "GameHubTheme.asset");
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<GameHubTheme>();
                theme.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/FootballSimulator/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                theme.RoundedPanel = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FootballSimulator/Arts/UI/Textures/buttonSliced.png");
                theme.Circle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FootballSimulator/Arts/UI/Textures/circle.png");
                theme.Portraits = new[]
                {
                    new CoachPortrait { Id = "coach-1", LabelKey = "career.avatar1", Skin = Hex("D9A67A"), Hair = Hex("342922"), Shirt = Hex("365D66"), HasHair = true },
                    new CoachPortrait { Id = "coach-2", LabelKey = "career.avatar2", Skin = Hex("A57455"), Hair = Hex("211C18"), Shirt = Hex("23735A"), HasHair = false },
                    new CoachPortrait { Id = "coach-3", LabelKey = "career.avatar3", Skin = Hex("604231"), Hair = Hex("222326"), Shirt = Hex("263746"), HasHair = true }
                };
                AssetDatabase.CreateAsset(theme, Root + "GameHubTheme.asset");
            }
            if (theme.Font == null || theme.RoundedPanel == null || theme.Circle == null)
                throw new InvalidOperationException("GameHubTheme requires a TMP font, rounded panel sprite and circle sprite. Assign them in the Inspector before creating the menu.");
            if (theme.Portraits == null || theme.Portraits.Length < 3 || theme.Portraits.Take(3).Any(portrait => portrait == null) ||
                !new[] { "coach-1", "coach-2", "coach-3" }.SequenceEqual(theme.Portraits.Take(3).Select(portrait => portrait.Id)))
                throw new InvalidOperationException("GameHubTheme requires three portraits with stable IDs coach-1, coach-2 and coach-3, in that order.");
            controls = new TMP_DefaultControls.Resources
            {
                standard = theme.RoundedPanel, background = theme.RoundedPanel, inputField = theme.RoundedPanel,
                knob = theme.Circle, checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"), mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
            };
        }

        private static void EnsureCanvasCameraBinding()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // Each nested Canvas raycaster needs the project UI camera; it does not inherit the parent's binding.
                if (root.GetComponent<UICanvas>() != null) return;
                root.AddComponent<UICanvas>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[GameHub] Canonical UI camera binding added without changing the authored layout.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // One-time migration; repeat calls preserve the saved layout and field references.
        private static void EnsureCountryFilters()
        {
            var hub = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var properties = new SerializedObject(hub.GetComponent<GameHubView>());
                if (properties.FindProperty("careerCountryDropdown").objectReferenceValue == null)
                {
                    var parent = hub.transform.Find("CareerProfile");
                    Set(properties, "careerCountryDropdown", Dropdown(parent, "CareerCountry", "country.label", 519, 361, 300));
                    Place(parent.Find("CareerClub").GetComponent<RectTransform>(), 850, 404, 607, 61);
                    Place(parent.Find("CareerClubLabel").GetComponent<RectTransform>(), 850, 361, 607, 35);
                }
                if (properties.FindProperty("championshipCountryDropdown").objectReferenceValue == null)
                {
                    var parent = hub.transform.Find("ChampionshipSelection");
                    Set(properties, "championshipCountryDropdown", Dropdown(parent, "ChampionshipCountry", "country.label", 126, 402, 440));
                    Place(parent.Find("Edition").GetComponent<RectTransform>(), 126, 316, 1346, 61);
                    Place(parent.Find("EditionLabel").GetComponent<RectTransform>(), 126, 273, 1346, 35);
                    Place(parent.Find("Club").GetComponent<RectTransform>(), 606, 445, 866, 61);
                    Place(parent.Find("ClubLabel").GetComponent<RectTransform>(), 606, 402, 866, 35);
                    Place(parent.Find("EditionDates").GetComponent<RectTransform>(), 130, 515, 1270, 43);
                }
                properties.ApplyModifiedPropertiesWithoutUndo();
                foreach (var child in hub.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
                PrefabUtility.SaveAsPrefabAsset(hub, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(hub); }

            const string quickPath = "Assets/FootballSimulator/Arts/UI/Panels/MainMenuPanel.prefab";
            var quick = PrefabUtility.LoadPrefabContents(quickPath);
            try
            {
                var properties = new SerializedObject(quick.GetComponent<MainMenuPanel>());
                foreach (var side in new[] { "home", "away" })
                {
                    if (properties.FindProperty(side + "Country").objectReferenceValue != null) continue;
                    var team = (TeamSelectionTeam)properties.FindProperty(side + "Team").objectReferenceValue;
                    var teamProperties = new SerializedObject(team);
                    var teamRect = (RectTransform)team.transform;
                    var x = teamRect.anchoredPosition.x;
                    teamRect.anchoredPosition = new Vector2(x, -55);
                    teamRect.localScale = Vector3.one * .9f;
                    var group = new GameObject(side + "Country", typeof(RectTransform)); group.SetActive(false);
                    group.transform.SetParent(team.transform.parent, false);
                    var rect = group.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = new Vector2(x, 252); rect.sizeDelta = new Vector2(500, 74);
                    var country = group.AddComponent<TeamCountrySelection>();
                    var countryProperties = new SerializedObject(country);
                    Set(countryProperties, "countryName", Label(group.transform, "CountryName", null, 62, 31, 376, 41, 28, theme.White, true, TextAlignmentOptions.Center));
                    Label(group.transform, "CountryLabel", "country.label", 62, 0, 376, 29, 20, theme.Accent, false, TextAlignmentOptions.Center);
                    foreach (var previous in new[] { true, false })
                    {
                        var field = previous ? "previousButton" : "nextButton";
                        var template = (InteractiveUIElement)teamProperties.FindProperty(field).objectReferenceValue;
                        var button = UnityEngine.Object.Instantiate(template, group.transform, false);
                        button.name = previous ? "PreviousCountry" : "NextCountry";
                        button.onClick = new UnityEvent(); button.onLateClick = new UnityEvent();
                        if (previous) UnityEventTools.AddPersistentListener(button.onClick, country.Previous);
                        else UnityEventTools.AddPersistentListener(button.onClick, country.Next);
                        Place(button.GetComponent<RectTransform>(), previous ? 0 : 446, 22, 54, 54);
                        Set(countryProperties, field, button);
                    }
                    countryProperties.ApplyModifiedPropertiesWithoutUndo();
                    group.SetActive(true); Set(properties, side + "Country", country);
                }
                properties.ApplyModifiedPropertiesWithoutUndo();
                foreach (var child in quick.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
                PrefabUtility.SaveAsPrefabAsset(quick, quickPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(quick); }
        }

        private static void CreatePrefab()
        {
            var root = new GameObject("GameHub", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster), typeof(UICanvas));
            root.layer = 5;
            Stretch(root.GetComponent<RectTransform>());
            var canvas = root.GetComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 30;
            var component = root.AddComponent<GameHubView>();
            view = new SerializedObject(component);
            view.FindProperty("sortingOrder").intValue = 30;
            Set(view, "theme", theme);

            var backdrop = Page(root.transform, "Backdrop");
            var bg = backdrop.AddComponent<Image>(); bg.color = theme.Background; bg.raycastTarget = true; BindTheme(bg, theme.Background);
            Pitch(backdrop.transform);
            Set(view, "backdrop", backdrop);

            var home = Page(root.transform, "Home"); Set(view, "homePage", home);
            Label(home.transform, "Brand", "hub.brand", 80, 67, 1080, 82, 56, theme.White, true);
            Label(home.transform, "Tagline", "hub.tagline", 82, 152, 1040, 52, 28, theme.Accent);
            var career = Card(home.transform, "CareerCard", 80, 254, 672, 423, theme.Primary, component.OpenCareer);
            Label(career.transform, "Career", "hub.career", 34, 32, 470, 63, 44, theme.White, true);
            Label(career.transform, "CareerDescription", "hub.careerHint", 36, 108, 330, 126, 28, theme.White);
            Label(career.transform, "CareerArrow", null, 36, 326, 110, 54, 40, theme.White).text = "→";
            var heroAvatar = Avatar(career.transform, "CoachPortrait", 373, 78, 238, 286, "coach-1");
            heroAvatar.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, -3);
            MenuCard(home.transform, "QuickMatchCard", "hub.quickMatch", "hub.quickMatchHint", 788, 254, component.OpenQuickMatch);
            MenuCard(home.transform, "ChampionshipCard", "hub.championships", "hub.championshipsHint", 788, 405, component.OpenChampionships);
            MenuCard(home.transform, "OptionsCard", "hub.options", "hub.optionsHint", 788, 556, component.OpenOptions);

            var header = Page(root.transform, "SharedHeader"); Set(view, "sharedHeader", header);
            Set(view, "pageTitle", Label(header.transform, "PageTitle", null, 80, 58, 1140, 76, 46, theme.White, true));
            Set(view, "backButton", Button(header.transform, "Back", "hub.back", 1320, 70, 200, 58, theme.Surface, theme.Ink));

            var quick = Page(root.transform, "QuickMatchNavigation"); Set(view, "quickMatchPage", quick);
            var quickBack = Button(quick.transform, "BackToHome", "hub.back", 48, 34, 194, 54, theme.Surface, theme.Ink);
            UnityEventTools.AddPersistentListener(quickBack.onClick, component.GoHome);

            var championshipSelection = Page(root.transform, "ChampionshipSelection"); Set(view, "championshipsPage", championshipSelection);
            Panel(championshipSelection.transform, "SelectionSurface", 80, 170, 1440, 580, theme.Surface);
            Label(championshipSelection.transform, "ChampionshipIntro", "hub.championshipsHint", 126, 202, 1250, 54, 28, theme.Muted);
            Set(view, "editionDropdown", Dropdown(championshipSelection.transform, "Edition", "hub.edition", 126, 290, 660));
            Set(view, "championshipClubDropdown", Dropdown(championshipSelection.transform, "Club", "hub.club", 830, 290, 642));
            Set(view, "editionDescription", Label(championshipSelection.transform, "EditionDates", null, 130, 415, 1270, 70, 26, theme.Muted));
            Set(view, "startChampionshipButton", Button(championshipSelection.transform, "StartChampionship", "hub.newChampionship", 126, 570, 580, 74, theme.Primary, theme.White));
            Set(view, "continueChampionshipButton", Button(championshipSelection.transform, "ContinueChampionship", "hub.continue", 738, 570, 580, 74, theme.White, theme.Ink));

            var dashboard = Page(root.transform, "ChampionshipDashboard"); Set(view, "championshipPage", dashboard);
            CreateDashboard(dashboard.transform);
            var careerPage = Page(root.transform, "CareerProfile"); Set(view, "careerPage", careerPage);
            CreateCareer(careerPage.transform, component);
            var options = Page(root.transform, "Preferences"); Set(view, "optionsPage", options);
            Panel(options.transform, "PreferencesSurface", 80, 170, 1440, 580, theme.Surface);
            Set(view, "languageDropdown", Dropdown(options.transform, "Language", "options.language", 132, 224, 610));
            Set(view, "cameraDropdown", Dropdown(options.transform, "Camera", "options.camera", 132, 374, 610));
            Set(view, "difficultyDropdown", Dropdown(options.transform, "Difficulty", "options.difficulty", 132, 524, 610));
            Label(options.transform, "PreferencesHint", "options.saved", 850, 284, 568, 150, 28, theme.Muted);
            PitchInset(options.transform, 880, 485, 460, 180);

            var footer = Page(root.transform, "SharedFooter"); Set(view, "sharedFooter", footer);
            Set(view, "statusText", Label(footer.transform, "Status", null, 82, 778, 1220, 54, 24, theme.White));
            Set(view, "retryButton", Button(footer.transform, "Retry", "hub.retry", 1320, 775, 200, 54, theme.Surface, theme.Ink));
            Set(view, "saveWarningText", Label(footer.transform, "SaveWarning", null, 82, 835, 1180, 50, 22, theme.Accent));
            Set(view, "dismissWarningButton", Button(footer.transform, "DismissWarning", "hub.dismiss", 1280, 835, 240, 48, theme.Surface, theme.Ink, 22));
            CreateConfirmation(root.transform);
            view.ApplyModifiedPropertiesWithoutUndo();

            // The saved preview opens the home page. Runtime selects the page retained by GameHubSession.
            header.SetActive(false); quick.SetActive(false); championshipSelection.SetActive(false);
            dashboard.SetActive(false); careerPage.SetActive(false); options.SetActive(false);
            // TMP/UGUI factories create their own child objects; keep every descendant on the UI camera layer.
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateDashboard(Transform parent)
        {
            Set(view, "championshipTitle", Label(parent, "ChampionshipName", null, 82, 151, 1350, 50, 31, theme.White, true));
            Set(view, "snapshotText", Label(parent, "Snapshot", null, 84, 202, 1350, 36, 22, theme.Accent));
            Panel(parent, "StandingsSurface", 80, 260, 790, 490, theme.Surface);
            Label(parent, "StandingsTitle", "hub.standings", 108, 283, 720, 43, 29, theme.Ink, true);
            var tableHead = Rect(parent, "TableHeading", 108, 340, 734, 40);
            TableCell(tableHead, "Rank", null, 0, 43, 20, theme.Muted).text = "#";
            TableCell(tableHead, "Club", "hub.tableClub", 43, 315, 21, theme.Muted);
            var keys = new[] { "hub.tableP", "hub.tableW", "hub.tableD", "hub.tableL", "hub.tableGD", "hub.tablePts" };
            for (var index = 0; index < keys.Length; index++) TableCell(tableHead, "Column" + index, keys[index], 358 + index * 59, 59, 20, theme.Muted, true);
            var tableContent = Scroll(parent, "StandingsScroll", 108, 385, 734, 330);
            var row = Rect(tableContent, "StandingRowTemplate", 0, 0, 720, 48);
            var rowBg = row.gameObject.AddComponent<Image>(); rowBg.color = theme.White;
            var standing = row.gameObject.AddComponent<HubStandingRow>();
            var properties = new SerializedObject(standing); Set(properties, "theme", theme); Set(properties, "background", rowBg);
            Set(properties, "rank", TableCell(row, "Rank", null, 8, 35, 22, theme.Muted));
            Set(properties, "club", TableCell(row, "Club", null, 43, 315, 23, theme.Ink));
            var fields = new[] { "played", "won", "drawn", "lost", "difference", "points" };
            for (var index = 0; index < fields.Length; index++) Set(properties, fields[index], TableCell(row, fields[index], null, 358 + index * 59, 59, 23, theme.Ink, true));
            properties.ApplyModifiedPropertiesWithoutUndo(); row.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
            row.gameObject.SetActive(false); Set(view, "standingTemplate", standing);

            Panel(parent, "FixturesSurface", 897, 260, 623, 287, theme.Surface);
            Label(parent, "FixturesTitle", "hub.fixtures", 925, 283, 566, 43, 29, theme.Ink, true);
            var fixtureContent = Scroll(parent, "FixturesScroll", 923, 339, 568, 180);
            var fixtureRow = Rect(fixtureContent, "FixtureRowTemplate", 0, 0, 554, 84);
            var fixtureBg = fixtureRow.gameObject.AddComponent<Image>(); fixtureBg.color = theme.White;
            var fixture = fixtureRow.gameObject.AddComponent<HubFixtureRow>(); properties = new SerializedObject(fixture);
            Set(properties, "theme", theme); Set(properties, "background", fixtureBg);
            Set(properties, "date", Label(fixtureRow, "Date", null, 12, 5, 524, 28, 17, theme.Muted));
            Set(properties, "home", Label(fixtureRow, "Home", null, 12, 34, 220, 40, 22, theme.Ink));
            Set(properties, "score", Label(fixtureRow, "Score", null, 233, 34, 70, 40, 22, theme.Ink, true, TextAlignmentOptions.Center));
            Set(properties, "away", Label(fixtureRow, "Away", null, 307, 34, 233, 40, 22, theme.Ink, false, TextAlignmentOptions.Right));
            properties.ApplyModifiedPropertiesWithoutUndo(); fixtureRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 84;
            fixtureRow.gameObject.SetActive(false); Set(view, "fixtureTemplate", fixture);
            Label(parent, "NextFixtureLabel", "hub.nextFixture", 925, 570, 540, 37, 23, theme.Accent, true);
            Set(view, "nextFixtureText", Label(parent, "NextFixture", null, 925, 610, 562, 71, 25, theme.White));
            Set(view, "playFixtureButton", Button(parent, "PlayFixture", "hub.play", 925, 690, 567, 63, theme.Accent, theme.Ink, 26));
        }

        private static void CreateCareer(Transform parent, GameHubView component)
        {
            Panel(parent, "CareerSurface", 80, 170, 1440, 580, theme.Surface);
            var preview = Avatar(parent, "CareerPortrait", 126, 215, 275, 286, "coach-1"); Set(view, "careerPortrait", preview);
            Label(parent, "AppearanceLabel", "career.avatar", 126, 520, 300, 37, 24, theme.Muted, true);
            var buttons = view.FindProperty("avatarButtons"); var frames = view.FindProperty("avatarSelectionFrames"); buttons.arraySize = 3; frames.arraySize = 3;
            for (var index = 0; index < 3; index++)
            {
                var button = Button(parent, "Avatar" + (index + 1), theme.Portraits[index].LabelKey, 126 + index * 114, 569, 103, 66, theme.White, theme.Ink, 19);
                buttons.GetArrayElementAtIndex(index).objectReferenceValue = button;
                frames.GetArrayElementAtIndex(index).objectReferenceValue = button.GetComponent<Image>();
                UnityEventTools.AddIntPersistentListener(button.onClick, component.SelectAvatar, index);
            }
            Set(view, "careerSummary", Label(parent, "SavedCareer", null, 126, 657, 352, 80, 18, theme.Muted));
            Set(view, "coachName", Input(parent, "CoachName", "career.name", "career.namePlaceholder", 519, 220, 938));
            Set(view, "careerClubDropdown", Dropdown(parent, "CareerClub", "hub.club", 519, 361, 938));
            Set(view, "careerMonthDropdown", Dropdown(parent, "CareerMonth", "career.month", 519, 502, 452));
            var yearInput = Input(parent, "CareerYear", "career.year", "career.yearHint", 1003, 502, 454);
            yearInput.contentType = TMP_InputField.ContentType.IntegerNumber; yearInput.characterLimit = 4;
            Set(view, "careerYearInput", yearInput);
            Set(view, "saveCareerButton", Button(parent, "SaveCareer", "career.save", 519, 642, 440, 67, theme.Primary, theme.White, 27));
            Label(parent, "CareerScope", "career.limit", 997, 638, 464, 83, 19, theme.Muted);
        }

        private static void CreateConfirmation(Transform parent)
        {
            var overlay = Page(parent, "Confirmation");
            var mask = overlay.AddComponent<Image>(); mask.color = new Color(0.025f, 0.08f, 0.1f, 0.8f);
            Panel(overlay.transform, "Dialog", 350, 247, 900, 398, theme.Surface);
            Set(view, "confirmationTitle", Label(overlay.transform, "Title", null, 400, 292, 800, 70, 34, theme.Ink, true));
            Set(view, "confirmationMessage", Label(overlay.transform, "Message", null, 402, 376, 786, 112, 26, theme.Muted));
            var cancel = Button(overlay.transform, "Cancel", "dialog.cancel", 662, 533, 245, 65, theme.White, theme.Ink);
            var confirm = Button(overlay.transform, "Confirm", "dialog.confirm", 937, 533, 260, 65, theme.Danger, theme.White);
            var nav = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = confirm, selectOnRight = confirm, selectOnUp = confirm, selectOnDown = confirm }; cancel.navigation = nav;
            nav.selectOnLeft = cancel; nav.selectOnRight = cancel; nav.selectOnUp = cancel; nav.selectOnDown = cancel; confirm.navigation = nav;
            Set(view, "confirmButton", confirm); Set(view, "cancelButton", cancel); Set(view, "confirmationPanel", overlay);
            overlay.SetActive(false);
        }

        private static void MenuCard(Transform parent, string name, string title, string description, float x, float y, UnityAction action)
        {
            var card = Card(parent, name, x, y, 732, 122, theme.Surface, action);
            Label(card.transform, "Title", title, 30, 16, 596, 46, 31, theme.Ink, true);
            Label(card.transform, "Description", description, 32, 66, 636, 39, 23, theme.Muted);
            Label(card.transform, "Arrow", null, 665, 36, 40, 46, 35, theme.Primary).text = "→";
        }

        private static Button Card(Transform parent, string name, float x, float y, float width, float height, Color color, UnityAction action)
        {
            var image = Panel(parent, name, x, y, width, height, color);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            Colors(button); UnityEventTools.AddPersistentListener(button.onClick, action);
            return button;
        }

        private static Button Button(Transform parent, string name, string key, float x, float y, float width, float height, Color background, Color foreground, float size = 26)
        {
            var image = Panel(parent, name, x, y, width, height, background);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; Colors(button);
            Label(button.transform, "Label", key, 14, 6, width - 28, height - 12, size, foreground, true, TextAlignmentOptions.Center);
            return button;
        }

        private static void Colors(Selectable selectable)
        {
            var colors = selectable.colors;
            colors.normalColor = Color.white; colors.highlightedColor = theme.Hover; colors.selectedColor = theme.Selected;
            colors.pressedColor = theme.Pressed; colors.disabledColor = theme.Disabled; colors.fadeDuration = 0.08f;
            selectable.colors = colors;
        }

        private static TMP_Dropdown Dropdown(Transform parent, string name, string label, float x, float y, float width)
        {
            Label(parent, name + "Label", label, x, y, width, 35, 24, theme.Ink, true);
            var root = TMP_DefaultControls.CreateDropdown(controls); root.name = name; root.transform.SetParent(parent, false);
            Place(root.GetComponent<RectTransform>(), x, y + 43, width, 61);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) StyleText(text, 26, theme.Ink);
            var dropdown = root.GetComponent<TMP_Dropdown>(); dropdown.ClearOptions(); dropdown.AddOptions(new List<string> { "—" });
            Colors(dropdown); root.GetComponent<Image>().color = theme.White;
            BindTheme(root.GetComponent<Image>(), theme.White);
            BindTheme(dropdown.template.GetComponent<Image>(), theme.Surface);
            dropdown.template.sizeDelta = new Vector2(0, 290);
            // The template is inactive; GetComponentInParent without includeInactive skips its Toggle.
            var item = dropdown.itemText.transform.parent.GetComponent<RectTransform>(); item.sizeDelta = new Vector2(0, 48);
            var content = item.parent as RectTransform; content.sizeDelta = new Vector2(0, 54);
            dropdown.itemText.rectTransform.offsetMin = new Vector2(32, 2); dropdown.itemText.rectTransform.offsetMax = new Vector2(-14, -2);
            dropdown.captionText.rectTransform.offsetMin = new Vector2(18, 5); dropdown.captionText.rectTransform.offsetMax = new Vector2(-48, -5);
            BindTheme(root.transform.Find("Arrow").GetComponent<Image>(), theme.Primary);
            BindTheme(dropdown.template.Find("Viewport/Content/Item/Item Checkmark").GetComponent<Image>(), theme.Primary);
            return dropdown;
        }

        private static TMP_InputField Input(Transform parent, string name, string label, string placeholder, float x, float y, float width)
        {
            Label(parent, name + "Label", label, x, y, width, 35, 24, theme.Ink, true);
            var root = TMP_DefaultControls.CreateInputField(controls); root.name = name; root.transform.SetParent(parent, false);
            Place(root.GetComponent<RectTransform>(), x, y + 43, width, 61);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) StyleText(text, 27, theme.Ink);
            var input = root.GetComponent<TMP_InputField>(); input.characterLimit = 100; input.lineType = TMP_InputField.LineType.SingleLine;
            BindTheme(root.GetComponent<Image>(), theme.White);
            input.richText = false; input.textComponent.richText = false; input.fontAsset = theme.Font; input.textComponent.font = theme.Font; Colors(input);
            var hint = input.placeholder.GetComponent<TMP_Text>(); hint.color = theme.Muted; hint.fontStyle = FontStyles.Normal; BindTheme(hint, theme.Muted, true);
            Localize(hint, placeholder);
            return input;
        }

        private static RectTransform Scroll(Transform parent, string name, float x, float y, float width, float height)
        {
            var root = Rect(parent, name, x, y, width, height); var image = root.gameObject.AddComponent<Image>(); image.color = new Color(1, 1, 1, 0.01f);
            root.gameObject.AddComponent<RectMask2D>(); var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 36;
            var content = Rect(root, "Content", 0, 0, width - 15, 0); content.anchorMax = new Vector2(1, 1); content.sizeDelta = new Vector2(-15, 0);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 6; layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scrollbarObject = DefaultControls.CreateScrollbar(new DefaultControls.Resources { standard = theme.RoundedPanel, background = theme.RoundedPanel }); scrollbarObject.transform.SetParent(parent, false); scrollbarObject.name = name + "Scrollbar";
            var scrollbar = scrollbarObject.GetComponent<Scrollbar>(); scrollbar.SetDirection(Scrollbar.Direction.BottomToTop, true); scrollbarObject.GetComponent<Image>().color = theme.Line; scrollbar.targetGraphic.color = theme.Primary;
            // SetDirection flips the RectTransform axes; place the vertical bar after that conversion.
            Place(scrollbarObject.GetComponent<RectTransform>(), x + width - 9, y, 9, height);
            scroll.content = content; scroll.viewport = root; scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            return content;
        }

        private static TMP_Text TableCell(Transform parent, string name, string key, float x, float width, float size, Color color, bool center = false)
        { return Label(parent, name, key, x, 0, width, 44, size, color, false, center ? TextAlignmentOptions.Center : TextAlignmentOptions.Left); }

        private static TMP_Text Label(Transform parent, string name, string key, float x, float y, float width, float height, float size, Color color, bool bold = false, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var rect = Rect(parent, name, x, y, width, height); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            StyleText(text, size, color); text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal; text.alignment = alignment;
            text.enableAutoSizing = true; text.fontSizeMin = Mathf.Max(16, size - 6); text.fontSizeMax = size;
            if (key != null) Localize(text, key); else text.text = string.Empty;
            return text;
        }

        private static void StyleText(TMP_Text text, float size, Color color)
        { text.font = theme.Font; text.fontSize = size; text.color = color; text.richText = false; text.raycastTarget = false; text.overflowMode = TextOverflowModes.Ellipsis; BindTheme(text, color, true); }

        private static void Localize(TMP_Text text, string key)
        {
            var localized = text.GetComponent<LocalizedText>() ?? text.gameObject.AddComponent<LocalizedText>();
            var serialized = new SerializedObject(localized); Set(serialized, "target", text); serialized.FindProperty("key").stringValue = key; serialized.ApplyModifiedPropertiesWithoutUndo();
            text.text = GameHubTextSeed.English(key);
        }

        private static CoachAvatarView Avatar(Transform parent, string name, float x, float y, float width, float height, string id)
        {
            var frame = Rect(parent, name, x, y, width, height); var portrait = frame.gameObject.AddComponent<CoachAvatarView>();
            var color = theme.Portraits.First(item => item.Id == id);
            var backdrop = Panel(frame, "PortraitCard", 0, 0, width, height, theme.Accent);
            var shirt = Panel(frame, "Jacket", width * .16f, height * .6f, width * .68f, height * .34f, color.Shirt);
            var neck = Panel(frame, "Neck", width * .415f, height * .46f, width * .17f, height * .22f, color.Skin);
            var face = Panel(frame, "Face", width * .28f, height * .16f, width * .44f, height * .43f, color.Skin); face.sprite = theme.Circle;
            var hair = Panel(frame, "Hair", width * .275f, height * .12f, width * .45f, height * .19f, color.Hair); hair.sprite = theme.Circle; hair.gameObject.SetActive(color.HasHair);
            Panel(frame, "EyeLeft", width * .38f, height * .35f, width * .035f, height * .022f, theme.Ink);
            Panel(frame, "EyeRight", width * .585f, height * .35f, width * .035f, height * .022f, theme.Ink);
            Panel(frame, "CollarLeft", width * .34f, height * .63f, width * .145f, height * .05f, theme.White).rectTransform.localRotation = Quaternion.Euler(0, 0, -28);
            Panel(frame, "CollarRight", width * .515f, height * .63f, width * .145f, height * .05f, theme.White).rectTransform.localRotation = Quaternion.Euler(0, 0, 28);
            foreach (var image in frame.GetComponentsInChildren<Image>(true)) image.raycastTarget = false;
            var serialized = new SerializedObject(portrait); Set(serialized, "theme", theme); Set(serialized, "face", face); Set(serialized, "neck", neck); Set(serialized, "hair", hair); Set(serialized, "shirt", shirt); serialized.FindProperty("avatarId").stringValue = id; serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (var image in new[] { face, neck, hair, shirt })
            { var binding = image.GetComponent<GameHubThemeBinding>(); if (binding != null) UnityEngine.Object.DestroyImmediate(binding); }
            return portrait;
        }

        private static void Pitch(Transform parent)
        {
            var pitch = Rect(parent, "TacticalPitch", 900, -130, 660, 1100); pitch.localRotation = Quaternion.Euler(0, 0, -15);
            var tint = new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, .08f);
            PitchLines(pitch, 660, 1100, tint);
        }

        private static void PitchInset(Transform parent, float x, float y, float width, float height)
        { var pitch = Rect(parent, "PitchDetail", x, y, width, height); PitchLines(pitch, width, height, theme.Line); }

        private static void PitchLines(Transform parent, float width, float height, Color tint)
        {
            Panel(parent, "LeftLine", 0, 0, 3, height, tint); Panel(parent, "RightLine", width - 3, 0, 3, height, tint);
            Panel(parent, "TopLine", 0, 0, width, 3, tint); Panel(parent, "BottomLine", 0, height - 3, width, 3, tint);
            Panel(parent, "HalfwayLine", 0, height / 2, width, 3, tint);
            var center = Panel(parent, "CenterSpot", width / 2 - 6, height / 2 - 6, 12, 12, tint); center.sprite = theme.Circle;
            foreach (var image in parent.GetComponentsInChildren<Image>(true)) image.raycastTarget = false;
        }

        private static Image Panel(Transform parent, string name, float x, float y, float width, float height, Color color)
        { var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>(); image.sprite = theme.RoundedPanel; image.type = Image.Type.Sliced; image.color = color; BindTheme(image, color); return image; }

        private static void BindTheme(Graphic graphic, Color color, bool font = false)
        {
            var colors = new[] { theme.Background, theme.Surface, theme.White, theme.Ink, theme.Muted, theme.Primary, theme.Accent, theme.Line, theme.Danger,
                theme.InspectionPanel, theme.InspectionRow, theme.InspectionText, theme.InspectionMuted, theme.PitchSurface, theme.PitchMarking };
            var index = Array.FindIndex(colors, value => value.Equals(color));
            if (index < 0) return;
            var binding = graphic.GetComponent<GameHubThemeBinding>() ?? graphic.gameObject.AddComponent<GameHubThemeBinding>();
            binding.Theme = theme; binding.Role = (GameHubColorRole)index; binding.UseFont = font; binding.Apply();
        }

        private static GameObject Page(Transform parent, string name)
        { var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup)); root.layer = 5; root.transform.SetParent(parent, false); Stretch(root.GetComponent<RectTransform>()); return root; }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        { var root = new GameObject(name, typeof(RectTransform)); root.layer = 5; root.transform.SetParent(parent, false); var rect = root.GetComponent<RectTransform>(); Place(rect, x, y, width, height); return rect; }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        { rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); rect.localScale = Vector3.one; }

        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; }

        private static void Set(SerializedObject serialized, string field, UnityEngine.Object value)
        { var property = serialized.FindProperty(field); if (property == null) throw new InvalidOperationException("Missing serialized UI field: " + field); property.objectReferenceValue = value; }

        private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }

        private static void LocalizeLegacyPrefabs()
        {
            var paths = new[]
            {
                "Assets/FootballSimulator/Arts/UI/Panels/MainMenuPanel.prefab", "Assets/FootballSimulator/Arts/UI/Panels/MatchSettings.prefab",
                "Assets/FootballSimulator/Arts/UI/Panels/PauseMenu.prefab", "Assets/FootballSimulator/Arts/UI/MatchThemes/MatchStartPanel/UpcomingMatchPanel.prefab",
                "Assets/FootballSimulator/Arts/UI/MatchCompletedPanel/Statistics/MatchStatisticsPanel.prefab"
            };
            var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "GO TO DRESSING ROOM", "match.play" }, { "RETRY", "hub.retry" }, { "START MATCH", "match.start" }, { "BACK TO TEAMS", "match.backTeams" },
                { "DIFFICULTY", "options.difficulty" }, { "DAYTIME", "match.daytime" }, { "DAY TIME", "match.daytime" }, { "SIDE", "match.side" }, { "WHICH SIDE", "match.side" }, { "WHICH SIDE YOU WANNA CONTROL", "match.side" }, { "HOME", "match.home" }, { "AWAY", "match.away" },
                { "LEAVE MATCH", "match.leave" }, { "LEAVE_MATCH", "match.leave" }, { "LEAVE", "match.leave" }, { "RESUME", "match.resume" }, { "RETURN TO MENU", "hub.home" }, { "GO BACK TO MENU", "hub.home" },
                { "CAMERA", "options.camera" }, { "CÂMERA", "options.camera" }, { "QUALITY", "match.quality" }, { "GRAPHICS QUALITY", "match.quality" }, { "LINE UP", "match.lineup" }
            };
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path); var changed = false;
                try
                {
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (string.IsNullOrWhiteSpace(text.text)) continue;
                        var normalizedText = Regex.Replace(text.text.Trim(), @"\s+", " ");
                        if (!keys.TryGetValue(normalizedText, out var key)) continue;
                        var existing = text.GetComponent<LocalizedText>();
                        if (existing != null) continue;
                        Localize(text, key); changed = true;
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
    }
}
