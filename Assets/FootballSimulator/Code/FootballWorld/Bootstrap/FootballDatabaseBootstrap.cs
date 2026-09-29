using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.DataContracts;
using FStudio.FootballWorld.Infrastructure.Importing;
using UnityEngine;
using UnityEngine.Networking;
using UnityApplication = UnityEngine.Application;

namespace FStudio.FootballWorld.Bootstrap
{
    public enum FootballDatabaseLoadState
    {
        Loading,
        Ready,
        Failed
    }

    /// <summary>
    /// Loads a portable catalog independently of menu and match scene lifetimes.
    /// Call Load/Reload on Unity's main thread. A rejected load preserves the active catalog.
    /// </summary>
    public sealed class FootballDatabaseBootstrap : MonoBehaviour
    {
        public const string SourceDatabaseAssetPath =
            "Assets/FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json";
        public const string SourceSchemaAssetPath =
            "Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v1.schema.json";
        public const string SourceSchemaV2AssetPath =
            "Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v2.schema.json";
        public const string SourceSchemaV3AssetPath =
            "Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v3.schema.json";
        public const string SourceSchemaV4AssetPath =
            "Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v4.schema.json";
        public const string SourceSchemaV5AssetPath =
            "Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v5.schema.json";
        public const string StreamingDatabasePath = "FootballWorld/database.json";
        public const string StreamingSchemaPath = "FootballWorld/database.schema.json";

        private static readonly IReadOnlyList<DatabaseImportError> NoErrors =
            Array.AsReadOnly(new DatabaseImportError[0]);
        private static readonly IReadOnlyList<VisualProfileData> NoVisualProfiles =
            Array.AsReadOnly(new VisualProfileData[0]);

        private readonly JsonDatabaseImporter importer = new JsonDatabaseImporter();
        private UnityWebRequest activeRequest;
        private int loadGeneration;

        public static FootballDatabaseBootstrap Current { get; private set; }
        public CatalogSession Session { get; } = new CatalogSession();
        public FootballDatabaseLoadState State { get; private set; } = FootballDatabaseLoadState.Loading;
        public IReadOnlyList<DatabaseImportError> Errors { get; private set; } = NoErrors;
        public IReadOnlyList<VisualProfileData> VisualProfiles { get; private set; } = NoVisualProfiles;
        public string SourceUri { get; private set; }
        public string ActiveSourceUri { get; private set; }
        // Exact validated authored content, pinned by a local season save.
        // Failed reloads preserve this snapshot along with the active catalog.
        public string ActiveSourceJson { get; private set; }

        public static string DefaultSourceUri
        {
            get
            {
#if UNITY_EDITOR
                var path = Path.GetFullPath(Path.Combine(UnityApplication.dataPath, "..", SourceDatabaseAssetPath));
                return new Uri(path).AbsoluteUri;
#else
                var path = UnityApplication.streamingAssetsPath.TrimEnd('/', '\\') + "/" + StreamingDatabasePath;
                // WebGL uses the same origin as its player; desktop uses a file URI.
                return path.Contains("://") ? path : new Uri(Path.GetFullPath(path)).AbsoluteUri;
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (Current != null)
                return;

            var existing = FindObjectOfType<FootballDatabaseBootstrap>();
            if (existing != null)
            {
                Current = existing;
                DontDestroyOnLoad(existing.gameObject);
            }
            else
            {
                var host = new GameObject("FootballWorld Database Session");
                Current = host.AddComponent<FootballDatabaseBootstrap>();
            }
            Current.Load();
        }

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
                return;
            }
            Current = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Load()
        {
            Load(DefaultSourceUri);
        }

        public void Reload()
        {
            Load(string.IsNullOrEmpty(SourceUri) ? DefaultSourceUri : SourceUri);
        }

        public void Load(string sourceUri)
        {
            var generation = ++loadGeneration;
            CancelActiveRequest();
            SourceUri = sourceUri;
            Errors = NoErrors;
            State = FootballDatabaseLoadState.Loading;

            if (!isActiveAndEnabled)
            {
                Fail(generation, "source_inactive", sourceUri ?? "$",
                    "Enable the database host before loading or reloading.", false);
                return;
            }

            if (string.IsNullOrWhiteSpace(sourceUri))
            {
                Fail(generation, "source_empty", "$", "The database source URI is empty.");
                return;
            }

            Debug.Log("[FootballWorld] Loading database from " + sourceUri);
            StartCoroutine(LoadDatabase(sourceUri, generation));
        }

