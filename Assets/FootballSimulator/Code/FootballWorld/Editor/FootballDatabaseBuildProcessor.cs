using System;
using System.IO;
using System.Linq;
using FStudio.FootballWorld.Bootstrap;
using FStudio.FootballWorld.Infrastructure.Importing;
using Newtonsoft.Json.Linq;
using UnityEditor.Build;
using UnityEngine;

namespace FStudio.FootballWorld.Editor
{
    /// <summary>
    /// Packages the authored JSON as loose, replaceable StreamingAssets files.
    /// Unity copies the registered sources into the output without touching Assets/StreamingAssets.
    /// </summary>
    public sealed class FootballDatabaseBuildProcessor : BuildPlayerProcessor
    {
        public override int callbackOrder => 100;

        public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
        {
            try
            {
                var projectPath = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
                var databasePath = Path.GetFullPath(Path.Combine(projectPath,
                    FootballDatabaseBootstrap.SourceDatabaseAssetPath));
                var schemaPath = Path.GetFullPath(Path.Combine(projectPath,
                    FootballDatabaseBootstrap.SourceSchemaAssetPath));

                if (!File.Exists(databasePath) || !File.Exists(schemaPath))
                    throw new BuildFailedException("[FootballWorld] Database JSON or its schema is missing: " +
                        databasePath + " / " + schemaPath);

                // The importer validates structure, versions, identifiers, and cross references.
                var result = new JsonDatabaseImporter().Import(File.ReadAllText(databasePath));
                if (!result.Success)
                    throw new BuildFailedException("[FootballWorld] Refusing to package an invalid database:\n" +
                        string.Join("\n", result.Errors.Select(error =>
                            error.Code + " at " + error.Path + ": " + error.Message)));

                // Require a valid JSON object for the published schema as well.
                JObject.Parse(File.ReadAllText(schemaPath));

                buildPlayerContext.AddAdditionalPathToStreamingAssets(databasePath,
                    FootballDatabaseBootstrap.StreamingDatabasePath);
                buildPlayerContext.AddAdditionalPathToStreamingAssets(schemaPath,
                    FootballDatabaseBootstrap.StreamingSchemaPath);

                var catalog = result.Catalog;
                Debug.Log("[FootballWorld] Validated database " + catalog.DatabaseId +
                    " revision " + catalog.DatabaseRevision + ": " + catalog.Clubs.Count +
                    " clubs, " + catalog.Players.Count + " players, " + catalog.Memberships.Count +
                    " memberships. Registered database.json and database.schema.json as StreamingAssets.");
            }
            catch (BuildFailedException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new BuildFailedException("[FootballWorld] Database packaging failed: " + exception.Message);
            }
        }
    }
}
