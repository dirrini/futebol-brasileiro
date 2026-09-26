using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FStudio.Build {
    public static class WebGLBuild {
        private const string LOG_PREFIX = "[WebGLBuild] ";

        // Launch with -buildTarget WebGL so imports and script compilation finish
        // for this platform before -executeMethod invokes this entry point.
        public static void Run() {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL) {
                throw new BuildFailedException(
                    "Start Unity with -buildTarget WebGL before invoking FStudio.Build.WebGLBuild.Run.");
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL)) {
                throw new BuildFailedException("The WebGL Build Support module is not installed for this Unity editor.");
            }

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0) {
                throw new BuildFailedException("Enable at least one scene in the Unity Build Settings.");
            }
            foreach (var scene in scenes) {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null) {
                    throw new BuildFailedException("An enabled build scene could not be loaded: " + scene);
                }
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) {
                throw new BuildFailedException("Addressable Asset Settings could not be loaded.");
            }
            if (settings.ActivePlayerDataBuilder == null ||
                !settings.ActivePlayerDataBuilder.CanBuildData<AddressablesPlayerBuildResult>()) {
                throw new BuildFailedException("Select a valid Addressables player data builder before building WebGL.");
            }

            var projectPath = Directory.GetParent(Application.dataPath).FullName;
            var requestedOutput = Environment.GetEnvironmentVariable("WEBGL_BUILD_PATH");
            var outputPath = Path.GetFullPath(Path.Combine(projectPath,
                string.IsNullOrWhiteSpace(requestedOutput) ? "Builds/WebGL" : requestedOutput));

            var previousCompression = PlayerSettings.WebGL.compressionFormat;
            var previousFallback = PlayerSettings.WebGL.decompressionFallback;
            var previousThreads = PlayerSettings.WebGL.threadsSupport;
            var previousAddressablesBuild = settings.BuildAddressablesWithPlayerBuild;

            try {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = false;
                PlayerSettings.WebGL.threadsSupport = false;

                // Content is built explicitly below. The player build processor still
                // copies it into StreamingAssets, but must not build it a second time.
                settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;

                Debug.Log(LOG_PREFIX + "Building Addressables for WebGL using " +
                    settings.ActivePlayerDataBuilder.Name + ".");
                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult contentResult);
                if (contentResult == null || !string.IsNullOrEmpty(contentResult.Error)) {
                    throw new BuildFailedException("WebGL Addressables build failed: " +
                        (contentResult == null ? "No build result was returned." : contentResult.Error));
                }
                Debug.Log(LOG_PREFIX + "Addressables succeeded: " + contentResult.LocationCount +
                    " locations, " + contentResult.Duration.ToString("F1") + " seconds, output " + contentResult.OutputPath);

                Directory.CreateDirectory(outputPath);
                Debug.Log(LOG_PREFIX + "Building " + scenes.Length + " scenes to " + outputPath +
                    " with Gzip compression, browser decompression, and WebGL threads disabled.");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = BuildOptions.None
                });
                if (report == null) {
                    throw new BuildFailedException("The WebGL player build returned no report.");
                }

                var summary = report.summary;
                Debug.Log(LOG_PREFIX + "Player result: " + summary.result +
                    "; duration " + summary.totalTime + "; size " + summary.totalSize +
                    " bytes; errors " + summary.totalErrors + "; warnings " + summary.totalWarnings +
                    "; output " + summary.outputPath);
                if (summary.result != BuildResult.Succeeded) {
                    throw new BuildFailedException("WebGL player build " + summary.result +
                        " with " + summary.totalErrors + " errors. See the Unity build log.");
                }
            } catch (BuildFailedException) {
                throw;
            } catch (Exception exception) {
                throw new BuildFailedException(exception);
            } finally {
                PlayerSettings.WebGL.compressionFormat = previousCompression;
                PlayerSettings.WebGL.decompressionFallback = previousFallback;
                PlayerSettings.WebGL.threadsSupport = previousThreads;
                settings.BuildAddressablesWithPlayerBuild = previousAddressablesBuild;
                // Addressables may save its settings while building. Persist the
                // restored option without saving unrelated dirty assets.
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(settings);
            }
        }
    }
}