        private IEnumerator LoadDatabase(string sourceUri, int generation)
        {
            UnityWebRequest request = null;
            UnityWebRequestAsyncOperation operation = null;
            Exception startError = null;
            try
            {
                request = UnityWebRequest.Get(sourceUri);
                request.timeout = 30;
                activeRequest = request;
                operation = request.SendWebRequest();
            }
            catch (Exception exception)
            {
                startError = exception;
            }

            if (startError != null)
            {
                if (ReferenceEquals(activeRequest, request))
                    activeRequest = null;
                request?.Dispose();
                Fail(generation, "source_read_failed", sourceUri, startError.Message);
                yield break;
            }

            try
            {
                yield return operation;
                if (generation != loadGeneration)
                    yield break;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Fail(generation, "source_read_failed", sourceUri,
                        "Could not read the database (HTTP " + request.responseCode + "): " + request.error);
                    yield break;
                }

                ImportAndActivate(request.downloadHandler.text, sourceUri, generation);
            }
            finally
            {
                if (ReferenceEquals(activeRequest, request))
                {
                    activeRequest = null;
                    request.Dispose();
                }
            }
        }

        private void ImportAndActivate(string json, string sourceUri, int generation)
        {
            if (generation != loadGeneration)
                return;

            try
            {
                var result = importer.Import(json);
                if (generation != loadGeneration)
                    return;
                if (!result.Success)
                {
                    Fail(generation, result.Errors);
                    return;
                }

                Session.Activate(result.Catalog);
                VisualProfiles = result.VisualProfiles;
                ActiveSourceUri = sourceUri;
                ActiveSourceJson = json;
                Errors = NoErrors;
                State = FootballDatabaseLoadState.Ready;

                var catalog = Session.ActiveCatalog;
                Debug.Log("[FootballWorld] Loaded database " + catalog.DatabaseId +
                    " revision " + catalog.DatabaseRevision + ": " + catalog.Clubs.Count +
                    " clubs, " + catalog.Players.Count + " players, " + catalog.Memberships.Count + " memberships.");
            }
            catch (Exception exception)
            {
                Fail(generation, "import_unexpected", "$", exception.Message);
            }
        }

        private void Fail(int generation, string code, string path, string message, bool logAsError = true)
        {
            Fail(generation, Array.AsReadOnly(new[] { new DatabaseImportError(code, path, message) }), logAsError);
        }

        private void Fail(int generation, IReadOnlyList<DatabaseImportError> errors, bool logAsError = true)
        {
            if (generation != loadGeneration)
                return;
            Errors = errors;
            State = FootballDatabaseLoadState.Failed;
            var summary = "[FootballWorld] Database load " + (logAsError ? "failed. " : "stopped. ") +
                (Session.ActiveCatalog == null ? "No catalog is active." : "The previous catalog remains active.");
            if (logAsError)
                Debug.LogError(summary);
            else
                Debug.LogWarning(summary);
            foreach (var error in errors)
            {
                var diagnostic = "[FootballWorld] " + error.Code + " at " + error.Path + ": " + error.Message;
                if (logAsError)
                    Debug.LogError(diagnostic);
                else
                    Debug.LogWarning(diagnostic);
            }
        }

        private void CancelActiveRequest()
        {
            activeRequest?.Abort();
            StopAllCoroutines();
            // Unity may stop an iterator without running its finally block.
            if (activeRequest != null)
            {
                activeRequest.Dispose();
                activeRequest = null;
            }
        }

        private void OnDestroy()
        {
            ++loadGeneration;
            CancelActiveRequest();
            if (Current == this)
                Current = null;
        }

        private void OnDisable()
        {
            var generation = ++loadGeneration;
            CancelActiveRequest();
            if (State == FootballDatabaseLoadState.Loading)
                Fail(generation, "source_cancelled", SourceUri ?? "$",
                    "The database load was cancelled because its host was disabled. Enable it and call Reload to retry.", false);
        }
    }
}
