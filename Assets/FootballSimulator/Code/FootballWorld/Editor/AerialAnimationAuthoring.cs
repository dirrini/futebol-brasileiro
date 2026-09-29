#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FStudio.MatchEngine.EngineOptions;
using FStudio.MatchEngine.Players;
using FStudio.MatchEngine.Players.PlayerController;
using FStudio.Utilities;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FStudio.FootballWorld.Editor {
    /// <summary>Creates an editable draft motion once and exports reproducible skeleton evidence for motion review.</summary>
    public static class AerialAnimationAuthoring {
        private const string ArtRoot = "Assets/FootballSimulator/Arts/FootballPlayer/";
        private const string Actions = ArtRoot + "Animations/Actions/";
        private const string DivePath = Actions + "DivingHeader.anim";
        private const string SettingsPath = "Assets/FootballSimulator/Resources/Singletons/EngineOptions_BallHitAnimations.asset";
        private const float CropStart = 19f / 30;
        private const float CropEnd = 110f / 30;
        private const float TimeScale = 0.8f;
        private const float ContactTime = (32f / 30 - CropStart) * TimeScale;

        [MenuItem("Tools/Futebol Brasileiro/Author and inspect aerial finishes")]
        public static void AuthorAndExport() {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DivePath);
            if (clip == null) clip = CreateDive();
            EnsureAerialStates(clip, "DivingHeader", 1);
            EnsureMotionCopy("Soccer Header.fbx", "Header", "AerialHeader", 0.2f, 1.6f);
            EnsureMotionCopy("GroundHeader.fbx", "GroundHeader", "AerialLowHeader", (18.5f - 12) / 30, 1.75f);
            EnsureMotionCopy("Strike Foward Jog.fbx", "Strike", "AerialVolley", (15.5f - 7) / 30, 1.35f);
            EnsureMotionCopy("Scissor Kick.fbx", "Volley", "AerialBicycle", (25f - 13.2f) / 30, 1.4f);
            EnsureBindings();
            AssetDatabase.SaveAssets();
            ExportEvidence();
        }

        // Explicit draft regeneration preserves the .anim GUID; normal authoring never overwrites an artist's edits.
        public static void RebuildDiveDraftAndExport() {
            CreateDive();
            var header = AssetDatabase.LoadAssetAtPath<AnimationClip>(Actions + "AerialHeader.anim");
            if (header != null) {
                AnimationUtility.SetAnimationEvents(header, new[] { new AnimationEvent { functionName = "BallHitEvent", time = 0.2f } });
                EditorUtility.SetDirty(header);
            }
            var settings = AssetDatabase.LoadAssetAtPath<EngineOptions_BallHitAnimations>(SettingsPath);
            var defaults = AerialShotRules.Defaults();
            foreach (var binding in settings.AerialShots) {
                var geometry = defaults.FirstOrDefault(x => x.Animation == binding.Animation);
                if (geometry != null) {
                    binding.MinimumHeight = geometry.MinimumHeight;
                    binding.MaximumHeight = geometry.MaximumHeight;
                    binding.ContactDelay = geometry.ContactDelay;
                    binding.RecoverySeconds = geometry.RecoverySeconds;
                    binding.ContactRadius = geometry.ContactRadius;
                    binding.MinimumForwardOffset = geometry.MinimumForwardOffset;
                    binding.MaximumForwardOffset = geometry.MaximumForwardOffset;
                    binding.MaximumLateralOffset = geometry.MaximumLateralOffset;
                }
                if (binding.Kind == AerialShotKind.DivingHeader) {
                    binding.MinimumHeight = 0.6f;
                    binding.MaximumHeight = 1.25f;
                    binding.MinimumForwardOffset = 0.5f;
                    binding.MaximumForwardOffset = 1.3f;
                    binding.MaximumLateralOffset = 0.5f;
                    binding.MinimumContactDistance = 0.6f;
                    binding.ContactRadius = 1.35f;
                }
            }
            EditorUtility.SetDirty(settings);
            AuthorAndExport();
        }

        private static AnimationClip CreateDive() {
            var source = LoadClip(Actions + "GKJump.fbx", "GKJumpRight");
            if (source == null) throw new InvalidOperationException("The authored goalkeeper dive clip is missing.");
            var clip = UnityEngine.Object.Instantiate(source);
            clip.name = "DivingHeader";
            var bindings = AnimationUtility.GetCurveBindings(source);
            foreach (var binding in bindings) {
                var original = AnimationUtility.GetEditorCurve(source, binding);
                var keys = new List<Keyframe>();
                for (float sourceTime = CropStart; sourceTime < CropEnd; sourceTime += 1f / 60)
                    keys.Add(new Keyframe((sourceTime - CropStart) * TimeScale, original.Evaluate(sourceTime)));
                keys.Add(new Keyframe((CropEnd - CropStart) * TimeScale, original.Evaluate(CropEnd)));
                AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys.ToArray()));
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0;
            settings.stopTime = (CropEnd - CropStart) * TimeScale;
            settings.loopTime = false;
            settings.mirror = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // Rotate the diagonal goalkeeper dive into the forward plane and limit visual travel.
            // These are saved clip curves, never transform corrections during gameplay.
            RotateRootCurves(clip, 58, 0.25f);
            foreach (var side in new[] { "Left", "Right" }) {
                BlendMuscle(clip, side + " Arm Down-Up", -0.65f);
                BlendMuscle(clip, side + " Arm Front-Back", -0.5f);
                BlendMuscle(clip, side + " Forearm Stretch", -0.2f);
            }
            AnimationUtility.SetAnimationEvents(clip, new[] {
                new AnimationEvent { functionName = "BallHitEvent", time = ContactTime }
            });
            clip.EnsureQuaternionContinuity();
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(DivePath);
            if (existing != null) {
                EditorUtility.CopySerialized(clip, existing);
                UnityEngine.Object.DestroyImmediate(clip);
                EditorUtility.SetDirty(existing);
                clip = existing;
            } else AssetDatabase.CreateAsset(clip, DivePath);
            return clip;
        }

        private static void RotateRootCurves(AnimationClip clip, float yaw, float horizontalScale) {
            var all = AnimationUtility.GetCurveBindings(clip);
            var rotation = Quaternion.Euler(0, yaw, 0);
            foreach (var prefix in new[] { "Root", "Motion" }) {
                var position = Enumerable.Range(0, 3).Select(i => FindCurve(clip, all, prefix + "T." + "xyz"[i])).ToArray();
                var orientation = Enumerable.Range(0, 4).Select(i => FindCurve(clip, all, prefix + "Q." + "xyzw"[i])).ToArray();
                var count = Mathf.CeilToInt(clip.length * 60);
                var pKeys = new[] { new List<Keyframe>(), new List<Keyframe>(), new List<Keyframe>() };
                var qKeys = new[] { new List<Keyframe>(), new List<Keyframe>(), new List<Keyframe>(), new List<Keyframe>() };
                var origin = position.All(x => x != null) ? new Vector3(position[0].Evaluate(0), 0, position[2].Evaluate(0)) : Vector3.zero;
                for (var i = 0; i <= count; i++) {
                    var time = Mathf.Min(i / 60f, clip.length);
                    if (position.All(x => x != null)) {
                        var p = new Vector3(position[0].Evaluate(time), position[1].Evaluate(time), position[2].Evaluate(time));
                        p -= origin;
                        p = rotation * p;
                        p.x *= horizontalScale;
                        p.z *= horizontalScale;
                        // Finish at the collider origin once the recovery is complete.
                        var returnWeight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.3f, clip.length, time));
                        p.x *= 1 - returnWeight;
                        p.z *= 1 - returnWeight;
                        for (var axis = 0; axis < 3; axis++) pKeys[axis].Add(new Keyframe(time, p[axis]));
                    }
                    if (orientation.All(x => x != null)) {
                        var q = new Quaternion(orientation[0].Evaluate(time), orientation[1].Evaluate(time), orientation[2].Evaluate(time), orientation[3].Evaluate(time));
                        // Blend the direction correction back out while getting up.
                        var amount = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.3f, clip.length, time));
                        q = Quaternion.Slerp(Quaternion.identity, rotation, amount) * q;
                        for (var axis = 0; axis < 4; axis++) qKeys[axis].Add(new Keyframe(time, q[axis]));
                    }
                }
                for (var i = 0; i < 3; i++) if (position[i] != null) SetCurve(clip, all, prefix + "T." + "xyz"[i], pKeys[i]);
                for (var i = 0; i < 4; i++) if (orientation[i] != null) SetCurve(clip, all, prefix + "Q." + "xyzw"[i], qKeys[i]);
            }
        }

        private static AnimationCurve FindCurve(AnimationClip clip, EditorCurveBinding[] bindings, string property) {
            var binding = bindings.FirstOrDefault(x => x.propertyName == property);
            return string.IsNullOrEmpty(binding.propertyName) ? null : AnimationUtility.GetEditorCurve(clip, binding);
        }

        private static void SetCurve(AnimationClip clip, EditorCurveBinding[] bindings, string property, List<Keyframe> keys) {
            var binding = bindings.First(x => x.propertyName == property);
            AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys.ToArray()));
        }

        private static void BlendMuscle(AnimationClip clip, string muscle, float value) {
            var binding = AnimationUtility.GetCurveBindings(clip).FirstOrDefault(x => x.propertyName == muscle);
            if (string.IsNullOrEmpty(binding.propertyName)) return;
            var original = AnimationUtility.GetEditorCurve(clip, binding);
            var keys = new List<Keyframe>();
            for (float time = 0; time <= clip.length; time += 1f / 60) {
                var weight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 0.15f, time)) *
                    (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.7f, 1.05f, time)));
                keys.Add(new Keyframe(time, Mathf.Lerp(original.Evaluate(time), value, weight)));
            }
            AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys.ToArray()));
        }

        private static void EnsureMotionCopy(string sourceFile, string sourceName, string name, float contactTime, float speed) {
            var path = Actions + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) {
                clip = UnityEngine.Object.Instantiate(LoadClip(Actions + sourceFile, sourceName));
                clip.name = name;
                AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { functionName = "BallHitEvent", time = contactTime } });
                AssetDatabase.CreateAsset(clip, path);
            }
            EnsureAerialStates(clip, name, speed);
        }

        private static void EnsureAerialStates(AnimationClip clip, string prefix, float speed) {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ArtRoot + "PlayerLocomotion.controller");
            var stateMachine = controller.layers[0].stateMachine;
            var locomotion = stateMachine.stateMachines.First(x => x.stateMachine.name == "Locomotion").stateMachine;
            foreach (var suffix in new[] { "R", "L" }) {
                var name = prefix + "_" + suffix;
                if (controller.parameters.All(x => x.name != name)) controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
                if (stateMachine.states.Any(x => x.state.name == name)) continue;
                var state = stateMachine.AddState(name, new Vector3(950, suffix == "R" ? 380 : 450));
                state.motion = clip;
                state.speed = speed;
                state.mirror = suffix == "L";
                state.writeDefaultValues = true;
                var enter = stateMachine.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0, name);
                enter.hasExitTime = false;
                enter.duration = 0.06f;
                enter.hasFixedDuration = true;
                enter.canTransitionToSelf = false;
                var leave = state.AddTransition(locomotion);
                leave.hasExitTime = true;
                leave.exitTime = 0.94f;
                leave.duration = 0.12f;
                leave.hasFixedDuration = true;
            }
            EditorUtility.SetDirty(controller);
        }

        private static void EnsureBindings() {
            var settings = AssetDatabase.LoadAssetAtPath<EngineOptions_BallHitAnimations>(SettingsPath);
            if (settings.AerialShots == null || settings.AerialShots.Length == 0) settings.AerialShots = AerialShotRules.Defaults();
            var entries = settings.AnimSettings.Entries.ToList();
            foreach (var id in new[] { PlayerAnimatorVariable.DivingHeader_R, PlayerAnimatorVariable.DivingHeader_L,
                PlayerAnimatorVariable.AerialHeader_R, PlayerAnimatorVariable.AerialHeader_L,
                PlayerAnimatorVariable.AerialLowHeader_R, PlayerAnimatorVariable.AerialLowHeader_L,
                PlayerAnimatorVariable.AerialVolley_R, PlayerAnimatorVariable.AerialVolley_L,
                PlayerAnimatorVariable.AerialBicycle_R, PlayerAnimatorVariable.AerialBicycle_L })
                if (entries.All(x => x.Id != id)) entries.Add(new EnumEntry<PlayerAnimatorVariable, bool> { Id = id, Val = true });
            settings.AnimSettings.Entries = entries.ToArray();
            var dive = settings.AerialShots.FirstOrDefault(x => x.Kind == AerialShotKind.DivingHeader);
            if (dive != null && dive.Animation == PlayerAnimatorVariable.GroundHeader_R) {
                dive.Animation = PlayerAnimatorVariable.DivingHeader_R;
                dive.ContactDelay = ContactTime + 0.06f;
                dive.RecoverySeconds = (CropEnd - CropStart) * TimeScale;
                dive.MinimumHeight = 0.6f;
                dive.MaximumHeight = 1.25f;
                dive.ContactRadius = 1.35f;
                dive.MinimumContactDistance = 0.6f;
                dive.MinimumForwardOffset = 0.5f;
                dive.MaximumForwardOffset = 1.3f;
                dive.MaximumLateralOffset = 0.5f;
                dive.MinimumFacingDot = 0.25f;
                dive.Enabled = true;
                settings.AerialShots = new[] { dive }.Concat(settings.AerialShots.Where(x => x != dive)).ToArray();
            }
            EditorUtility.SetDirty(settings);
        }

        public static void ExportEvidence() {
            var output = Path.GetFullPath("Logs/CompetitionFormats/AerialMotion");
            Directory.CreateDirectory(output);
            var report = new List<string>();
            foreach (var item in new[] {
                ("GroundHeader", Actions + "GroundHeader.fbx", "GroundHeader"),
                ("Bicycle", Actions + "Scissor Kick.fbx", "Volley"),
                ("Header", Actions + "Soccer Header.fbx", "Header"),
                ("Volley", Actions + "Strike Foward Jog.fbx", "Strike"),
                ("DivingHeader", DivePath, "DivingHeader") }) {
                var clip = LoadClip(item.Item2, item.Item3);
                if (clip == null) continue;
                report.Add(item.Item1 + " length=" + clip.length + " events=" +
                    string.Join(",", AnimationUtility.GetAnimationEvents(clip).Select(x => x.functionName + "@" + x.time)));
                ExportSkeleton(clip, Path.Combine(output, item.Item1 + ".png"), report);
            }
            foreach (var name in new[] { "AerialHeader", "AerialLowHeader", "AerialVolley", "AerialBicycle" }) {
                var clip = LoadClip(Actions + name + ".anim", name);
                if (clip == null) continue;
                report.Add(name + " length=" + clip.length + " events=" + string.Join(",", AnimationUtility.GetAnimationEvents(clip).Select(x => x.functionName + "@" + x.time)));
                ExportSkeleton(clip, Path.Combine(output, name + ".png"), report);
            }
            var dive = AssetDatabase.LoadAssetAtPath<AnimationClip>(DivePath);
            if (dive != null) foreach (var offset in new[] { -15f, 15f }) {
                var candidate = UnityEngine.Object.Instantiate(dive);
                try {
                    candidate.name = "DivingHeaderYaw" + (58 + offset);
                    RotateRootCurves(candidate, offset, 1);
                    candidate.EnsureQuaternionContinuity();
                    ExportSkeleton(candidate, Path.Combine(output, candidate.name + ".png"), report);
                } finally { UnityEngine.Object.DestroyImmediate(candidate); }
            }
            File.WriteAllLines(Path.Combine(output, "poses.txt"), report);
            Debug.Log("Aerial motion evidence exported to " + output);
        }

        private static AnimationClip LoadClip(string path, string name) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(x => x.name == name);

        private static void ExportSkeleton(AnimationClip clip, string path, List<string> report) {
            var scene = EditorSceneManager.NewPreviewScene();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "PlayerRendererMobile.prefab");
            var instance = UnityEngine.Object.Instantiate(prefab);
            // Match CodeBasedController.SetPlayer's outer scale for a nominal 180 cm / 75 kg player.
            instance.transform.localScale = new Vector3(75 * 0.01375f, 180 * 0.0058f, 75 * 0.01375f);
            SceneManager.MoveGameObjectToScene(instance, scene);
            var texture = new Texture2D(1000, 640, TextureFormat.RGBA32, false);
            try {
                foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                var animator = instance.GetComponentsInChildren<Animator>(true).First(x => x.avatar != null && x.avatar.isHuman);
                animator.applyRootMotion = false;
                var pixels = Enumerable.Repeat(new Color32(17, 28, 34, 255), texture.width * texture.height).ToArray();
                var bones = new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Neck,
                    HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                    HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                    HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                    HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot };
                var links = new[] { (0,1),(1,2),(2,3),(3,4),(2,5),(5,6),(6,7),(2,8),(8,9),(9,10),
                    (0,11),(11,12),(12,13),(0,14),(14,15),(15,16) };
                var contact = AnimationUtility.GetAnimationEvents(clip).FirstOrDefault(x => x.functionName == "BallHitEvent")?.time ?? clip.length * 0.35f;
                var sampleTimes = new[] { 0f, contact * 0.6f, contact, Mathf.Lerp(contact, clip.length, 0.45f), clip.length * 0.94f };
                AnimationMode.StartAnimationMode();
                for (var frame = 0; frame < 5; frame++) {
                    var time = sampleTimes[frame];
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animator.gameObject, clip, time);
                    AnimationMode.EndSampling();
                    var positions = bones.Select(x => animator.GetBoneTransform(x)).Select(x => x == null ? Vector3.zero : x.position - instance.transform.position).ToArray();
                    report.Add(clip.name + " t=" + time + " hips=" + positions[0].ToString("F3") + " head=" + positions[4].ToString("F3") +
                        " leftFoot=" + positions[13].ToString("F3") + " rightFoot=" + positions[16].ToString("F3"));
                    for (var view = 0; view < 2; view++) {
                        var origin = new Vector2(frame * 200 + 70, view == 0 ? 350 : 30);
                        DrawLine(pixels, 1000, 640, new Vector2(frame * 200 + 5, origin.y), new Vector2(frame * 200 + 195, origin.y), new Color32(65, 88, 91, 255));
                        Vector2 Project(Vector3 value) => origin + new Vector2(view == 0 ? value.x : value.z, value.y) * 115;
                        foreach (var link in links) DrawLine(pixels, 1000, 640, Project(positions[link.Item1]), Project(positions[link.Item2]), new Color32(80, 224, 182, 255));
                        var head = Project(positions[4]);
                        for (var a = 0; a < 360; a += 8) DrawLine(pixels, 1000, 640, head + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * 8,
                            head + new Vector2(Mathf.Cos((a + 8) * Mathf.Deg2Rad), Mathf.Sin((a + 8) * Mathf.Deg2Rad)) * 8, new Color32(255, 198, 99, 255));
                        if (frame == 2) {
                            var bodyContact = clip.name.Contains("Header") ? positions[4] : positions[16];
                            var ball = Project(bodyContact + Vector3.forward * (clip.name.Contains("Bicycle") ? -0.1f : 0.1f));
                            for (var a = 0; a < 360; a += 12) DrawLine(pixels, 1000, 640,
                                ball + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * 5,
                                ball + new Vector2(Mathf.Cos((a + 12) * Mathf.Deg2Rad), Mathf.Sin((a + 12) * Mathf.Deg2Rad)) * 5,
                                new Color32(245, 245, 245, 255));
                        }
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            } finally {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(instance);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void DrawLine(Color32[] pixels, int width, int height, Vector2 start, Vector2 end, Color32 color) {
            var steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, end)));
            for (var i = 0; i <= steps; i++) {
                var point = Vector2.Lerp(start, end, (float)i / steps);
                var x = Mathf.RoundToInt(point.x); var y = Mathf.RoundToInt(point.y);
                if (x >= 0 && x < width && y >= 0 && y < height) pixels[y * width + x] = color;
            }
        }
    }
}
#endif
