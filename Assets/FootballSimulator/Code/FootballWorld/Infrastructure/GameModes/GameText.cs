using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FStudio.FootballWorld.Infrastructure.GameModes
{
    public static class GameText
    {
        private static Dictionary<string, LocalizedGameString> entries;
        private static GameUserSettings observedSettings;
        public static event Action Changed;
        public static CultureInfo Culture => CultureInfo.GetCultureInfo(GameUserSettings.Current.Language == "pt" ? "pt-BR" : "en");

        public static string Get(string key, params object[] arguments)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(key)) return string.Empty;
            var text = entries.TryGetValue(key, out var entry)
                ? (GameUserSettings.Current.Language == "pt" ? entry.Portuguese : entry.English) : key;
            if (string.IsNullOrEmpty(text)) text = key;
            return arguments == null || arguments.Length == 0 ? text : string.Format(Culture, text, arguments);
        }

        public static string FormatDate(DateTime date) => date.ToString("d", Culture);

        public static void EnsureLoaded()
        {
            if (entries == null)
            {
                entries = new Dictionary<string, LocalizedGameString>(StringComparer.Ordinal);
                var catalog = Resources.Load<GameTextCatalog>("FootballWorld/GameText");
                if (catalog != null)
                    foreach (var entry in catalog.Entries)
                        if (entry != null && !string.IsNullOrEmpty(entry.Key)) entries[entry.Key] = entry;
            }
            if (ReferenceEquals(observedSettings, GameUserSettings.Current)) return;
            if (observedSettings != null) observedSettings.Changed -= OnSettingsChanged;
            observedSettings = GameUserSettings.Current;
            observedSettings.Changed += OnSettingsChanged;
        }

        private static void OnSettingsChanged() => Changed?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (observedSettings != null) observedSettings.Changed -= OnSettingsChanged;
            observedSettings = null;
            entries = null;
            Changed = null;
        }
    }
}
