using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FStudio.FootballWorld.Bootstrap;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.Importing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityApplication = UnityEngine.Application;

namespace FStudio.FootballWorld.Tests.PlayMode
{
    public sealed class FootballDatabaseBootstrapTests
    {
        private readonly List<string> temporaryFiles = new List<string>();
        private readonly List<Scene> temporaryScenes = new List<Scene>();
        private FootballDatabaseBootstrap bootstrap;
        private Scene originalScene;
        private string validJson;
        private DatabaseImportResult expectedImport;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScene = SceneManager.GetActiveScene();
            bootstrap = FootballDatabaseBootstrap.Current;
            Assert.NotNull(bootstrap,
                "The runtime initialization must create FootballDatabaseBootstrap automatically before the tests start.");

            bootstrap.gameObject.SetActive(true);
            bootstrap.enabled = true;
            var sourcePath = Path.GetFullPath(Path.Combine(UnityApplication.dataPath, "..",
                FootballDatabaseBootstrap.SourceDatabaseAssetPath));
            validJson = File.ReadAllText(sourcePath);
            expectedImport = new JsonDatabaseImporter().Import(validJson);
            Assert.That(expectedImport.Success, Is.True,
                string.Join("; ", expectedImport.Errors.Select(error => error.Path + ": " + error.Message)));
            bootstrap.Load(new Uri(sourcePath).AbsoluteUri);
            yield return WaitForLoad(FootballDatabaseLoadState.Ready);
            AssertAuthoredContent();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (bootstrap != null)
            {
                bootstrap.gameObject.SetActive(true);
                bootstrap.enabled = true;
                bootstrap.Load();
                yield return WaitForLoad(FootballDatabaseLoadState.Ready);
            }

