using System;
using System.IO;
using System.Linq;
using System.Text;
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
                var schemaV2Path = Path.GetFullPath(Path.Combine(projectPath,
                    FootballDatabaseBootstrap.SourceSchemaV2AssetPath));

                if (!File.Exists(databasePath) || !File.Exists(schemaPath) || !File.Exists(schemaV2Path))
                    throw new BuildFailedException("[FootballWorld] Database JSON or its schema is missing: " +
                        databasePath + " / " + schemaPath + " / " + schemaV2Path);

                // The importer validates structure, versions, identifiers, and cross references.
                var result = new JsonDatabaseImporter().Import(File.ReadAllText(databasePath));
                if (!result.Success)
                    throw new BuildFailedException("[FootballWorld] Refusing to package an invalid database:\n" +
                        string.Join("\n", result.Errors.Select(error =>
                            error.Code + " at " + error.Path + ": " + error.Message)));

                // The live database may switch between either supported version without
                // rebuilding. Publish a self-contained schema that describes both.
                var publishedSchema = CreatePublishedSchema(
                    JObject.Parse(File.ReadAllText(schemaPath)),
                    JObject.Parse(File.ReadAllText(schemaV2Path)));
                var generatedSchemaPath = Path.Combine(projectPath, "Library", "FootballWorld", "database.schema.json");
                Directory.CreateDirectory(Path.GetDirectoryName(generatedSchemaPath));
                File.WriteAllText(generatedSchemaPath, publishedSchema.ToString(), new UTF8Encoding(false));

                buildPlayerContext.AddAdditionalPathToStreamingAssets(databasePath,
                    FootballDatabaseBootstrap.StreamingDatabasePath);
                buildPlayerContext.AddAdditionalPathToStreamingAssets(generatedSchemaPath,
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

        private static JObject CreatePublishedSchema(params JObject[] versions)
        {
            var alternatives = new JArray();
            for (var i = 0; i < versions.Length; i++)
            {
                var version = (JObject)versions[i].DeepClone();
                version.Remove("$id");
                version.Remove("$schema");
                // Local references must now address their enclosing oneOf branch,
                // rather than the root of the original standalone schema.
                foreach (var reference in version.Descendants().OfType<JProperty>()
                             .Where(property => property.Name == "$ref"))
                {
                    var value = (string)reference.Value;
                    if (value != null && value.StartsWith("#/", StringComparison.Ordinal))
                        reference.Value = "#/oneOf/" + i + value.Substring(1);
                }
                alternatives.Add(version);
            }
            return new JObject
            {
                ["$schema"] = "http://json-schema.org/draft-07/schema#",
                ["$id"] = "urn:futebol-brasileiro:database:supported",
                ["title"] = "Futebol Brasileiro - supported database formats",
                ["oneOf"] = alternatives
            };
        }
    }
}
