using System;
using System.IO;
using System.IO.Compression;
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
    // Compression belongs to storage, independently of the domain/save format.
    // Four bounded slots (championship + career) leave room for preferences in WebGL.
    public sealed class LocalGameSaveStore
    {
        public const int MaxPayloadBytes = 2 * 1024 * 1024;
        public const int MaxStoredBytes = 112 * 1024;
        private const string CompressedPrefix = "football-save:gzip:1:";
        private readonly IGamePreferenceStore store;
        public LocalGameSaveStore(IGamePreferenceStore store) { this.store = store ?? throw new ArgumentNullException(nameof(store)); }

        public string Read(string name)
        {
            if (!store.HasKey(name + ".active")) return null;
            var active = store.GetString(name + ".active");
            if (active != "0" && active != "1") throw new InvalidOperationException("Invalid saved content slot.");
            var key = name + "." + active;
            if (!store.HasKey(key)) throw new InvalidOperationException("Saved content is missing.");
            return Decode(store.GetString(key));
        }

        public bool Write(string name, string json, out string error)
        {
            error = null;
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaxPayloadBytes)
            { error = "save_too_large"; return false; }
            string encoded;
            try { encoded = Encode(json); }
            catch (Exception) { error = "save_failed"; return false; }
            if (Encoding.UTF8.GetByteCount(encoded) > MaxStoredBytes)
            { error = "save_too_large"; return false; }
            var pointerKey = name + ".active";
            var hadPointer = store.HasKey(pointerKey);
            var previousPointer = hadPointer ? store.GetString(pointerKey) : null;
            if (hadPointer && previousPointer != "0" && previousPointer != "1")
            { error = "save_failed"; return false; }
            var nextSlot = previousPointer == "0" ? "1" : "0";
            var nextKey = name + "." + nextSlot;
            var hadStaged = store.HasKey(nextKey);
            var previousStaged = hadStaged ? store.GetString(nextKey) : null;
            try
            {
                store.SetString(nextKey, encoded);
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

        private static string Encode(string json)
        {
            if (Encoding.UTF8.GetByteCount(json) < 4096) return json;
            using (var output = new MemoryStream())
            {
                using (var zip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                {
                    var bytes = Encoding.UTF8.GetBytes(json);
                    zip.Write(bytes, 0, bytes.Length);
                }
                return CompressedPrefix + Convert.ToBase64String(output.ToArray());
            }
        }

        private static string Decode(string saved)
        {
            if (saved == null) throw new InvalidOperationException("Missing saved content.");
            // Existing uncompressed v1 saves remain readable without rewriting them.
            if (!saved.StartsWith(CompressedPrefix, StringComparison.Ordinal))
            {
                if (Encoding.UTF8.GetByteCount(saved) > MaxPayloadBytes) throw new InvalidOperationException("Saved content exceeds its limit.");
                return saved;
            }
            if (saved.Length > MaxStoredBytes) throw new InvalidOperationException("Compressed save exceeds its limit.");
            using (var input = new MemoryStream(Convert.FromBase64String(saved.Substring(CompressedPrefix.Length))))
            using (var zip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192];
                int count;
                while ((count = zip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + count > MaxPayloadBytes) throw new InvalidOperationException("Expanded save exceeds its limit.");
                    output.Write(buffer, 0, count);
                }
                return new UTF8Encoding(false, true).GetString(output.ToArray());
            }
        }
    }
}
