using System;
using System.IO;
using System.Linq;
using FStudio.FootballWorld.Infrastructure.GameModes;
using UnityEditor;
using UnityEngine;

namespace FStudio.FootballWorld.Editor
{
    public static class GameHubTextSeed
    {
        private const string SourcePath = "Assets/FootballSimulator/Code/FootballWorld/Editor/GameHubTexts.json";
        private const string CatalogPath = "Assets/FootballSimulator/Resources/FootballWorld/GameText.asset";
        [Serializable] private sealed class Seed { public LocalizedGameString[] Entries; }
        private static LocalizedGameString[] Read() { return JsonUtility.FromJson<Seed>(File.ReadAllText(SourcePath)).Entries; }

        public static void MergeCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameTextCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<GameTextCatalog>();
                catalog.Entries = Read();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                return;
            }
            // Existing translations are authored content. Only append new keys on repeat runs.
            var keys = catalog.Entries.Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
            var missing = Read().Where(entry => !keys.Contains(entry.Key)).ToArray();
            if (missing.Length == 0) return;
            catalog.Entries = catalog.Entries.Concat(missing).ToArray();
            EditorUtility.SetDirty(catalog);
        }

        public static string English(string key)
        {
            return Read().FirstOrDefault(entry => entry.Key == key)?.English ?? key;
        }
    }
}
