using FStudio.FootballWorld.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Editor
{
    public static partial class GameHubAuthoring
    {
        // Adds only this feature's controls; existing artist-authored pages are retained.
        private static void EnsureCareerOffice()
        {
            var hub = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var properties = new SerializedObject(hub.GetComponent<GameHubView>());
                var careerHint = hub.transform.Find("Home/CareerCard/CareerDescription").GetComponent<TMP_Text>();
                Localize(careerHint, "career.dailyHint");
                if (properties.FindProperty("careerOfficePage").objectReferenceValue == null)
                {
                    var page = Page(hub.transform, "CareerOffice");
                    page.transform.SetSiblingIndex(hub.transform.Find("Confirmation").GetSiblingIndex());
                    var office = new SerializedObject(page.AddComponent<GameHubCareerOffice>());
                    Set(office, "heading", Label(page.transform, "CareerHeading", null, 82, 151, 1420, 88, 29, theme.White, true));
                    Panel(page.transform, "ManagementSurface", 80, 250, 682, 432, theme.Surface);
                    Set(office, "finance", Label(page.transform, "Finance", null, 108, 270, 625, 80, 24, theme.Ink, true));
                    Set(office, "fitness", Label(page.transform, "Fitness", null, 108, 352, 625, 52, 23, theme.Muted));
                    Set(office, "training", Dropdown(page.transform, "Training", "career.training", 108, 407, 625));
                    Set(office, "calendar", Label(page.transform, "NextCareerFixture", null, 108, 517, 625, 68, 23, theme.Ink));
                    Set(office, "advance", Button(page.transform, "AdvanceDay", "career.advanceDay", 108, 590, 297, 55, theme.Primary, theme.White, 24));
                    Set(office, "next", Button(page.transform, "AdvanceNext", "career.advanceNext", 425, 590, 308, 55, theme.White, theme.Ink, 23));
                    Label(page.transform, "SimulationHint", "career.simulationHint", 110, 649, 620, 28, 17, theme.Muted);

                    Panel(page.transform, "NewsSurface", 787, 250, 733, 259, theme.Surface);
                    Label(page.transform, "NewsHeading", "career.news", 813, 267, 680, 42, 27, theme.Ink, true);
                    var newsContent = Scroll(page.transform, "NewsScroll", 813, 315, 680, 175);
                    var news = Label(newsContent, "News", null, 0, 0, 655, 200, 22, theme.Ink);
                    ScrollText(news); Set(office, "news", news);
                    Panel(page.transform, "LedgerSurface", 787, 527, 733, 155, theme.Surface);
                    Label(page.transform, "LedgerHeading", "career.ledger", 813, 539, 680, 34, 22, theme.Ink, true);
                    var ledgerContent = Scroll(page.transform, "LedgerScroll", 813, 582, 680, 78);
                    var ledger = Label(ledgerContent, "Ledger", null, 0, 0, 655, 160, 20, theme.Muted);
                    ScrollText(ledger); Set(office, "ledger", ledger);

                    Set(office, "play", Button(page.transform, "PlayCareer", "hub.play", 80, 704, 332, 50, theme.Accent, theme.Ink, 25));
                    Set(office, "simulate", Button(page.transform, "SimulateCareer", "hub.simulateMatch", 432, 704, 330, 50, theme.Surface, theme.Ink, 24));
                    Set(office, "competition", Button(page.transform, "CareerCalendar", "career.calendar", 787, 704, 447, 50, theme.Surface, theme.Ink, 24));
                    Set(office, "newCareer", Button(page.transform, "NewCareer", "career.new", 1254, 704, 266, 50, theme.Surface, theme.Ink, 22));
                    office.ApplyModifiedPropertiesWithoutUndo();
                    page.SetActive(false); Set(properties, "careerOfficePage", page);
                }
                if (properties.FindProperty("simulateFixtureButton").objectReferenceValue == null)
                {
                    var parent = hub.transform.Find("ChampionshipDashboard");
                    Place(parent.Find("PlayFixture").GetComponent<RectTransform>(), 925, 690, 270, 63);
                    Set(properties, "simulateFixtureButton", Button(parent, "SimulateFixture", "hub.simulateMatch", 1210, 690, 282, 63, theme.Surface, theme.Ink, 23));
                    Place(parent.Find("NextFixture").GetComponent<RectTransform>(), 925, 605, 562, 79);
                    Label(parent, "RulesHint", "hub.rulesHint", 82, 766, 1425, 60, 18, theme.Accent);
                }
                if (properties.FindProperty("continueCareerButton").objectReferenceValue == null)
                {
                    var parent = hub.transform.Find("CareerProfile");
                    Set(properties, "continueCareerButton", Button(parent, "ContinueCareer", "career.continue", 996, 706, 465, 44, theme.White, theme.Ink, 21));
                    var scope = parent.Find("CareerScope").GetComponent<TMP_Text>();
                    Localize(scope, "career.dailyScope");
                }
                var officePage = hub.transform.Find("CareerOffice");
                var playLabel = hub.transform.Find("ChampionshipDashboard/PlayFixture/Label");
                if (playLabel != null)
                    Place(playLabel.GetComponent<RectTransform>(), 14, 6, 242, 51);
                Set(properties, "standingsTitle", hub.transform.Find("ChampionshipDashboard/StandingsTitle").GetComponent<TMP_Text>());
                Set(properties, "rulesHint", hub.transform.Find("ChampionshipDashboard/RulesHint").GetComponent<TMP_Text>());
                if (officePage != null && officePage.Find("LayoutVersion2") == null)
                {
                    var positions = new[] {
                        new object[] { "ManagementSurface", 80f,250f,682f,432f },
                        new object[] { "Finance",108f,270f,625f,80f }, new object[] { "Fitness",108f,352f,625f,52f },
                        new object[] { "TrainingLabel",108f,407f,625f,35f }, new object[] { "Training",108f,450f,625f,61f },
                        new object[] { "NextCareerFixture",108f,517f,625f,68f },
                        new object[] { "AdvanceDay",108f,590f,297f,55f }, new object[] { "AdvanceNext",425f,590f,308f,55f },
                        new object[] { "SimulationHint",110f,649f,620f,28f }, new object[] { "NewsSurface",787f,250f,733f,259f },
                        new object[] { "NewsScroll",813f,315f,680f,175f }, new object[] { "NewsScrollScrollbar",1484f,315f,9f,175f },
                        new object[] { "LedgerSurface",787f,527f,733f,155f }, new object[] { "LedgerHeading",813f,539f,680f,34f },
                        new object[] { "LedgerScroll",813f,582f,680f,78f }, new object[] { "LedgerScrollScrollbar",1484f,582f,9f,78f },
                        new object[] { "PlayCareer",80f,704f,332f,50f }, new object[] { "SimulateCareer",432f,704f,330f,50f },
                        new object[] { "CareerCalendar",787f,704f,447f,50f }, new object[] { "NewCareer",1254f,704f,266f,50f }
                    };
                    foreach (var item in positions)
                        Place(officePage.Find((string)item[0]).GetComponent<RectTransform>(), (float)item[1], (float)item[2], (float)item[3], (float)item[4]);
                    var marker = new GameObject("LayoutVersion2"); marker.transform.SetParent(officePage, false); marker.SetActive(false);
                }
                properties.ApplyModifiedPropertiesWithoutUndo();
                foreach (var child in hub.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
                PrefabUtility.SaveAsPrefabAsset(hub, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(hub); }
        }

        private static void ScrollText(TMP_Text text)
        {
            text.enableAutoSizing = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableWordWrapping = true;
            // VerticalLayoutGroup reads TMP's preferred height; no runtime geometry is authored in code.
            text.gameObject.AddComponent<LayoutElement>().minHeight = 74;
        }
    }
}
