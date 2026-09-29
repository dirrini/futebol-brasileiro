using System;
using System.Text;
using UnityEngine;

namespace FStudio.FootballWorld.Infrastructure.GameModes
{
    public interface IGamePreferenceStore
    {
        bool HasKey(string key);
        string GetString(string key);
        void SetString(string key, string value);
        void DeleteKey(string key);
        void Flush();
    }

    public sealed class UnityGamePreferenceStore : IGamePreferenceStore
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public string GetString(string key) => PlayerPrefs.GetString(key);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Flush() => PlayerPrefs.Save();
    }

    // Two slots preserve the committed snapshot while its replacement is staged.
    // The budget leaves room for both slots, a career profile, and user preferences
    // in WebGL's limited PlayerPrefs store. Bigger valid databases remain playable.
    public sealed class LocalGameSaveStore
    {
        public const int MaxPayloadBytes = 384 * 1024;
        private readonly IGamePreferenceStore store;
        public LocalGameSaveStore(IGamePreferenceStore store) { this.store = store ?? throw new ArgumentNullException(nameof(store)); }

        public string Read(string name)
        {
            if (!store.HasKey(name + ".active")) return null;
            var active = store.GetString(name + ".active");
            if (active != "0" && active != "1") throw new InvalidOperationException("Invalid saved content slot.");
            var key = name + "." + active;
            if (!store.HasKey(key)) throw new InvalidOperationException("Saved content is missing.");
            return store.GetString(key);
        }

        public bool Write(string name, string json, out string error)
        {
            error = null;
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaxPayloadBytes)
            { error = "save_too_large"; return false; }
            var pointerKey = name + ".active";
            var hadPointer = store.HasKey(pointerKey);
            var previousPointer = hadPointer ? store.GetString(pointerKey) : null;
            var nextSlot = previousPointer == "0" ? "1" : "0";
            var nextKey = name + "." + nextSlot;
            var hadStaged = store.HasKey(nextKey);
            var previousStaged = hadStaged ? store.GetString(nextKey) : null;
            try
            {
                store.SetString(nextKey, json);
                store.Flush();
                store.SetString(pointerKey, nextSlot);
                store.Flush();
                return true;
            }
            catch (Exception)
            {
                // Restore the in-memory preferences as well as the committed
                // pointer, otherwise a later unrelated flush could commit them.
                try
                {
                    if (hadPointer) store.SetString(pointerKey, previousPointer); else store.DeleteKey(pointerKey);
                    if (hadStaged) store.SetString(nextKey, previousStaged); else store.DeleteKey(nextKey);
                    store.Flush();
                }
                catch (Exception) { /* The previously committed slot itself was never overwritten. */ }
                error = "save_failed";
                return false;
            }
        }
    }
}
