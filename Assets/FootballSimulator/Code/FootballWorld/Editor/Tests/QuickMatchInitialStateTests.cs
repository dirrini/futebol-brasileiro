#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using FStudio.FootballWorld.Infrastructure.GameModes;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using FStudio.UI;
using FStudio.UI.Panels;
using NUnit.Framework;
using Shared.Responses;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FStudio.FootballWorld.Editor.Tests
{
    public sealed class QuickMatchInitialStateTests
    {
        [Test]
        public void ChampionshipSideIsDisplayedAndLockedWithoutChangingFriendlyPreference()
        {
            const string preference = "SETTING_SIDE";
            var hadPreference = PlayerPrefs.HasKey(preference);
            var previousPreference = PlayerPrefs.GetInt(preference);
            var currentField = typeof(FriendlyMatchSession).GetField("current", BindingFlags.NonPublic | BindingFlags.Static);
            var previousSession = currentField.GetValue(null);
            var panelType = typeof(MainMenuPanel).Assembly.GetType("FStudio.UI.Panels.MatchSettingsPanel");
            var sideProperty = panelType.GetProperty("SIDE", BindingFlags.Public | BindingFlags.Static);
            var previousSide = sideProperty.GetValue(null);
            var host = new GameObject("Locked side presentation test");
            host.SetActive(false);
            try
            {
                var session = host.AddComponent<FriendlyMatchSession>();
                currentField.SetValue(null, session);
                var lockedSide = typeof(FriendlyMatchSession).GetProperty("LockedUserSide");
                lockedSide.SetValue(session, MatchCreateRequest.UserTeam.Away);
                PlayerPrefs.SetInt(preference, (int)MatchCreateRequest.UserTeam.Home);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/FootballSimulator/Arts/UI/Panels/MatchSettings.prefab");
                var instance = Object.Instantiate(prefab, host.transform);
                var panel = instance.GetComponent(panelType);
                var fields = new SerializedObject(panel);
                var selector = (Selector)fields.FindProperty("sideSelector").objectReferenceValue;
                var label = (TextMeshProUGUI)fields.FindProperty("sideText").objectReferenceValue;
                var arrows = selector.transform.parent.GetComponentsInChildren<InteractiveUIElement>(true);
                Assert.That(arrows.Length, Is.EqualTo(2), "Exercise the actual authored arrow controls.");
                panelType.GetMethod("InitializeSideSelection", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null);

                Assert.That(label.text, Is.EqualTo(GameText.Get("side.Away")));
                Assert.That(selector.CurrentSelected, Is.EqualTo((int)MatchCreateRequest.UserTeam.Away));
                foreach (var arrow in arrows) Assert.That(arrow.gameObject.activeSelf, Is.False);
                selector.Next();
                Assert.That(selector.CurrentSelected, Is.EqualTo((int)MatchCreateRequest.UserTeam.Away));
                Assert.That(sideProperty.GetValue(null), Is.EqualTo(MatchCreateRequest.UserTeam.Home));
                Assert.That(PlayerPrefs.GetInt(preference), Is.EqualTo((int)MatchCreateRequest.UserTeam.Home));

                lockedSide.SetValue(session, null);
                panelType.GetMethod("RefreshSideSelection", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null);
                Assert.That(label.text, Is.EqualTo(GameText.Get("side.Home")));
                foreach (var arrow in arrows) Assert.That(arrow.gameObject.activeSelf, Is.True);
                selector.Next();
                Assert.That(sideProperty.GetValue(null), Is.EqualTo(MatchCreateRequest.UserTeam.Away));
                Assert.That(PlayerPrefs.GetInt(preference), Is.EqualTo((int)MatchCreateRequest.UserTeam.Away));
            }
            finally
            {
                Object.DestroyImmediate(host);
                currentField.SetValue(null, previousSession);
                sideProperty.SetValue(null, previousSide);
                if (hadPreference) PlayerPrefs.SetInt(preference, previousPreference);
                else PlayerPrefs.DeleteKey(preference);
            }
        }

        [Test]
        public void QuickMatchControllerStartsEnabledWithItsVisualAndRaycastSubtreeHidden()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/FootballSimulator/Arts/UI/Panels/MainMenuPanel.prefab");
            Assert.That(prefab, Is.Not.Null);
            var controller = prefab.GetComponent<MainMenuPanel>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(prefab.activeSelf, Is.True, "The controller must keep receiving QuickMatchEvent.");
            Assert.That(controller.enabled, Is.True);
            var body = new SerializedObject(controller).FindProperty("canvasGroup").objectReferenceValue as CanvasGroup;
            Assert.That(body, Is.Not.Null);
            Assert.That(body.gameObject, Is.Not.SameAs(prefab), "Hide the authored body, not the event controller.");
            Assert.That(body.transform.IsChildOf(controller.transform), Is.True);
            Assert.That(body.gameObject.activeSelf, Is.False,
                "A visible legacy body can intercept the home hub's clicks before Panel.Appear has ever been called.");
        }
    }
}
#endif
