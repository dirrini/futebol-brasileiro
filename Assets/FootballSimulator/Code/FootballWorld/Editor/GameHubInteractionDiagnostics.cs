using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FStudio.FootballWorld.Infrastructure.GameModes;
using FStudio.FootballWorld.Presentation;
using FStudio.UI;
using FStudio.UI.Utilities;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Editor
{
    // Editor-only diagnostic: disposable objects, no changes to the prefab, scene or player preferences.
    public static class GameHubInteractionDiagnostics
    {
        private const string BatchKey = "FootballWorld.HubPointerBatch";
        private const string FinishingKey = BatchKey + ".Finishing";
        private const string ExitCodeKey = BatchKey + ".ExitCode";
        private const string StartedKey = BatchKey + ".Started";
        private const string ReadyKey = BatchKey + ".Ready";
        private const string StageKey = BatchKey + ".Stage";

        [Serializable] private sealed class Report
        {
            public string scope;
            public int screenWidth, screenHeight;
            public string canvasMode, rootCanvasMode, eventCamera, worldCamera;
            public bool overrideSorting;
            public int sortingOrder, registeredGraphics;
            public bool applicationFocused, sendNavigationEvents, sessionBusy;
            public string currentInputModule, currentSelection, databaseReady, cameraPixelRect;
            public string[] inputActions, activeRaycasters, mouseHits;
            public Vector2 mousePosition;
            public CardReport[] cards;
            public CardReport[] legacyArrows;
        }

        // Invoke in a fresh batch Editor process WITHOUT -quit; domain reloads are coordinated via SessionState.
        public static void RunStartingScene()
        {
            if (!UnityEngine.Application.isBatchMode || UnityEngine.Application.isPlaying)
                throw new InvalidOperationException("RunStartingScene requires a fresh batch Editor process, without -quit.");
            SessionState.SetBool(BatchKey, true);
            SessionState.SetBool(FinishingKey, false);
            SessionState.SetString(StartedKey, DateTime.UtcNow.Ticks.ToString());
            SessionState.SetString(ReadyKey, string.Empty);
            SessionState.SetInt(ExitCodeKey, 0);
            SessionState.SetInt(StageKey, 0);
            AttachBatchCallbacks();
            EditorSceneManager.OpenScene("Assets/FootballSimulator/_StartingScene.unity", OpenSceneMode.Single);
            Debug.Log("[GameHubPointerDiagnostic] Entering the real starting scene in Play Mode.");
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void ResumeBatchAfterDomainReload()
        {
            if (SessionState.GetBool(BatchKey, false)) AttachBatchCallbacks();
        }

        private static void AttachBatchCallbacks()
        {
            EditorApplication.update -= PollBatch;
            EditorApplication.update += PollBatch;
            EditorApplication.playModeStateChanged -= OnBatchPlayModeChanged;
            EditorApplication.playModeStateChanged += OnBatchPlayModeChanged;
        }

        private static void OnBatchPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(FinishingKey, false)) ExitBatch();
        }

        private static void PollBatch()
        {
            if (!SessionState.GetBool(BatchKey, false)) return;
            if (SessionState.GetBool(FinishingKey, false))
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) ExitBatch();
                return;
            }
            if (!long.TryParse(SessionState.GetString(StartedKey, string.Empty), out var started)) return;
            var elapsed = (DateTime.UtcNow.Ticks - started) / (double)TimeSpan.TicksPerSecond;
            var hub = UnityEngine.Object.FindObjectOfType<GameHubView>();
            var session = UnityEngine.Object.FindObjectOfType<GameHubSession>();
            var ready = UnityEngine.Application.isPlaying && hub != null && EventSystem.current != null && session != null && session.DatabaseReady;
            if (ready)
            {
                if (!long.TryParse(SessionState.GetString(ReadyKey, string.Empty), out var readyAt))
                {
                    SessionState.SetString(ReadyKey, DateTime.UtcNow.Ticks.ToString());
                    return;
                }
                // Allow loading overlays, Canvas rendering and InputSystem to settle naturally.
                if ((DateTime.UtcNow.Ticks - readyAt) / (double)TimeSpan.TicksPerSecond < 3) return;
            }
            if (!ready && elapsed < 90) return;
            try
            {
                if (hub == null || EventSystem.current == null)
                    throw new InvalidOperationException("Starting-scene timeout: hub=" + (hub != null) + ", EventSystem=" + (EventSystem.current != null) + ", playing=" + UnityEngine.Application.isPlaying);
                Canvas.ForceUpdateCanvases();
                if (SessionState.GetInt(StageKey, 0) == 0)
                {
                    WriteReport(hub.gameObject, EventSystem.current, "Starting-scene regression check; each home card must be the first pointer hit.", true);
                    if (!ready) { FinishBatch(2); return; }
                    SessionState.SetInt(StageKey, 1);
                    SessionState.SetString(ReadyKey, string.Empty);
                    session.Navigate(HubPage.QuickMatch);
                    return;
                }
                WriteReport(hub.gameObject, EventSystem.current, "Quick-match regression check; Back must receive pointer events above the active legacy team-selection panel.", true,
                    new[] { "QuickMatchNavigation/BackToHome" }, "hub-quick-match-pointer-diagnostic.json");
                FinishBatch(ready ? 0 : 2);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Directory.CreateDirectory("Logs/MenuQA");
                File.WriteAllText("Logs/MenuQA/hub-pointer-diagnostic-error.txt", exception.ToString());
                FinishBatch(1);
            }
        }

        private static void FinishBatch(int exitCode)
        {
            SessionState.SetInt(ExitCodeKey, exitCode);
            SessionState.SetBool(FinishingKey, true);
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
            else ExitBatch();
        }

        private static void ExitBatch()
        {
            var exitCode = SessionState.GetInt(ExitCodeKey, 1);
            SessionState.SetBool(BatchKey, false);
            SessionState.SetBool(FinishingKey, false);
            EditorApplication.update -= PollBatch;
            EditorApplication.playModeStateChanged -= OnBatchPlayModeChanged;
            EditorApplication.Exit(exitCode);
        }

        [Serializable] private sealed class CardReport
        {
            public string name;
            public Vector2 screenPoint;
            public bool active, interactable, graphicRaycast, pointerHandlerDispatched, firstHitIsCard;
            public int depth;
            public string[] listeners, canvasGroups, directHits, eventSystemHits;
        }

        [MenuItem("Tools/Futebol Brasileiro/Diagnostics/Check hub pointer routing")]
        public static void RunControlled()
        {
            var previousEventSystem = EventSystem.current;
            var root = new GameObject("Disposable hub pointer diagnostic") { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture target = null;
            try
            {
                var cameraObject = new GameObject("Diagnostic UI camera", typeof(Camera));
                cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 5; camera.nearClipPlane = .3f; camera.farClipPlane = 1000;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.cullingMask = 1 << 5; camera.enabled = false;
                target = new RenderTexture(1600, 900, 24); target.Create(); camera.targetTexture = target;

                var parentObject = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                parentObject.transform.SetParent(root.transform, false); parentObject.layer = 5;
                var parentCanvas = parentObject.GetComponent<Canvas>();
                parentCanvas.renderMode = RenderMode.ScreenSpaceCamera; parentCanvas.worldCamera = camera; parentCanvas.planeDistance = 100;
                var scaler = parentObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1600, 900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FootballSimulator/Resources/FootballWorld/GameHub.prefab");
                var hub = UnityEngine.Object.Instantiate(prefab, parentObject.transform, false);
                var canvas = hub.GetComponent<Canvas>();
                // UICanvas.OnEnable is a Play Mode callback. Supply the identical binding in this Edit Mode harness.
                canvas.worldCamera = camera;
                var eventsObject = new GameObject("Diagnostic EventSystem", typeof(EventSystem));
                eventsObject.transform.SetParent(root.transform, false);
                var events = eventsObject.GetComponent<EventSystem>(); EventSystem.current = events;
                foreach (var text in hub.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
                Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases();
                WriteReport(hub, events, "Controlled Edit Mode scene; actual saved prefab and UGUI raycasters. Camera supplied as UICanvas would in Play Mode.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (previousEventSystem != null) EventSystem.current = previousEventSystem;
            }
        }

        [MenuItem("Tools/Futebol Brasileiro/Diagnostics/Capture live hub pointer routing")]
        public static void CaptureLive()
        {
            var hub = UnityEngine.Object.FindObjectOfType<GameHubView>();
            if (!UnityEngine.Application.isPlaying || hub == null || EventSystem.current == null) throw new InvalidOperationException("Open the game hub in Play Mode first.");
            Canvas.ForceUpdateCanvases();
            WriteReport(hub.gameObject, EventSystem.current, "Live scene snapshot; no UI events were dispatched.");
        }

        private static void WriteReport(GameObject hub, EventSystem events, string scope, bool requireUnblockedCards = false, string[] buttonPaths = null, string outputFile = "hub-pointer-diagnostic.json")
        {
            var canvas = hub.GetComponent<Canvas>(); var raycaster = hub.GetComponent<GraphicRaycaster>();
            var paths = buttonPaths ?? new[] { "Home/CareerCard", "Home/QuickMatchCard", "Home/ChampionshipCard", "Home/OptionsCard" };
            var session = UnityEngine.Object.FindObjectOfType<GameHubSession>();
            var mouse = Mouse.current == null ? Vector2.zero : Mouse.current.position.ReadValue();
            var mouseHits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = mouse }, mouseHits);
            var report = new Report
            {
                scope = scope, screenWidth = Screen.width, screenHeight = Screen.height,
                canvasMode = canvas.renderMode.ToString(), rootCanvasMode = canvas.rootCanvas.renderMode.ToString(),
                eventCamera = raycaster.eventCamera == null ? "null" : raycaster.eventCamera.name,
                worldCamera = canvas.worldCamera == null ? "null" : canvas.worldCamera.name,
                overrideSorting = canvas.overrideSorting, sortingOrder = canvas.sortingOrder,
                registeredGraphics = GraphicRegistry.GetGraphicsForCanvas(canvas).Count,
                applicationFocused = UnityEngine.Application.isFocused, sendNavigationEvents = events.sendNavigationEvents,
                sessionBusy = session != null && session.IsBusy, databaseReady = session == null ? "No session" : session.DatabaseReady.ToString(),
                currentSelection = events.currentSelectedGameObject == null ? "null" : HierarchyPath(events.currentSelectedGameObject.transform),
                currentInputModule = events.currentInputModule == null ? "null" : events.currentInputModule.GetType().Name + ": active=" + events.currentInputModule.isActiveAndEnabled,
                cameraPixelRect = raycaster.eventCamera == null ? "null" : raycaster.eventCamera.pixelRect.ToString(),
                inputActions = DescribeInput(events),
                activeRaycasters = RaycasterManager.GetRaycasters().Where(item => item != null).Select(item => HierarchyPath(item.transform) + ": active=" + item.isActiveAndEnabled + ", camera=" + (item.eventCamera == null ? "null" : item.eventCamera.name)).ToArray(),
                mousePosition = mouse, mouseHits = mouseHits.Select(hit => HierarchyPath(hit.gameObject.transform) + ": module=" + hit.module.name + ", order=" + hit.sortingOrder).ToArray(),
                cards = new CardReport[paths.Length],
                legacyArrows = buttonPaths == null ? Array.Empty<CardReport>() : InspectLegacyArrows(events, raycaster.eventCamera)
            };
            for (var index = 0; index < paths.Length; index++)
            {
                var button = hub.transform.Find(paths[index]).GetComponent<Button>();
                var rect = button.GetComponent<RectTransform>(); var graphic = button.targetGraphic;
                var point = RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera, rect.TransformPoint(rect.rect.center));
                var pointer = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
                var directHits = new List<RaycastResult>(); raycaster.Raycast(pointer, directHits);
                var allHits = new List<RaycastResult>(); events.RaycastAll(pointer, allHits);
                var item = new CardReport
                {
                    name = paths[index], screenPoint = point, active = button.gameObject.activeInHierarchy,
                    interactable = button.IsInteractable(), depth = graphic.depth, graphicRaycast = graphic.Raycast(point, raycaster.eventCamera),
                    firstHitIsCard = allHits.Count > 0 && allHits[0].gameObject != null && (allHits[0].gameObject == button.gameObject || allHits[0].gameObject.transform.IsChildOf(button.transform)),
                    listeners = Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Select(i => button.onClick.GetPersistentMethodName(i) + " → " + button.onClick.GetPersistentTarget(i)?.GetType().Name).ToArray(),
                    canvasGroups = button.GetComponentsInParent<CanvasGroup>(true).Select(group => group.name + ": alpha=" + group.alpha + ", interactable=" + group.interactable + ", blocks=" + group.blocksRaycasts).ToArray(),
                    directHits = directHits.Select(hit => HierarchyPath(hit.gameObject.transform) + ": depth=" + hit.depth + ", distance=" + hit.distance).ToArray(),
                    eventSystemHits = allHits.Select(hit => HierarchyPath(hit.gameObject.transform) + ": module=" + hit.module.name + ", order=" + hit.sortingOrder).ToArray()
                };
                if (!UnityEngine.Application.isPlaying)
                {
                    // Test the UGUI pointer handler on the disposable clone without navigating or starting the runtime session.
                    for (var listener = 0; listener < button.onClick.GetPersistentEventCount(); listener++) button.onClick.SetPersistentListenerState(listener, UnityEventCallState.Off);
                    button.onClick.AddListener(() => item.pointerHandlerDispatched = true);
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                }
                report.cards[index] = item;
            }
            Directory.CreateDirectory("Logs/MenuQA");
            var json = JsonUtility.ToJson(report, true);
            File.WriteAllText(Path.Combine("Logs/MenuQA", outputFile), json);
            Debug.Log("[GameHubPointerDiagnostic] " + json);
            if (requireUnblockedCards)
            {
                var blocked = report.cards.Concat(report.legacyArrows).Where(card => !card.firstHitIsCard).Select(card => card.name + " → " + card.eventSystemHits.FirstOrDefault()).ToArray();
                if (blocked.Length > 0) throw new InvalidOperationException("Hub pointer routing is blocked: " + string.Join("; ", blocked));
            }
        }

        private static CardReport[] InspectLegacyArrows(EventSystem events, Camera camera)
        {
            var result = new List<CardReport>();
            foreach (var team in UnityEngine.Object.FindObjectsOfType<TeamSelectionTeam>())
            {
                var fields = new SerializedObject(team);
                foreach (var field in new[] { "previousButton", "nextButton" })
                {
                    var arrow = fields.FindProperty(field).objectReferenceValue as InteractiveUIElement;
                    if (arrow == null || !arrow.gameObject.activeInHierarchy || !arrow.IsInteractable) continue;
                    var rect = arrow.GetComponent<RectTransform>();
                    var point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
                    var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = point }, hits);
                    result.Add(new CardReport
                    {
                        name = HierarchyPath(arrow.transform), screenPoint = point, active = true, interactable = true,
                        firstHitIsCard = hits.Count > 0 && hits[0].gameObject != null && (hits[0].gameObject == arrow.gameObject || hits[0].gameObject.transform.IsChildOf(arrow.transform)),
                        eventSystemHits = hits.Select(hit => HierarchyPath(hit.gameObject.transform) + ": order=" + hit.sortingOrder).ToArray()
                    });
                }
            }
            return result.ToArray();
        }

        private static string[] DescribeInput(EventSystem events)
        {
            var module = events.GetComponent<InputSystemUIInputModule>();
            if (module == null) return new[] { "No InputSystemUIInputModule" };
            var references = new[] { module.point, module.leftClick, module.move, module.submit, module.cancel, module.scrollWheel };
            return references.Select(reference =>
            {
                var action = reference == null ? null : reference.action;
                return action == null ? "Missing action" : action.actionMap.name + "/" + action.name + ": enabled=" + action.enabled + ", controls=" + action.controls.Count + ", value=" + action.ReadValueAsObject();
            }).ToArray();
        }

        private static string HierarchyPath(Transform transform)
        {
            return transform.parent == null ? transform.name : HierarchyPath(transform.parent) + "/" + transform.name;
        }
    }
}
