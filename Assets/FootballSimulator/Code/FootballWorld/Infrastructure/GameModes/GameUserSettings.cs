using System;
using FStudio.MatchEngine.Enums;
using UnityEngine;

namespace FStudio.FootballWorld.Infrastructure.GameModes
{
    public sealed class GameUserSettings
    {
        public static readonly string[] CameraIds = { "Broadcast", "Stadium", "Tele", "StadiumHigh" };
        private const string LanguageKey = "FOOTBALL_LANGUAGE";
        private static GameUserSettings current;
        public static GameUserSettings Current => current ?? (current = new GameUserSettings());
        public event Action Changed;
        public string Language { get; private set; }
        public string CameraId { get; private set; }
        public AILevel Difficulty { get; private set; }
        public bool PersistenceFailed { get; private set; }

        private GameUserSettings()
        {
            Language = PlayerPrefs.GetString(LanguageKey, "en") == "pt" ? "pt" : "en";
            CameraId = CameraIds[Mathf.Clamp(PlayerPrefs.GetInt("SETTING_CAMERA", 1), 0, CameraIds.Length - 1)];
            Difficulty = (AILevel)Mathf.Clamp(PlayerPrefs.GetInt("SETTING_AILEVEL", 4), 0, 4);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { current = null; }

        public void SetLanguage(string language)
        {
            if (language != "pt" && language != "en") throw new ArgumentException("Unsupported language.", nameof(language));
            Language = language;
            SaveAndNotify(() => PlayerPrefs.SetString(LanguageKey, language));
        }

        public void SetCamera(string cameraId)
        {
            var index = Array.IndexOf(CameraIds, cameraId);
            if (index < 0) throw new ArgumentException("Unsupported match camera.", nameof(cameraId));
            CameraId = cameraId;
            SaveAndNotify(() => PlayerPrefs.SetInt("SETTING_CAMERA", index));
        }

        public void SetDifficulty(AILevel difficulty)
        {
            if (!Enum.IsDefined(typeof(AILevel), difficulty)) throw new ArgumentException("Unsupported difficulty.", nameof(difficulty));
            Difficulty = difficulty;
            SaveAndNotify(() => PlayerPrefs.SetInt("SETTING_AILEVEL", (int)difficulty));
        }

        private void SaveAndNotify(Action write)
        {
            try { write(); PlayerPrefs.Save(); PersistenceFailed = false; }
            catch (Exception exception)
            {
                PersistenceFailed = true;
                Debug.LogWarning("[FootballWorld] Could not persist preferences: " + exception.Message);
            }
            Changed?.Invoke();
        }
    }
}