            if (originalScene.IsValid() && originalScene.isLoaded)
                SceneManager.SetActiveScene(originalScene);
            foreach (var scene in temporaryScenes)
            {
                if (scene.IsValid() && scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
            temporaryScenes.Clear();

            // Only remove the exact unique files created by this fixture.
            foreach (var file in temporaryFiles)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            temporaryFiles.Clear();
        }

        [UnityTest]
        public IEnumerator FailedLoadKeepsCatalogAndVisualProfilesThenReloadRecovers()
        {
            var previousCatalog = bootstrap.Session.ActiveCatalog;
            var previousProfiles = bootstrap.VisualProfiles;
            var previousActiveSource = bootstrap.ActiveSourceUri;
            var invalidPath = CreateTemporaryDatabase("{");
            var invalidUri = new Uri(invalidPath).AbsoluteUri;

            LogAssert.Expect(LogType.Error,
                "[FootballWorld] Database load failed. The previous catalog remains active.");
            LogAssert.Expect(LogType.Error, new Regex(@"^\[FootballWorld\] invalid_json at \$: "));
            bootstrap.Load(invalidUri);
            yield return WaitForLoad(FootballDatabaseLoadState.Failed);

            Assert.That(bootstrap.Session.ActiveCatalog, Is.SameAs(previousCatalog));
            Assert.That(bootstrap.VisualProfiles, Is.SameAs(previousProfiles));
            Assert.That(bootstrap.ActiveSourceUri, Is.EqualTo(previousActiveSource));
            Assert.That(bootstrap.Errors, Has.Count.EqualTo(1));
            Assert.That(bootstrap.Errors[0].Code, Is.EqualTo("invalid_json"));
            Assert.That(bootstrap.SourceUri, Is.EqualTo(invalidUri));

            File.WriteAllText(invalidPath, validJson);
            bootstrap.Reload();
            yield return WaitForLoad(FootballDatabaseLoadState.Ready);

            AssertAuthoredContent();
            Assert.That(bootstrap.Session.ActiveCatalog, Is.Not.SameAs(previousCatalog));
            Assert.That(bootstrap.VisualProfiles, Is.Not.SameAs(previousProfiles));
            Assert.That(bootstrap.ActiveSourceUri, Is.EqualTo(invalidUri));
            Assert.That(bootstrap.Errors, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ANewLoadCancelsTheEarlierRequestAndOnlyActivatesItsOwnRevision()
        {
            var earlierUri = new Uri(CreateTemporaryDatabase(WithRevision(2))).AbsoluteUri;
            var latestUri = new Uri(CreateTemporaryDatabase(WithRevision(3))).AbsoluteUri;
            var loadMessages = new List<string>();
            UnityApplication.LogCallback capture = (message, stackTrace, type) =>
            {
                if (message.StartsWith("[FootballWorld] Loaded database ", StringComparison.Ordinal))
                    loadMessages.Add(message);
            };

            UnityApplication.logMessageReceived += capture;
            try
            {
                // Both start before advancing the player loop. The first completion
                // must neither replace the newer catalog nor emit a success diagnostic.
                bootstrap.Load(earlierUri);
                bootstrap.Load(latestUri);
                yield return WaitForLoad(FootballDatabaseLoadState.Ready);
                yield return null;
                yield return null;

                AssertAuthoredContent(3);
                Assert.That(bootstrap.ActiveSourceUri, Is.EqualTo(latestUri));
                Assert.That(bootstrap.Errors, Is.Empty);
                Assert.That(loadMessages, Has.Count.EqualTo(1));
                Assert.That(loadMessages[0], Does.Contain(" revision 3:"));
            }
            finally
            {
                UnityApplication.logMessageReceived -= capture;
            }
        }

        [UnityTest]
        public IEnumerator CatalogSessionSurvivesTheActiveSceneBeingReplacedAndUnloaded()
        {
            var host = bootstrap;
            var session = bootstrap.Session;
            var catalog = session.ActiveCatalog;
            var outgoing = SceneManager.CreateScene("FootballWorld-Outgoing-" + Guid.NewGuid().ToString("N"));
            var incoming = SceneManager.CreateScene("FootballWorld-Incoming-" + Guid.NewGuid().ToString("N"));
            temporaryScenes.Add(outgoing);
            temporaryScenes.Add(incoming);

            Assert.That(SceneManager.SetActiveScene(outgoing), Is.True);
            yield return null;
            Assert.That(SceneManager.SetActiveScene(incoming), Is.True);
            yield return SceneManager.UnloadSceneAsync(outgoing);

            Assert.That(host != null, Is.True);
            Assert.That(FootballDatabaseBootstrap.Current, Is.SameAs(host));
            Assert.That(host.gameObject.scene, Is.Not.EqualTo(outgoing));
            Assert.That(host.Session, Is.SameAs(session));
            Assert.That(host.Session.ActiveCatalog, Is.SameAs(catalog));
            Assert.That(host.State, Is.EqualTo(FootballDatabaseLoadState.Ready));
            AssertAuthoredContent();
        }

        [UnityTest]
        public IEnumerator DisablingDuringLoadCancelsAndAnExplicitReloadRecoversAfterEnabling()
        {
            var previousCatalog = bootstrap.Session.ActiveCatalog;
            var previousProfiles = bootstrap.VisualProfiles;
            var uri = new Uri(CreateTemporaryDatabase(WithRevision(2))).AbsoluteUri;
            bootstrap.Load(uri);
            Assert.That(bootstrap.State, Is.EqualTo(FootballDatabaseLoadState.Loading));

            ExpectStoppedWarning("source_cancelled");
            bootstrap.gameObject.SetActive(false);
            yield return null;
            yield return null;

            Assert.That(bootstrap.State, Is.EqualTo(FootballDatabaseLoadState.Failed));
            Assert.That(bootstrap.Errors[0].Code, Is.EqualTo("source_cancelled"));
            Assert.That(bootstrap.Session.ActiveCatalog, Is.SameAs(previousCatalog));
            Assert.That(bootstrap.VisualProfiles, Is.SameAs(previousProfiles));

            ExpectStoppedWarning("source_inactive");
            bootstrap.Reload();
            Assert.That(bootstrap.State, Is.EqualTo(FootballDatabaseLoadState.Failed));
            Assert.That(bootstrap.Errors[0].Code, Is.EqualTo("source_inactive"));

            bootstrap.gameObject.SetActive(true);
            bootstrap.Reload();
            yield return WaitForLoad(FootballDatabaseLoadState.Ready);
            Assert.That(bootstrap.Session.ActiveCatalog.DatabaseRevision, Is.EqualTo(2));
            Assert.That(bootstrap.ActiveSourceUri, Is.EqualTo(uri));
            Assert.That(bootstrap.Errors, Is.Empty);
            AssertAuthoredContent(2);
        }

        private IEnumerator WaitForLoad(FootballDatabaseLoadState expected)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 20d;
            while (bootstrap.State == FootballDatabaseLoadState.Loading &&
                Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;

            Assert.That(bootstrap.State, Is.EqualTo(expected),
                "The local database request did not reach the expected state within 20 seconds. Source: " + bootstrap.SourceUri);
        }

        private void AssertAuthoredContent(int? expectedRevision = null)
        {
            var expected = expectedImport.Catalog;
            var actual = bootstrap.Session.ActiveCatalog;
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.DatabaseId, Is.EqualTo(expected.DatabaseId));
            Assert.That(actual.DatabaseRevision, Is.EqualTo(expectedRevision ?? expected.DatabaseRevision));
            Assert.That(actual.Clubs.Select(club => new { club.Id, club.Name }),
                Is.EqualTo(expected.Clubs.Select(club => new { club.Id, club.Name })));
            Assert.That(actual.Players.Select(player => player.Id), Is.EqualTo(expected.Players.Select(player => player.Id)));
            foreach (var player in actual.Players)
            {
                var source = expected.GetPlayer(player.Id);
                Assert.That(player.Name, Is.EqualTo(source.Name));
                Assert.That(player.NaturalPositions, Is.EqualTo(source.NaturalPositions));
                Assert.That(player.HeightCm, Is.EqualTo(source.HeightCm));
                Assert.That(player.WeightKg, Is.EqualTo(source.WeightKg));
                Assert.That(AttributeValues(player.Attributes), Is.EqualTo(AttributeValues(source.Attributes)));
            }
            Assert.That(actual.Memberships.Select(member => new { member.ClubId, member.PlayerId }),
                Is.EqualTo(expected.Memberships.Select(member => new { member.ClubId, member.PlayerId })));
            foreach (var club in expected.Clubs)
                Assert.That(actual.GetRoster(club.Id).Select(player => player.Id),
                    Is.EqualTo(expected.GetRoster(club.Id).Select(player => player.Id)));
            Assert.That(bootstrap.VisualProfiles.Select(profile => new {
                    profile.PlayerId, profile.Skin.SkinId, profile.Skin.Revision, profile.Skin.CompatibilityProfile }),
                Is.EqualTo(expectedImport.VisualProfiles.Select(profile => new {
                    profile.PlayerId, profile.Skin.SkinId, profile.Skin.Revision, profile.Skin.CompatibilityProfile })));
        }

        private static int[] AttributeValues(PlayerAttributes attributes) => new[] {
            attributes.Strength, attributes.Acceleration, attributes.TopSpeed, attributes.DribbleSpeed,
            attributes.Jump, attributes.Tackling, attributes.BallKeeping, attributes.Passing,
            attributes.LongBall, attributes.Agility, attributes.Shooting, attributes.ShootPower,
            attributes.Positioning, attributes.Reaction, attributes.BallControl
        };

        private string CreateTemporaryDatabase(string json)
        {
            var path = Path.Combine(UnityApplication.temporaryCachePath,
                "football-world-bootstrap-" + Guid.NewGuid().ToString("N") + ".json");
            temporaryFiles.Add(path);
            File.WriteAllText(path, json);
            return path;
        }

        private string WithRevision(int revision)
        {
            var replacement = "\"databaseRevision\": " + revision;
            var changed = Regex.Replace(validJson, "\"databaseRevision\"\\s*:\\s*\\d+", replacement);
            Assert.That(changed, Does.Contain(replacement));
            return changed;
        }

        private static void ExpectStoppedWarning(string code)
        {
            LogAssert.Expect(LogType.Warning,
                "[FootballWorld] Database load stopped. The previous catalog remains active.");
            LogAssert.Expect(LogType.Warning, new Regex("^\\[FootballWorld\\] " + code + " at "));
        }
    }
}
