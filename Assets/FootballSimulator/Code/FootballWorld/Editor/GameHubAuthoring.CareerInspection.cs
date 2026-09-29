using System;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.GameModes;
using FStudio.FootballWorld.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Editor
{
    public static partial class GameHubAuthoring
    {
        // One-time migration: existing controls, events, references and scrollbar repairs survive.
        // Later artist edits are preserved by the marker; runtime never constructs this geometry.
        private static void EnsureCareerInspectionLayout()
        {
            EnsureCareerInspectionLayoutV1();
            EnsureCareerInspectionPolish();
            EnsureCareerTacticsInteraction();
        }

        private static void EnsureCareerInspectionLayoutV1()
        {
            var hub = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                const string markerName = "CareerInspectionLayoutV1";
                if (hub.transform.Find(markerName) != null) return;
                AuthorTacticalInspection(hub.transform.Find("CareerTactics"));
                AuthorPlayerInspection(hub.transform.Find("CareerSquad"), false);
                AuthorPlayerInspection(hub.transform.Find("CareerMarket/SearchPlayers"), true);
                var marker = new GameObject(markerName); marker.transform.SetParent(hub.transform, false); marker.SetActive(false);
                foreach (var child in hub.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
                PrefabUtility.SaveAsPrefabAsset(hub, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(hub); }
        }

        private static void EnsureCareerInspectionPolish()
        {
            var hub = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                const string markerName = "CareerInspectionPolishV2";
                if (hub.transform.Find(markerName) != null) return;
                var hint = hub.transform.Find("CareerTactics/ApplicationHint").GetComponent<TMP_Text>();
                Localize(hint, "career.tacticsEffectCompact");
                foreach (var path in new[] { "CareerSquad", "CareerMarket/SearchPlayers" })
                {
                    var page = hub.transform.Find(path);
                    InspectionText(page, "PreviousPlayers/Label", 20, theme.Ink);
                    InspectionText(page, "NextPlayers/Label", 20, theme.Ink);
                }
                var marker = new GameObject(markerName); marker.transform.SetParent(hub.transform, false);
                marker.layer = 5; marker.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(hub, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(hub); }
        }

        private static void EnsureCareerTacticsInteraction()
        {
            var hub = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                const string markerName = "CareerTacticsInteractionV3";
                if (hub.transform.Find(markerName) != null) return;
                var page = hub.transform.Find("CareerTactics");
                var board = page.Find("TacticalBoard").GetComponent<CareerTacticalBoard>();
                var boardData = new SerializedObject(board);
                var pitch = page.Find("TacticalBoard/Field").GetComponent<RectTransform>();
                Set(boardData, "pitch", pitch);
                Localize(page.Find("Scope").GetComponent<TMP_Text>(), "career.tacticsInteractiveHelp");
                InspectionText(page, "SavedTactics", 102, 220, 306, 46, 20, theme.InspectionText);
                InspectionText(page, "FormationLabel", 102, 274, 306, 25, 20, theme.InspectionText);
                InspectionMove(page, "Formation", 102, 300, 306, 44);
                InspectionText(page, "MentalityLabel", 102, 352, 306, 25, 20, theme.InspectionText);
                InspectionMove(page, "Mentality", 102, 378, 306, 44);
                var role = Dropdown(page, "TacticalRole", null, 102, 434, 306);
                InspectionMove(page, "TacticalRole", 102, 467, 306, 43);
                foreach (var text in role.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.fontSize = 20; text.fontSizeMin = 16; text.fontSizeMax = 20; text.enableAutoSizing = true;
                }
                var selectedLabel = InspectionText(page, "TacticalRoleLabel", 102, 434, 306, 30, 20, theme.InspectionText);
                var effect = Label(page, "TacticalRoleEffect", null, 104, 518, 302, 51, 18, theme.InspectionMuted);
                var reset = Button(page, "ResetTacticalSlot", "career.resetTacticSlot", 102, 577, 146, 34, theme.White, theme.Ink, 16);
                var resetAll = Button(page, "ResetTacticalPlan", "career.resetTacticPlan", 258, 577, 150, 34, theme.White, theme.Ink, 16);
                var hint = InspectionText(page, "ApplicationHint", 104, 616, 302, 36, 16, theme.InspectionMuted);
                Localize(hint, "career.tacticsKeyboard");
                InspectionMove(page, "ApplyTactics", 102, 660, 306, 48);
                InspectionMove(page, "ApplyTactics/Label", 12, 5, 282, 38);
                InspectionText(page, "TacticsFeedback", 104, 715, 302, 39, 18, theme.InspectionMuted);
                Set(boardData, "role", role); Set(boardData, "selectedSlotLabel", selectedLabel); Set(boardData, "roleEffect", effect);
                Set(boardData, "resetSlot", reset); Set(boardData, "resetAll", resetAll);
                var rows = boardData.FindProperty("lineupButtons"); var highlights = boardData.FindProperty("lineupHighlights");
                rows.arraySize = highlights.arraySize = 11;
                for (var i = 0; i < 11; i++)
                {
                    var row = page.Find("PreviewPlayer" + (i + 1).ToString("00"));
                    var button = row.gameObject.AddComponent<Button>(); button.targetGraphic = row.GetComponent<Image>(); Colors(button);
                    var outline = row.gameObject.AddComponent<Outline>(); outline.effectColor = theme.Accent; outline.effectDistance = new Vector2(2, -2); outline.enabled = false;
                    rows.GetArrayElementAtIndex(i).objectReferenceValue = button; highlights.GetArrayElementAtIndex(i).objectReferenceValue = outline;
                }
                var layouts = boardData.FindProperty("layouts");
                for (var f = 0; f < layouts.arraySize; f++)
                {
                    var layout = layouts.GetArrayElementAtIndex(f);
                    var root = ((GameObject)layout.FindPropertyRelative("Root").objectReferenceValue).transform;
                    var markers = layout.FindPropertyRelative("Markers"); var selectionOutlines = layout.FindPropertyRelative("SelectionOutlines");
                    markers.arraySize = selectionOutlines.arraySize = 11;
                    for (var i = 0; i < 11; i++)
                    {
                        var markerRoot = root.Find("Slot" + (i + 1).ToString("00"));
                        var hitArea = markerRoot.gameObject.AddComponent<Image>(); hitArea.color = Color.clear; hitArea.raycastTarget = true;
                        var input = markerRoot.gameObject.AddComponent<CareerTacticalSlot>();
                        input.targetGraphic = markerRoot.Find("PositionMarker").GetComponent<Image>(); Colors(input);
                        var inputData = new SerializedObject(input); Set(inputData, "board", board); inputData.FindProperty("slotIndex").intValue = i; inputData.ApplyModifiedPropertiesWithoutUndo();
                        var outline = markerRoot.Find("NameSurface").gameObject.AddComponent<Outline>();
                        outline.effectColor = theme.Accent; outline.effectDistance = new Vector2(2, -2); outline.enabled = false;
                        markers.GetArrayElementAtIndex(i).objectReferenceValue = input;
                        selectionOutlines.GetArrayElementAtIndex(i).objectReferenceValue = outline;
                    }
                }
                boardData.ApplyModifiedPropertiesWithoutUndo();
                var marker = new GameObject(markerName); marker.transform.SetParent(hub.transform, false); marker.SetActive(false);
                foreach (var child in page.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
                marker.layer = 5;
                PrefabUtility.SaveAsPrefabAsset(hub, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(hub); }
        }

        private static void AuthorTacticalInspection(Transform page)
        {
            if (page == null) throw new InvalidOperationException("Career tactics must be authored before its inspection layout.");
            var scope = InspectionText(page, "Scope", 82, 146, 1432, 49, 21, theme.Accent); Localize(scope, "career.previewHelp");
            InspectionMove(page, "TacticsSurface", 80, 207, 350, 555); InspectionColor(page.Find("TacticsSurface"), theme.InspectionPanel);
            InspectionText(page, "SavedTactics", 102, 225, 306, 78, 24, theme.InspectionText);
            InspectionText(page, "FormationLabel", 102, 319, 306, 34, 24, theme.InspectionText);
            InspectionMove(page, "Formation", 102, 357, 306, 61);
            InspectionText(page, "MentalityLabel", 102, 438, 306, 34, 24, theme.InspectionText);
            InspectionMove(page, "Mentality", 102, 476, 306, 61);
            InspectionText(page, "ApplicationHint", 104, 554, 302, 72, 21, theme.InspectionMuted);
            InspectionMove(page, "ApplyTactics", 102, 641, 306, 61);
            InspectionMove(page, "ApplyTactics/Label", 12, 5, 282, 51);
            InspectionText(page, "TacticsFeedback", 104, 711, 302, 39, 19, theme.InspectionMuted);
            InspectionMove(page, "LineupSurface", 1100, 207, 420, 555); InspectionColor(page.Find("LineupSurface"), theme.InspectionPanel);
            var title = InspectionText(page, "LineupTitle", 1120, 225, 380, 42, 27, theme.InspectionText);
            Localize(title, "career.previewPlayers");
            page.Find("LineupScroll").gameObject.SetActive(false);
            page.Find("LineupScrollScrollbar").gameObject.SetActive(false);
            InspectionText(page, "LineupHint", 1120, 719, 380, 31, 18, theme.InspectionMuted);
            Localize(page.Find("LineupHint").GetComponent<TMP_Text>(), "career.previewPositions");
            var boardRoot = Rect(page, "TacticalBoard", 450, 207, 620, 555);
            Panel(boardRoot, "BoardSurface", 0, 0, 620, 555, theme.InspectionPanel);
            Label(boardRoot, "PreviewHeading", "career.lineupPreview", 18, 16, 584, 36, 26, theme.InspectionText, true);
            var pitch = InspectionPitch(boardRoot, "Field", 18, 65, 584, 468);
            var board = boardRoot.gameObject.AddComponent<CareerTacticalBoard>();
            var properties = new SerializedObject(board);
            var names = properties.FindProperty("lineupNames"); var roles = properties.FindProperty("lineupPositions"); names.arraySize = roles.arraySize = 11;
            for (var i = 0; i < 11; i++)
            {
                var row = Panel(page, "PreviewPlayer" + (i + 1).ToString("00"), 1118, 280 + i * 39, 384, 35, theme.InspectionRow);
                names.GetArrayElementAtIndex(i).objectReferenceValue = Label(row.transform, "Name", null, 114, 2, 258, 31, 23, theme.InspectionText, true);
                roles.GetArrayElementAtIndex(i).objectReferenceValue = Label(row.transform, "Position", null, 10, 3, 99, 29, 18, theme.InspectionMuted);
            }
            var layouts = properties.FindProperty("layouts"); layouts.arraySize = 3;
            var basePositions = new[] { PlayerPosition.GK, PlayerPosition.LB, PlayerPosition.CB, PlayerPosition.CB, PlayerPosition.RB };
            var fields = new[] {
                new[] { new Vector2(.50f,.88f),new Vector2(.11f,.69f),new Vector2(.37f,.73f),new Vector2(.63f,.73f),new Vector2(.89f,.69f),new Vector2(.11f,.40f),new Vector2(.37f,.46f),new Vector2(.63f,.46f),new Vector2(.89f,.40f),new Vector2(.35f,.15f),new Vector2(.65f,.15f) },
                new[] { new Vector2(.50f,.88f),new Vector2(.11f,.69f),new Vector2(.37f,.73f),new Vector2(.63f,.73f),new Vector2(.89f,.69f),new Vector2(.16f,.44f),new Vector2(.50f,.49f),new Vector2(.84f,.44f),new Vector2(.16f,.15f),new Vector2(.84f,.15f),new Vector2(.50f,.17f) },
                new[] { new Vector2(.50f,.88f),new Vector2(.11f,.69f),new Vector2(.37f,.73f),new Vector2(.63f,.73f),new Vector2(.89f,.69f),new Vector2(.35f,.49f),new Vector2(.65f,.49f),new Vector2(.12f,.29f),new Vector2(.88f,.29f),new Vector2(.50f,.28f),new Vector2(.50f,.10f) }
            };
            var forwardPositions = new[] {
                new[] { PlayerPosition.LM, PlayerPosition.CM, PlayerPosition.CM, PlayerPosition.RM, PlayerPosition.ST, PlayerPosition.ST },
                new[] { PlayerPosition.LM, PlayerPosition.CM, PlayerPosition.RM, PlayerPosition.LW, PlayerPosition.RW, PlayerPosition.ST },
                new[] { PlayerPosition.DM, PlayerPosition.DM, PlayerPosition.LM, PlayerPosition.RM, PlayerPosition.AM, PlayerPosition.ST }
            };
            var formationNames = new[] { "Formation442", "Formation433", "Formation4231" };
            for (var f = 0; f < 3; f++)
            {
                var layoutRoot = Rect(pitch, formationNames[f], 0, 0, 584, 468);
                var definition = layouts.GetArrayElementAtIndex(f);
                definition.FindPropertyRelative("Formation").enumValueIndex = f;
                definition.FindPropertyRelative("Root").objectReferenceValue = layoutRoot.gameObject;
                var slotPositions = definition.FindPropertyRelative("SlotPositions"); var playerNames = definition.FindPropertyRelative("PlayerNames");
                slotPositions.arraySize = playerNames.arraySize = 11;
                for (var i = 0; i < 11; i++)
                {
                    var position = i < 5 ? basePositions[i] : forwardPositions[f][i - 5];
                    slotPositions.GetArrayElementAtIndex(i).enumValueIndex = (int)position;
                    var point = fields[f][i]; var marker = Rect(layoutRoot, "Slot" + (i + 1).ToString("00"), point.x * 584 - 62, point.y * 468 - 26, 124, 56);
                    var circle = Panel(marker, "PositionMarker", 47, 0, 30, 30, theme.Accent); circle.sprite = theme.Circle; circle.type = Image.Type.Simple;
                    Label(marker, "Position", null, 47, 2, 30, 26, 17, theme.Ink, true, TextAlignmentOptions.Center).text = position.ToString();
                    Panel(marker, "NameSurface", 0, 32, 124, 24, theme.InspectionPanel);
                    playerNames.GetArrayElementAtIndex(i).objectReferenceValue = Label(marker, "PlayerName", null, 3, 32, 118, 24, 18, theme.InspectionText, true, TextAlignmentOptions.Center);
                }
                layoutRoot.gameObject.SetActive(f == 0);
            }
            var emptyPanel = Panel(boardRoot, "EmptyPreview", 30, 234, 560, 88, theme.InspectionPanel);
            var empty = Label(emptyPanel.transform, "UnavailableLineup", "career.previewUnavailable", 12, 8, 536, 72, 24, theme.InspectionText, true, TextAlignmentOptions.Center);
            emptyPanel.gameObject.SetActive(false); Set(properties, "emptyState", empty); Set(properties, "emptyPanel", emptyPanel.gameObject); properties.ApplyModifiedPropertiesWithoutUndo();
            var tactics = new SerializedObject(page.GetComponent<GameHubCareerTactics>()); Set(tactics, "tacticalBoard", board); tactics.ApplyModifiedPropertiesWithoutUndo();
            foreach (var image in boardRoot.GetComponentsInChildren<Image>(true)) image.raycastTarget = false;
        }

        private static void AuthorPlayerInspection(Transform parent, bool market)
        {
            if (parent == null) throw new InvalidOperationException("Player browser must exist before its inspection layout.");
            InspectionMove(parent, "PlayersSurface", 80, 215, 350, 547);
            InspectionMove(parent, "PlayerSurface", 450, 215, 1070, 547); InspectionColor(parent.Find("PlayerSurface"), theme.InspectionPanel);
            InspectionText(parent, "PlayerSearchLabel", 106, 230, 181, 31, 21, theme.Ink);
            InspectionMove(parent, "PlayerSearch", 106, 269, 298, 53);
            InspectionMove(parent, "ClearSearch", 294, 232, 110, 31); InspectionMove(parent, "ClearSearch/Label", 5, 2, 100, 27);
            InspectionText(parent, "PositionFilterLabel", 106, 337, 298, 28, 21, theme.Ink);
            InspectionMove(parent, "PositionFilter", 106, 372, 298, 47);
            if (market)
            {
                InspectionText(parent, "ClubFilterLabel", 106, 429, 298, 28, 21, theme.Ink);
                InspectionMove(parent, "ClubFilter", 106, 464, 298, 47);
            }
            InspectionText(parent, "PlayerCount", 108, market ? 519 : 435, 290, 28, 20, theme.Muted);
            InspectionMove(parent, "PlayerScroll", 106, market ? 554 : 476, 298, market ? 137 : 215);
            var scroll = parent.Find("PlayerScroll").GetComponent<ScrollRect>();
            var scrollRect = scroll.GetComponent<RectTransform>();
            if (scroll.verticalScrollbar != null) Place(scroll.verticalScrollbar.GetComponent<RectTransform>(), 395, -scrollRect.anchoredPosition.y, 9, scrollRect.sizeDelta.y);
            var row = parent.Find("PlayerScroll/Content/PlayerTemplate");
            Place(row.GetComponent<RectTransform>(), 0, 0, 283, 66); row.GetComponent<LayoutElement>().preferredHeight = 66;
            InspectionText(row, "Name", 10, 5, 261, 30, 23, theme.Ink);
            InspectionText(row, "Description", 10, 37, 261, 24, 18, theme.Muted);
            InspectionText(parent, "NoPlayers", 117, market ? 567 : 497, 270, 106, 22, theme.Muted);
            InspectionMove(parent, "PreviousPlayers", 106, 710, 88, 39); InspectionMove(parent, "PreviousPlayers/Label", 4, 2, 80, 35);
            InspectionText(parent, "PlayerPageCount", 200, 710, 108, 39, 18, theme.Ink);
            InspectionMove(parent, "NextPlayers", 314, 710, 90, 39); InspectionMove(parent, "NextPlayers/Label", 4, 2, 82, 35);
            var detailRoot = parent.Find("PlayerDetails"); Place(detailRoot.GetComponent<RectTransform>(), 474, 232, 1022, 397);
            InspectionText(detailRoot, "PlayerName", 0, 0, 1020, 47, 36, theme.InspectionText);
            InspectionText(detailRoot, "ClubAndPosition", 2, 49, 1016, 29, 23, theme.Accent);
            InspectionText(detailRoot, "Biography", 2, 85, 1016, 72, 22, theme.InspectionMuted);
            var attributesTitle = InspectionText(detailRoot, "AttributesTitle", 274, 163, 744, 29, 21, theme.InspectionMuted);
            Localize(attributesTitle, "career.attributeScale");
            Label(detailRoot, "PositionsHeading", "career.naturalPositionMap", 0, 199, 250, 29, 22, theme.InspectionText, true);
            var map = InspectionPitch(detailRoot, "NaturalPositions", 0, 235, 246, 148);
            var positions = map.gameObject.AddComponent<CareerNaturalPositionMap>(); var positionData = new SerializedObject(positions);
            Set(positionData, "theme", theme); var markers = positionData.FindProperty("positions"); markers.arraySize = 12;
            // Horizontal mini-field: natural positions, independent of a tactical lineup slot.
            var location = new[] { new Vector2(.08f,.50f), new Vector2(.25f,.82f), new Vector2(.25f,.18f), new Vector2(.25f,.50f),
                new Vector2(.42f,.50f),new Vector2(.56f,.50f),new Vector2(.56f,.82f),new Vector2(.56f,.18f),new Vector2(.71f,.50f),
                new Vector2(.78f,.18f),new Vector2(.78f,.82f),new Vector2(.92f,.50f) };
            for (var index = 0; index < 12; index++)
            {
                var position = (PlayerPosition)index; var point = location[index];
                var marker = Panel(map, position + "Marker", point.x * 246 - 12, point.y * 148 - 12, 24, 24, theme.InspectionRow);
                marker.sprite = theme.Circle; marker.type = Image.Type.Simple;
                var label = Label(map, position + "Label", null, point.x * 246 - 13, point.y * 148 - 11, 26, 22, 16, theme.InspectionMuted, false, TextAlignmentOptions.Center); label.text = position.ToString();
                var value = markers.GetArrayElementAtIndex(index); value.FindPropertyRelative("Position").enumValueIndex = index;
                value.FindPropertyRelative("Marker").objectReferenceValue = marker; value.FindPropertyRelative("Label").objectReferenceValue = label;
            }
            positionData.ApplyModifiedPropertiesWithoutUndo();
            var groups = new[] { new[] { "BallKeeping", "Passing", "LongBall", "Shooting", "BallControl" }, new[] { "Strength", "Acceleration", "TopSpeed", "Jump", "Agility" }, new[] { "DribbleSpeed", "Tackling", "ShootPower", "Positioning", "Reaction" } };
            var keys = new[] { "career.attributesTechnical", "career.attributesPhysical", "career.attributesGame" };
            for (var group = 0; group < groups.Length; group++)
            {
                var x = 274 + group * 250;
                Label(detailRoot, "AttributeGroup" + group, keys[group], x + 2, 199, 234, 29, 23, theme.InspectionText, true);
                for (var rowIndex = 0; rowIndex < 5; rowIndex++)
                {
                    var name = groups[group][rowIndex]; var y = 235 + rowIndex * 31;
                    var stripe = Panel(detailRoot, name + "Row", x, y, 238, 28, theme.InspectionRow); stripe.transform.SetAsFirstSibling();
                    InspectionText(detailRoot, name + "Label", x + 8, y, 179, 28, 20, theme.InspectionMuted);
                    InspectionText(detailRoot, name + "Value", x + 192, y, 37, 28, 23, theme.InspectionText);
                }
            }
            var details = new SerializedObject(detailRoot.GetComponent<GameHubCareerPlayerDetails>()); Set(details, "theme", theme); Set(details, "positionMap", positions); details.ApplyModifiedPropertiesWithoutUndo();
            if (market)
            {
                InspectionText(parent, "OfferAmountLabel", 474, 629, 578, 28, 21, theme.InspectionText);
                InspectionMove(parent, "OfferAmount", 474, 663, 574, 45);
                InspectionMove(parent, "SubmitOffer", 1068, 663, 428, 45); InspectionMove(parent, "SubmitOffer/Label", 12, 3, 404, 39);
                InspectionText(parent, "OfferHint", 476, 714, 1018, 43, 20, theme.InspectionMuted);
            }
            else InspectionText(parent, "SquadHint", 476, 657, 1018, 85, 23, theme.InspectionMuted);
            foreach (var image in detailRoot.GetComponentsInChildren<Image>(true)) image.raycastTarget = false;
        }

        private static RectTransform InspectionPitch(Transform parent, string name, float x, float y, float width, float height)
        {
            var pitch = Rect(parent, name, x, y, width, height);
            var surface = Panel(pitch, "Surface", 0, 0, width, height, theme.PitchSurface); surface.sprite = null;
            // All field markings are ordinary Inspector-editable Images/RectTransforms.
            InspectionLine(pitch, "Left", 2, 2, 2, height - 4); InspectionLine(pitch, "Right", width - 4, 2, 2, height - 4);
            InspectionLine(pitch, "Top", 2, 2, width - 4, 2); InspectionLine(pitch, "Bottom", 2, height - 4, width - 4, 2);
            // The large tactical board is oriented goal-to-goal vertically; the small position map horizontally.
            var tactical = width > 400;
            InspectionLine(pitch, "Halfway", tactical ? 2 : width / 2, tactical ? height / 2 : 2, tactical ? width - 4 : 2, tactical ? 2 : height - 4);
            var diameter = Mathf.Min(width, height) * .24f;
            var outer = Panel(pitch, "CenterCircle", (width - diameter) / 2, (height - diameter) / 2, diameter, diameter, theme.PitchMarking); outer.sprite = theme.Circle; outer.type = Image.Type.Simple;
            var inner = Panel(outer.transform, "CenterCircleInside", 2, 2, diameter - 4, diameter - 4, theme.PitchSurface); inner.sprite = theme.Circle; inner.type = Image.Type.Simple;
            InspectionLine(pitch, "CenterSpot", width / 2 - 2, height / 2 - 2, 4, 4);
            if (tactical)
            {
                InspectionBox(pitch, "TopArea", width * .27f, 2, width * .46f, height * .14f);
                InspectionBox(pitch, "BottomArea", width * .27f, height * .86f - 2, width * .46f, height * .14f);
            }
            else
            {
                InspectionBox(pitch, "LeftArea", 2, height * .22f, width * .14f, height * .56f);
                InspectionBox(pitch, "RightArea", width * .86f - 2, height * .22f, width * .14f, height * .56f);
            }
            foreach (var image in pitch.GetComponentsInChildren<Image>(true)) image.raycastTarget = false;
            return pitch;
        }
        private static void InspectionBox(Transform parent, string name, float x, float y, float width, float height)
        {
            var box = Rect(parent, name, x, y, width, height);
            InspectionLine(box, "Top", 0, 0, width, 2); InspectionLine(box, "Bottom", 0, height - 2, width, 2);
            InspectionLine(box, "Left", 0, 0, 2, height); InspectionLine(box, "Right", width - 2, 0, 2, height);
        }
        private static void InspectionLine(Transform parent, string name, float x, float y, float width, float height)
        { var line = Panel(parent, name, x, y, width, height, theme.PitchMarking); line.sprite = null; line.raycastTarget = false; }
        private static void InspectionMove(Transform parent, string name, float x, float y, float width, float height)
        { var target = parent.Find(name); if (target == null) throw new InvalidOperationException("Missing authored UI: " + name); Place(target.GetComponent<RectTransform>(), x, y, width, height); }
        private static TMP_Text InspectionText(Transform parent, string name, float x, float y, float width, float height, float size, Color color)
        {
            InspectionMove(parent, name, x, y, width, height);
            return InspectionText(parent, name, size, color);
        }
        private static TMP_Text InspectionText(Transform parent, string name, float size, Color color)
        {
            var text = parent.Find(name).GetComponent<TMP_Text>();
            text.fontSize = size; text.fontSizeMin = Mathf.Max(16, size - 4); text.fontSizeMax = size; text.enableAutoSizing = true;
            text.color = color; BindTheme(text, color, true); return text;
        }
        private static void InspectionColor(Transform target, Color color)
        { var image = target.GetComponent<Image>(); image.color = color; BindTheme(image, color); }
    }
}
