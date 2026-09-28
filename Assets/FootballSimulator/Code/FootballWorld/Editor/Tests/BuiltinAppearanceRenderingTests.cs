#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FStudio.Data;
using FStudio.Database;
using FStudio.FootballWorld.DataContracts;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using FStudio.MatchEngine.Graphics;
using FStudio.MatchEngine.Players.PlayerController;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace FStudio.FootballWorld.Editor.Tests
{
    public sealed class BuiltinAppearanceRenderingTests
    {
        [UnityTearDown]
        public IEnumerator RestoreEditModeAfterRenderingTest()
        {
            // Also runs after a failed assertion, so later Editor tests do not
            // inherit the rendering test's Play Mode state.
            if (UnityEngine.Application.isPlaying)
                yield return new ExitPlayMode();
        }

        [Test]
        public void EveryPortablePresetResolvesToTheSupportedLegacyValue()
        {
            var player = ScriptableObject.CreateInstance<PlayerEntry>();
            try
            {
                var colors = BuiltinAppearancePresets.HairColors;
                for (var i = 0; i < colors.Count; i++)
                {
                    var appearance = AppearanceAt(i);
                    BuiltinAppearanceMapper.Apply(player, appearance);
                    Assert.AreEqual((SkinColor)(i % 6), player.SkinColor, appearance.SkinTone);
                    Assert.AreEqual((HairStyles)(i % 7), player.HairStyles, appearance.HairStyle);
                    Assert.AreEqual((FacialHairStyles)(i % 4), player.FacialHairStyles, appearance.BeardStyle);
                    Assert.AreEqual((HairColors)i, player.HairColor, appearance.HairColor);
                    Assert.AreEqual((HairColors)(colors.Count - 1 - i), player.FacialHairColor, appearance.BeardColor);
                    Assert.AreEqual((BootColor)(i % 7), player.BootColor, appearance.BootsColor);
                    Assert.AreEqual((SockAccessoryColor)(i % 4), player.SockAccessoryColor, appearance.SockAccessoryColor);
                }
            }
            finally { Object.DestroyImmediate(player); }
        }

        [UnityTest]
        public IEnumerator BuiltinPrefabDisplaysAllPresetChoicesAndClearsHairAndBeardWhenSetToNone()
        {
            // PlayerGraphic uses renderer.material to own per-player instances.
            // Exercise that runtime API in Play Mode rather than suppressing the
            // Editor's material-instantiation error. Load all objects after the
            // domain reload requested by this transition.
            yield return new EnterPlayMode();
            Assert.IsTrue(UnityEngine.Application.isPlaying);
            const string prefabPath = "Assets/FootballSimulator/Arts/FootballPlayer/PlayerRendererMobile.prefab";
            const string materialPath = "Assets/FootballSimulator/Arts/FootballPlayer/PlayerModel/Materials/MaskMaterial.mat";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(material);
            var sourceMaterial = EditorJsonUtility.ToJson(material);
            // Keep gameplay Awake/Start callbacks dormant: this test exercises the
            // graphic component directly, without spawning shadows or a match.
            var host = new GameObject("Builtin appearance test");
            host.SetActive(false);
            var instance = Object.Instantiate(prefab, host.transform, false);
            var player = ScriptableObject.CreateInstance<PlayerEntry>();
            var ownedMaterials = new HashSet<Material>();
            try
            {
                var graphic = instance.GetComponentInChildren<PlayerGraphic>(true);
                Assert.IsNotNull(graphic);
                var serializedGraphic = new SerializedObject(graphic);
                var hair = RendererArray(serializedGraphic.FindProperty("hairRenderers"));
                var beard = RendererArray(serializedGraphic.FindProperty("facialHairRenderers"));
                Assert.AreEqual(BuiltinAppearancePresets.HairStyles.Count - 1, hair.Length);
                Assert.AreEqual(BuiltinAppearancePresets.BeardStyles.Count - 1, beard.Length);
                Assert.IsTrue(hair.Concat(beard).All(renderer => renderer != null));

                for (var i = 0; i < BuiltinAppearancePresets.HairColors.Count; i++)
                {
                    BuiltinAppearanceMapper.Apply(player, AppearanceAt(i));
                    graphic.SetPlayer(0, material, player);
                    CollectInstanceMaterials(instance, ownedMaterials);
                    AssertActiveStyle(hair, (int)player.HairStyles);
                    AssertActiveStyle(beard, (int)player.FacialHairStyles);
                    Assert.AreEqual(SkinColors.Current.GetColor(player.SkinColor), graphic.mainRenderer.sharedMaterial.GetColor("_SkinColor"));
                    Assert.AreEqual(BootColors.Current.GetColor(player.BootColor), graphic.mainRenderer.sharedMaterial.GetColor("_BootColor"));
                    Assert.AreEqual(SockAccessoryColors.Current.GetColor(player.SockAccessoryColor), graphic.mainRenderer.sharedMaterial.GetColor("_SockAccessoriesColor"));
                    AssertHairColor(hair, (int)player.HairStyles, player.HairColor);
                    AssertHairColor(beard, (int)player.FacialHairStyles, player.FacialHairColor);

                    player.HairStyles = HairStyles.None;
                    player.FacialHairStyles = FacialHairStyles.None;
                    graphic.SetPlayer(0, material, player);
                    CollectInstanceMaterials(instance, ownedMaterials);
                    AssertActiveStyle(hair, 0);
                    AssertActiveStyle(beard, 0);
                }
                Assert.AreEqual(sourceMaterial, EditorJsonUtility.ToJson(material), "Selecting cosmetics must not modify a shared kit material.");
            }
            finally
            {
                CollectInstanceMaterials(instance, ownedMaterials);
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(player);
                foreach (var owned in ownedMaterials) Object.DestroyImmediate(owned);
            }
            yield return new ExitPlayMode();
        }

        private static BuiltinAppearanceData AppearanceAt(int index)
            => new BuiltinAppearanceData(
                BuiltinAppearancePresets.SkinTones[index % BuiltinAppearancePresets.SkinTones.Count],
                BuiltinAppearancePresets.HairStyles[index % BuiltinAppearancePresets.HairStyles.Count],
                BuiltinAppearancePresets.HairColors[index],
                BuiltinAppearancePresets.BeardStyles[index % BuiltinAppearancePresets.BeardStyles.Count],
                BuiltinAppearancePresets.HairColors[BuiltinAppearancePresets.HairColors.Count - 1 - index],
                BuiltinAppearancePresets.BootsColors[index % BuiltinAppearancePresets.BootsColors.Count],
                BuiltinAppearancePresets.SockAccessoryColors[index % BuiltinAppearancePresets.SockAccessoryColors.Count]);

        private static Renderer[] RendererArray(SerializedProperty property)
        {
            var result = new Renderer[property.arraySize];
            for (var i = 0; i < result.Length; i++) result[i] = (Renderer)property.GetArrayElementAtIndex(i).objectReferenceValue;
            return result;
        }

        private static void AssertActiveStyle(Renderer[] renderers, int selected)
        {
            for (var i = 0; i < renderers.Length; i++)
                Assert.AreEqual(i + 1 == selected, renderers[i].gameObject.activeSelf,
                    "Only the requested built-in hairstyle or beard may remain enabled.");
        }

        private static void AssertHairColor(Renderer[] renderers, int selected, HairColors color)
        {
            if (selected == 0) return;
            foreach (var renderer in renderers[selected - 1].GetComponentsInChildren<Renderer>(true))
                Assert.AreEqual(PlayerHairColors.Current.GetColor(color), renderer.sharedMaterial.GetColor("_Color"));
        }

        private static void CollectInstanceMaterials(GameObject instance, ISet<Material> owned)
        {
            if (instance == null) return;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && !EditorUtility.IsPersistent(material)) owned.Add(material);
        }
    }
}
#endif
