using FStudio.FootballWorld.Presentation;
using FStudio.FootballWorld.Infrastructure.GameModes;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Editor
{
    public static partial class GameHubAuthoring
    {
        // Incremental authoring: pages and row templates are saved as editable UGUI objects.
        private static void EnsureCareerManagement()
        {
            var hub = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var component = hub.GetComponent<GameHubView>();
                var properties = new SerializedObject(component);
                if (properties.FindProperty("careerSquadPage").objectReferenceValue == null)
                {
                    var page = ManagementPage(hub.transform, "CareerSquad");
                    var squad = new SerializedObject(page.AddComponent<GameHubCareerSquad>());
                    Label(page.transform, "Scope", "career.squadScope", 82, 151, 1420, 44, 24, theme.Accent);
                    PlayerBrowser(page.transform, squad, false);
                    squad.ApplyModifiedPropertiesWithoutUndo(); page.SetActive(false); Set(properties, "careerSquadPage", page);
                }
                if (properties.FindProperty("careerMarketPage").objectReferenceValue == null)
                {
                    var page = ManagementPage(hub.transform, "CareerMarket");
                    var market = new SerializedObject(page.AddComponent<GameHubCareerMarket>());
                    Set(market, "hub", component); Set(market, "theme", theme);
                    Set(market, "searchTab", Button(page.transform, "SearchTab", "career.searchPlayers", 80, 153, 352, 48, theme.Accent, theme.Ink, 24));
                    Set(market, "offersTab", Button(page.transform, "OffersTab", "career.offers", 451, 153, 292, 48, theme.Surface, theme.Ink, 24));
                    Label(page.transform, "Scope", "career.marketScope", 771, 149, 747, 57, 21, theme.Accent);
                    var search = Page(page.transform, "SearchPlayers"); Set(market, "searchPanel", search);
                    PlayerBrowser(search.transform, market, true);
                    var offers = Page(page.transform, "Offers"); Set(market, "offersPanel", offers);
                    CreateOffers(offers.transform, market); offers.SetActive(false);
                    market.ApplyModifiedPropertiesWithoutUndo(); page.SetActive(false); Set(properties, "careerMarketPage", page);
                }
                if (properties.FindProperty("careerTacticsPage").objectReferenceValue == null)
                {
                    var page = ManagementPage(hub.transform, "CareerTactics");
                    var tactics = new SerializedObject(page.AddComponent<GameHubCareerTactics>());
                    Label(page.transform, "Scope", "career.tacticsScope", 82, 151, 1420, 44, 24, theme.Accent);
                    Panel(page.transform, "TacticsSurface", 80, 207, 620, 555, theme.Surface);
                    Set(tactics, "selectedSummary", Label(page.transform, "SavedTactics", null, 108, 225, 563, 48, 24, theme.Ink, true));
                    Set(tactics, "formation", Dropdown(page.transform, "Formation", "career.formation", 108, 296, 563));
                    Set(tactics, "mentality", Dropdown(page.transform, "Mentality", "career.mentality", 108, 424, 563));
                    Label(page.transform, "ApplicationHint", "career.tacticsEffect", 110, 552, 560, 65, 21, theme.Muted);
                    Set(tactics, "apply", Button(page.transform, "ApplyTactics", "career.applyTactics", 108, 637, 563, 62, theme.Primary, theme.White, 26));
                    Set(tactics, "feedback", Label(page.transform, "TacticsFeedback", null, 110, 710, 560, 37, 21, theme.Muted));
                    Panel(page.transform, "LineupSurface", 725, 207, 795, 555, theme.Surface);
                    Label(page.transform, "LineupTitle", "career.automaticXI", 752, 229, 735, 48, 29, theme.Ink, true);
                    var content = Scroll(page.transform, "LineupScroll", 752, 294, 738, 397);
                    var lineup = Label(content, "StartingPlayers", null, 0, 0, 715, 350, 26, theme.Ink);
                    ScrollText(lineup); Set(tactics, "startingLineup", lineup);
                    Label(page.transform, "LineupHint", "career.lineupHint", 754, 706, 730, 44, 21, theme.Muted);
                    tactics.ApplyModifiedPropertiesWithoutUndo(); page.SetActive(false); Set(properties, "careerTacticsPage", page);
                }
                var marketClubLabel = hub.transform.Find("CareerMarket/SearchPlayers/ClubFilterLabel");
                if (marketClubLabel != null && marketClubLabel.GetComponent<LocalizedText>()?.Key == "hub.club")
                    Localize(marketClubLabel.GetComponent<TMP_Text>(), "career.clubFilter");
                var office = hub.transform.Find("CareerOffice");
                if (office.Find("OpenSquad") == null)
                {
                    Place(office.Find("CareerHeading").GetComponent<RectTransform>(), 82, 151, 682, 88);
                    UnityEventTools.AddPersistentListener(Button(office, "OpenSquad", "career.squad", 787, 174, 229, 58, theme.Surface, theme.Ink, 25).onClick, component.OpenCareerSquad);
                    UnityEventTools.AddPersistentListener(Button(office, "OpenTactics", "career.tacticsShort", 1032, 174, 229, 58, theme.Surface, theme.Ink, 25).onClick, component.OpenCareerTactics);
                    UnityEventTools.AddPersistentListener(Button(office, "OpenMarket", "career.market", 1277, 174, 243, 58, theme.Accent, theme.Ink, 25).onClick, component.OpenCareerMarket);
                }
                // Space for the fee, reservation, tomorrow's decision and any recoverable validation error.
                var confirmation = hub.transform.Find("Confirmation");
                if (confirmation.Find("CareerOfferLayout") == null)
                {
                    Place(confirmation.Find("Dialog").GetComponent<RectTransform>(), 310, 187, 980, 518);
                    Place(confirmation.Find("Title").GetComponent<RectTransform>(), 350, 224, 900, 70);
                    Place(confirmation.Find("Message").GetComponent<RectTransform>(), 352, 310, 892, 263);
                    Place(confirmation.Find("Cancel").GetComponent<RectTransform>(), 622, 607, 275, 65);
                    Place(confirmation.Find("Confirm").GetComponent<RectTransform>(), 923, 607, 322, 65);
                    Place(confirmation.Find("Cancel/Label").GetComponent<RectTransform>(), 14, 6, 247, 53);
                    Place(confirmation.Find("Confirm/Label").GetComponent<RectTransform>(), 14, 6, 294, 53);
                    var marker = new GameObject("CareerOfferLayout"); marker.transform.SetParent(confirmation, false); marker.SetActive(false);
                }
                RepairVerticalScrollbarLayout(hub.transform);
                properties.ApplyModifiedPropertiesWithoutUndo();
                foreach (var child in hub.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
                PrefabUtility.SaveAsPrefabAsset(hub, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(hub); }
        }

        private static void RepairVerticalScrollbarLayout(Transform hub)
        {
            // Repair the inherited axis flip once, then retain future artist-authored dimensions.
            const string markerName = "VerticalScrollbarLayoutV2";
            if (hub.Find(markerName) != null) return;
            foreach (var pageName in new[] { "CareerSquad", "CareerTactics", "CareerMarket", "CareerOffice", "ChampionshipDashboard", "ChampionshipSelection" })
            {
                var page = hub.Find(pageName);
                if (page == null) continue;
                foreach (var scroll in page.GetComponentsInChildren<ScrollRect>(true))
                {
                    var bar = scroll.verticalScrollbar;
                    var scrollRect = scroll.GetComponent<RectTransform>();
                    if (bar == null || scrollRect == null || bar.transform.parent != scrollRect.parent) continue;
                    bar.SetDirection(Scrollbar.Direction.BottomToTop, true);
                    Place(bar.GetComponent<RectTransform>(), scrollRect.anchoredPosition.x + scrollRect.sizeDelta.x - 9,
                        -scrollRect.anchoredPosition.y, 9, scrollRect.sizeDelta.y);
                }
            }
            var marker = new GameObject(markerName); marker.transform.SetParent(hub, false); marker.SetActive(false);
        }

        private static GameObject ManagementPage(Transform parent, string name)
        {
            var page = Page(parent, name);
            page.transform.SetSiblingIndex(parent.Find("Confirmation").GetSiblingIndex());
            return page;
        }

        private static void PlayerBrowser(Transform parent, SerializedObject properties, bool market)
        {
            Panel(parent, "PlayersSurface", 80, 215, 600, 547, theme.Surface);
            Panel(parent, "PlayerSurface", 705, 215, 815, 547, theme.Surface);
            Set(properties, "search", Input(parent, "PlayerSearch", "career.searchName", "career.searchPlaceholder", 106, 232, 410));
            Set(properties, "clear", Button(parent, "ClearSearch", "career.clearFilters", 534, 275, 120, 61, theme.White, theme.Ink, 22));
            Set(properties, "positionFilter", Dropdown(parent, "PositionFilter", "career.position", 106, 352, market ? 263 : 548));
            if (market) Set(properties, "clubFilter", Dropdown(parent, "ClubFilter", "career.clubFilter", 391, 352, 263));
            Set(properties, "resultCount", Label(parent, "PlayerCount", null, 108, 470, 542, 30, 22, theme.Muted));
            var content = Scroll(parent, "PlayerScroll", 106, 509, 548, 179);
            Set(properties, "playerScroll", content.parent.GetComponent<ScrollRect>());
            var row = Panel(content, "PlayerTemplate", 0, 0, 529, 78, theme.White);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 78;
            var button = row.gameObject.AddComponent<Button>(); button.targetGraphic = row; Colors(button);
            var rowProperties = new SerializedObject(row.gameObject.AddComponent<HubCareerPlayerRow>());
            Set(rowProperties, "select", button); Set(rowProperties, "background", row); Set(rowProperties, "theme", theme);
            Set(rowProperties, "playerName", Label(row.transform, "Name", null, 15, 7, 493, 34, 27, theme.Ink, true));
            Set(rowProperties, "description", Label(row.transform, "Description", null, 17, 44, 490, 27, 21, theme.Muted));
            rowProperties.ApplyModifiedPropertiesWithoutUndo(); row.gameObject.SetActive(false);
            Set(properties, "rowTemplate", row.GetComponent<HubCareerPlayerRow>());
            Set(properties, "emptyState", Label(parent, "NoPlayers", "career.noPlayers", 120, 530, 510, 104, 25, theme.Muted));
            Set(properties, "previous", Button(parent, "PreviousPlayers", "career.previous", 106, 707, 151, 42, theme.White, theme.Ink, 21));
            Set(properties, "pageCount", Label(parent, "PlayerPageCount", null, 266, 708, 226, 39, 21, theme.Ink, false, TextAlignmentOptions.Center));
            Set(properties, "next", Button(parent, "NextPlayers", "career.next", 503, 707, 151, 42, theme.White, theme.Ink, 21));
            var details = Rect(parent, "PlayerDetails", 733, 231, 761, 353);
            var detailProperties = new SerializedObject(details.gameObject.AddComponent<GameHubCareerPlayerDetails>());
            Set(detailProperties, "playerName", Label(details, "PlayerName", null, 0, 0, 753, 46, 33, theme.Ink, true));
            Set(detailProperties, "club", Label(details, "ClubAndPosition", null, 2, 49, 750, 29, 23, theme.Primary));
            Set(detailProperties, "biography", Label(details, "Biography", null, 2, 86, 750, 70, 22, theme.Muted));
            Label(details, "AttributesTitle", "career.attributes", 2, 163, 747, 30, 22, theme.Ink, true);
            var attributes = new[] { "Strength", "Acceleration", "TopSpeed", "DribbleSpeed", "Jump", "Tackling", "BallKeeping", "Passing", "LongBall", "Agility", "Shooting", "ShootPower", "Positioning", "Reaction", "BallControl" };
            var values = detailProperties.FindProperty("attributeValues"); values.arraySize = attributes.Length;
            for (var index = 0; index < attributes.Length; index++)
            {
                var x = index / 5 * 255; var y = 202 + index % 5 * 31;
                Label(details, attributes[index] + "Label", "attribute." + attributes[index], x + 2, y, 190, 29, 20, theme.Muted);
                values.GetArrayElementAtIndex(index).objectReferenceValue = Label(details, attributes[index] + "Value", null, x + 200, y, 40, 29, 24, theme.Ink, true, TextAlignmentOptions.Right);
            }
            detailProperties.ApplyModifiedPropertiesWithoutUndo(); Set(properties, "details", details.GetComponent<GameHubCareerPlayerDetails>());
            if (market)
            {
                var amount = Input(parent, "OfferAmount", "career.offerAmount", "career.amountPlaceholder", 735, 592, 421);
                amount.contentType = TMP_InputField.ContentType.IntegerNumber; amount.characterLimit = 10;
                Set(properties, "amount", amount);
                Set(properties, "submit", Button(parent, "SubmitOffer", "career.makeOffer", 1175, 635, 313, 61, theme.Primary, theme.White, 25));
                Set(properties, "offerHint", Label(parent, "OfferHint", null, 737, 703, 750, 51, 20, theme.Muted));
            }
            else Label(parent, "SquadHint", "career.squadDetailsHint", 737, 617, 750, 119, 24, theme.Muted);
        }

        private static void CreateOffers(Transform parent, SerializedObject properties)
        {
            Panel(parent, "OffersSurface", 80, 215, 1440, 547, theme.Surface);
            Set(properties, "budget", Label(parent, "TransferBudget", null, 108, 234, 1385, 67, 26, theme.Ink, true));
            Set(properties, "offerCount", Label(parent, "OfferCount", null, 108, 310, 1380, 32, 23, theme.Muted));
            var content = Scroll(parent, "OffersScroll", 106, 354, 1387, 337);
            Set(properties, "offerScroll", content.parent.GetComponent<ScrollRect>());
            var row = Panel(content, "OfferTemplate", 0, 0, 1360, 106, theme.White);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 106;
            var offer = new SerializedObject(row.gameObject.AddComponent<HubCareerOfferRow>());
            Set(offer, "heading", Label(row.transform, "OfferHeading", null, 18, 9, 1110, 36, 26, theme.Ink, true));
            Set(offer, "description", Label(row.transform, "OfferDescription", null, 20, 48, 1107, 49, 21, theme.Muted));
            Set(offer, "cancel", Button(row.transform, "CancelOffer", "career.cancelOffer", 1150, 28, 186, 51, theme.Surface, theme.Ink, 21));
            offer.ApplyModifiedPropertiesWithoutUndo(); row.gameObject.SetActive(false);
            Set(properties, "offerTemplate", row.GetComponent<HubCareerOfferRow>());
            Set(properties, "emptyOffers", Label(parent, "NoOffers", "career.noOffers", 120, 397, 1360, 104, 27, theme.Muted));
            Set(properties, "previousOffers", Button(parent, "PreviousOffers", "career.previous", 108, 707, 224, 42, theme.White, theme.Ink, 22));
            Set(properties, "offerPages", Label(parent, "OfferPageCount", null, 353, 708, 895, 39, 23, theme.Ink, false, TextAlignmentOptions.Center));
            Set(properties, "nextOffers", Button(parent, "NextOffers", "career.next", 1270, 707, 224, 42, theme.White, theme.Ink, 22));
        }
    }
}
