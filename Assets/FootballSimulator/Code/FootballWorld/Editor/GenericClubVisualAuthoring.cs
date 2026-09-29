using System;
using System.IO;
using FStudio.Database;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using UnityEditor;
using UnityEngine;

namespace FStudio.FootballWorld.Editor
{
    public static class GenericClubVisualAuthoring
    {
        public const string Root = "Assets/FootballSimulator/Arts/Teams/Generic/";

        public static void CreateCatalogSelectionAssets()
        {
            CreateAssets();
            GameHubAuthoring.CreateAssets();
        }

        [MenuItem("Tools/Futebol Brasileiro/Create generic club visuals")]
        public static void CreateAssets()
        {
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            var badge = AssetDatabase.LoadAssetAtPath<Texture>(Root + "Badge.png");
            if (badge == null) throw new InvalidOperationException("Rasterize the authored Generic/Badge.svg before creating club visuals.");
            var logo = Asset<LogoEntry>("GenericLogo.asset", item => {
                item.TeamLogoMaterial = badge;
                item.TeamLogoColor1 = new Color32(224, 232, 230, 255);
                item.TeamLogoColor2 = new Color32(55, 80, 91, 255);
            });
            var home = Kit("GenericHomeKit.asset", new Color32(35, 72, 90, 255), new Color32(35, 72, 90, 255),
                Color.white, new Color32(220, 124, 36, 255), new Color32(220, 124, 36, 255), Color.black);
            var away = Kit("GenericAwayKit.asset", new Color32(235, 239, 237, 255), new Color32(235, 239, 237, 255),
                Color.black, new Color32(46, 117, 67, 255), new Color32(46, 117, 67, 255), Color.white);
            var team = Asset<TeamEntry>("GenericVisualTemplate.asset", item => {
                item.TeamName = "Generic club visuals (not official)";
                item.TeamLogo = logo; item.HomeKit = home; item.AwayKit = away;
                item.Players = Array.Empty<PlayerEntry>();
            });
            var bindings = AssetDatabase.LoadAssetAtPath<LegacyMatchBindings>("Assets/FootballSimulator/Resources/FootballWorld/LegacyMatchBindings.asset");
            if (bindings == null) throw new InvalidOperationException("LegacyMatchBindings is missing.");
            bindings.DefaultVisualTemplate = team;
            EditorUtility.SetDirty(bindings);
            AssetDatabase.SaveAssets();
        }

        private static KitEntry Kit(string path, Color primary, Color secondary, Color text, Color keeperPrimary, Color keeperSecondary, Color keeperText)
            => Asset<KitEntry>(path, item => {
                item.PreviewTexture = AssetDatabase.LoadAssetAtPath<Texture>("Assets/FootballSimulator/Arts/FootballPlayer/PlayerModel/UIKits/KitMask1.png");
                item.KitMaterial = AssetDatabase.LoadAssetAtPath<Texture>("Assets/FootballSimulator/Arts/FootballPlayer/PlayerModel/Textures/KitSchemas/KitMask1.png");
                item.GKKitMaterial = item.KitMaterial;
                item.Color1 = primary; item.Color2 = secondary; item.TextColor = text;
                item.GKColor1 = keeperPrimary; item.GKColor2 = keeperSecondary; item.GKTextColor = keeperText;
            });

        // Existing assets are authored content: do not replace their Inspector edits on repeat runs.
        private static T Asset<T>(string file, Action<T> initialize) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(Root + file);
            if (existing != null) return existing;
            var created = ScriptableObject.CreateInstance<T>();
            initialize(created); AssetDatabase.CreateAsset(created, Root + file); return created;
        }
    }
}
