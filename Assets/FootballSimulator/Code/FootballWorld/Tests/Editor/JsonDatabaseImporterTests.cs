using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.DataContracts;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.Importing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FStudio.FootballWorld.Tests
{
    public sealed class JsonDatabaseImporterTests
    {
        private readonly JsonDatabaseImporter importer = new JsonDatabaseImporter();

        [Test]
        public void LegacyFixtureImportsFourClubsAndFortyFourPlayers()
        {
            var path = Path.Combine(UnityEngine.Application.dataPath,
                "FootballSimulator/Code/FootballWorld/Tests/Fixtures/legacy-four-clubs.database.json");
            var result = importer.Import(File.ReadAllText(path));
            AssertSuccess(result);
            Assert.That(result.Catalog.Clubs.Count, Is.EqualTo(4));
            Assert.That(result.Catalog.Players.Count, Is.EqualTo(44));
            foreach (var club in result.Catalog.Clubs) Assert.That(result.Catalog.GetRoster(club.Id).Count, Is.EqualTo(11));
        }

        [Test]
        public void MapsEveryAttributeWithoutMixingVisualMetadataIntoTheCatalog()
        {
            var result = Import(ValidDocument());
            AssertSuccess(result);
            var player = result.Catalog.GetPlayer("player-1");
            Assert.That(player.Name, Is.EqualTo("Player One"));
            Assert.That(player.NaturalPositions, Is.EqualTo(new[] {PlayerPosition.ST, PlayerPosition.RW}));
            Assert.That(player.HeightCm, Is.EqualTo(180));
            Assert.That(player.WeightKg, Is.EqualTo(80));
            var a = player.Attributes;
            Assert.That(new[] {a.Strength, a.Acceleration, a.TopSpeed, a.DribbleSpeed, a.Jump,
                a.Tackling, a.BallKeeping, a.Passing, a.LongBall, a.Agility, a.Shooting,
                a.ShootPower, a.Positioning, a.Reaction, a.BallControl}, Is.EqualTo(Enumerable.Range(1, 15)));
            Assert.That(result.VisualProfiles[0].Skin.SkinId, Is.EqualTo("skin-1"));
            Assert.That(result.VisualProfiles[0].Skin.Revision, Is.EqualTo(2));
        }

        [Test]
        public void AcceptsFreeAgentsEmptyClubRostersAndMoreThanElevenPlayers()
        {
            var document = ValidDocument();
            ((JArray)document["clubs"]).Add(new JObject { ["id"] = "empty-club", ["name"] = "Empty Club" });
            var players = (JArray)document["players"];
            var memberships = (JArray)document["memberships"];
            for (var i = 2; i <= 14; i++)
            {
                var player = (JObject)players[0].DeepClone();
                player["id"] = "player-" + i;
                players.Add(player);
                if (i != 14) memberships.Add(new JObject { ["clubId"] = "club-1", ["playerId"] = "player-" + i });
            }
            var result = Import(document);
            AssertSuccess(result);
            Assert.That(result.Catalog.GetRoster("club-1").Count, Is.EqualTo(13));
            Assert.That(result.Catalog.GetRoster("empty-club"), Is.Empty);
            Assert.That(result.Catalog.GetPlayer("player-14"), Is.Not.Null);
            document["memberships"] = new JArray();
            document["visualProfiles"] = new JArray();
            AssertSuccess(Import(document));
        }

        [TestCase("databaseRevision", "\"1\"", "invalid_type")]
        [TestCase("databaseRevision", "1.0", "invalid_type")]
        [TestCase("databaseRevision", "1e0", "invalid_type")]
        [TestCase("databaseRevision", "true", "invalid_type")]
        [TestCase("databaseRevision", "null", "invalid_type")]
        [TestCase("databaseRevision", "0", "out_of_range")]
        [TestCase("databaseRevision", "2147483648", "out_of_range")]
        [TestCase("databaseRevision", "999999999999999999999999999999999999", "out_of_range")]
        [TestCase("players[0].heightCm", "149", "out_of_range")]
        [TestCase("players[0].heightCm", "211", "out_of_range")]
        [TestCase("players[0].weightKg", "44", "out_of_range")]
        [TestCase("players[0].weightKg", "101", "out_of_range")]
        [TestCase("players[0].attributes.shootPower", "101", "out_of_range")]
        [TestCase("players[0].attributes.strength", "-1", "out_of_range")]
        [TestCase("players[0].attributes.agility", "50.25", "invalid_type")]
        [TestCase("players[0].attributes", "null", "invalid_type")]
        [TestCase("players[0].naturalPositions[0]", "\"st\"", "invalid_position")]
        [TestCase("players[0].naturalPositions[0]", "\"0\"", "invalid_position")]
        [TestCase("players[0].naturalPositions[0]", "0", "invalid_type")]
        [TestCase("players[0].naturalPositions", "[]", "too_few_items")]
        [TestCase("players[0].name", "\" \\t \"", "invalid_name")]
        [TestCase("players[0].id", "\"player-1\\n\"", "invalid_id")]
        [TestCase("databaseId", "\"../database\"", "invalid_id")]
        [TestCase("clubs", "[]", "too_few_items")]
        [TestCase("players", "[]", "too_few_items")]
        [TestCase("memberships", "{}", "invalid_type")]
        [TestCase("visualProfiles", "null", "invalid_type")]
        [TestCase("visualProfiles[0].skin.revision", "0", "out_of_range")]
        [TestCase("visualProfiles[0].skin.compatibilityProfile", "\"bad/profile\"", "invalid_id")]
        [TestCase("schemaVersion", "4", "unsupported_schema_version")]
        public void RejectsWrongTypesRangesAndUnsupportedVersions(string path, string replacement, string code)
        {
            // Replace textual numbers directly so exponent spelling is preserved.
            var document = ValidDocument();
            document.SelectToken(path).Replace(new JRaw(replacement));
            AssertFailure(Import(document), code, "$." + path);
        }

        [Test]
        public void VersionOneRejectsAppearanceAndVersionTwoCanOmitIt()
        {
            var document = ValidDocument();
            Assert.That(Import(document).VisualProfiles.Single().Appearance, Is.Null);
            document["visualProfiles"][0]["appearance"] = ValidAppearance();
            AssertFailure(Import(document), "unknown_property", "$.visualProfiles[0].appearance");
            document["schemaVersion"] = 2;
            ((JProperty)document["visualProfiles"][0]["appearance"].Parent).Remove();
            var withoutAppearance = Import(document);
            AssertSuccess(withoutAppearance);
            Assert.That(withoutAppearance.VisualProfiles.Single().Appearance, Is.Null);
        }

        [Test]
        public void VersionTwoImportsImmutableAppearanceWithoutChangingSportingData()
        {
            var document = ValidAppearanceDocument();
            var result = Import(document);
            AssertSuccess(result);
            var appearance = result.VisualProfiles.Single().Appearance;
            Assert.That(new[] {appearance.SkinTone, appearance.HairStyle, appearance.HairColor,
                    appearance.BeardStyle, appearance.BeardColor, appearance.BootsColor, appearance.SockAccessoryColor},
                Is.EqualTo(new[] {"tone-3", "short", "black", "goatee", "brown", "cyan", "white"}));
            var player = result.Catalog.GetPlayer("player-1");
            Assert.That(player.Name, Is.EqualTo("Player One"));
            Assert.That(player.NaturalPositions, Is.EqualTo(new[] {PlayerPosition.ST, PlayerPosition.RW}));
            Assert.That(player.HeightCm, Is.EqualTo(180));
            Assert.That(player.WeightKg, Is.EqualTo(80));
            var a = player.Attributes;
            Assert.That(new[] {a.Strength, a.Acceleration, a.TopSpeed, a.DribbleSpeed, a.Jump,
                a.Tackling, a.BallKeeping, a.Passing, a.LongBall, a.Agility, a.Shooting,
                a.ShootPower, a.Positioning, a.Reaction, a.BallControl}, Is.EqualTo(Enumerable.Range(1, 15)));
            document["visualProfiles"][0]["appearance"]["hairStyle"] = "none";
            document["visualProfiles"] = new JArray();
            Assert.That(appearance.HairStyle, Is.EqualTo("short"));
            Assert.That(result.VisualProfiles, Has.Count.EqualTo(1));
            Assert.That(typeof(BuiltinAppearanceData).GetProperties().All(property => property.SetMethod == null), Is.True);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)BuiltinAppearancePresets.SkinTones)[0] = "other");
        }

        [TestCaseSource(nameof(AppearancePresetCases))]
        public void VersionTwoAcceptsEveryDocumentedPreset(string field, string value)
        {
            var document = ValidAppearanceDocument();
            document["visualProfiles"][0]["appearance"][field] = value;
            AssertSuccess(Import(document));
        }

        [TestCase("skinTone")]
        [TestCase("hairStyle")]
        [TestCase("hairColor")]
        [TestCase("beardStyle")]
        [TestCase("beardColor")]
        [TestCase("bootsColor")]
        [TestCase("sockAccessoryColor")]
        public void AppearanceRequiresEveryFieldAndRejectsUnknownNullOrNumericPresets(string field)
        {
            var document = ValidAppearanceDocument();
            var appearance = (JObject)document["visualProfiles"][0]["appearance"];
            var path = "$.visualProfiles[0].appearance." + field;
            appearance.Remove(field);
            AssertFailure(Import(document), "required", path);
            appearance[field] = "not-a-preset";
            AssertFailure(Import(document), "unknown_appearance_preset", path);
            appearance[field] = JValue.CreateNull();
            AssertFailure(Import(document), "invalid_type", path);
            appearance[field] = 0;
            AssertFailure(Import(document), "invalid_type", path);
        }

        [TestCase("null")]
        [TestCase("[]")]
        [TestCase("\"preset\"")]
        public void AppearanceRejectsNonObjectValues(string replacement)
        {
            var document = ValidAppearanceDocument();
            document["visualProfiles"][0]["appearance"] = JToken.Parse(replacement);
            AssertFailure(Import(document), "invalid_type", "$.visualProfiles[0].appearance");
        }

        [Test]
        public void AppearanceRejectsUnknownPropertiesAndCaseChanges()
        {
            var document = ValidAppearanceDocument();
            document["visualProfiles"][0]["appearance"]["eyeColor"] = "blue";
            AssertFailure(Import(document), "unknown_property", "$.visualProfiles[0].appearance.eyeColor");
            ((JProperty)document["visualProfiles"][0]["appearance"]["eyeColor"].Parent).Remove();
            document["visualProfiles"][0]["appearance"]["hairStyle"] = "Short";
            AssertFailure(Import(document), "unknown_appearance_preset", "$.visualProfiles[0].appearance.hairStyle");
        }

        [TestCase("skinId", "\"community-skin\"")]
        [TestCase("revision", "2")]
        [TestCase("compatibilityProfile", "\"football-player-v2\"")]
        public void BuiltinAppearanceCannotOverrideAnExternalOrUnsupportedSkin(string field, string replacement)
        {
            var document = ValidAppearanceDocument();
            document["visualProfiles"][0]["skin"][field] = JToken.Parse(replacement);
            AssertFailure(Import(document), "unsupported_appearance_skin", "$.visualProfiles[0].appearance");
        }

        [Test]
        public void PublishedVersionTwoSchemaListsTheSameAppearancePresetsAsTheImporter()
        {
            var path = Path.Combine(UnityEngine.Application.dataPath,
                "FootballSimulator/Data/FootballWorld/Schemas/database-v2.schema.json");
            var schema = JObject.Parse(File.ReadAllText(path));
            var appearance = schema["definitions"]["appearance"];
            Assert.That((int)schema["properties"]["schemaVersion"]["const"], Is.EqualTo(2));
            Assert.That((bool)appearance["additionalProperties"], Is.False);
            var options = AppearanceOptions();
            Assert.That(appearance["required"].Values<string>(), Is.EquivalentTo(options.Keys));
            foreach (var option in options)
                Assert.That(appearance["properties"][option.Key]["enum"].Values<string>(),
                    Is.EqualTo(option.Value), option.Key);
            Assert.That(schema["definitions"]["visualProfile"]["required"].Values<string>(),
                Does.Not.Contain("appearance"));
        }

        [TestCase("visualProfiles")]
        [TestCase("players[0].attributes.reaction")]
        [TestCase("players[0].naturalPositions")]
        [TestCase("visualProfiles[0].skin.revision")]
        public void RejectsMissingRequiredFields(string path)
        {
            var document = ValidDocument();
            ((JProperty)document.SelectToken(path).Parent).Remove();
            AssertFailure(Import(document), "required", "$." + path);
        }

        [TestCase("$type")]
        [TestCase("competitions")]
        [TestCase("Players")]
        public void RejectsUnknownRootFields(string property)
        {
            var document = ValidDocument();
            document[property] = "unsupported";
            AssertFailure(Import(document), "unknown_property", "$." + property);
        }

        [Test]
        public void RejectsUnknownNestedFields()
        {
            var document = ValidDocument();
            document["players"][0]["attributes"]["magic"] = 10;
            AssertFailure(Import(document), "unknown_property", "$.players[0].attributes.magic");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{")]
        [TestCase("{\"schemaVersion\":1,}")]
        [TestCase("[1,]")]
        [TestCase("{'schemaVersion':1}")]
        [TestCase("{schemaVersion:1}")]
        [TestCase("{/*comment*/\"schemaVersion\":1}")]
        [TestCase("{} //comment")]
        [TestCase("{} {}")]
        [TestCase("{\"schemaVersion\":01}")]
        [TestCase("{\"schemaVersion\":0x1}")]
        [TestCase("{\"schemaVersion\":NaN}")]
        [TestCase("{\"schemaVersion\":Infinity}")]
        [TestCase("{\"schemaVersion\":undefined}")]
        [TestCase("{\"schemaVersion\":1,\"schemaVersion\":1}")]
        [TestCase("{\"name\":\"x\",\"na\\u006de\":\"y\"}")]
        [TestCase("{\"name\":\"\\uD800\"}")]
        [TestCase("{\"name\":\"\\uDC00\"}")]
        [TestCase("{\"name\":\"\\uD800\\u0041\"}")]
        [TestCase("{\"name\":\"\\uD800\\uD800\"}")]
        public void RejectsMalformedOrExtendedJsonWithoutThrowing(string json)
            => AssertFailure(importer.Import(json), "invalid_json");

        [TestCase("null")]
        [TestCase("[]")]
        [TestCase("5")]
        [TestCase("\"text\"")]
        public void RejectsNonObjectRoots(string json)
            => AssertFailure(importer.Import(json), "invalid_type", "$");

        [TestCase("clubs", "duplicate_id")]
        [TestCase("players", "duplicate_id")]
        [TestCase("memberships", "duplicate_membership")]
        [TestCase("visualProfiles", "duplicate_visual_profile")]
        public void RejectsDuplicateIdentitiesAndBindings(string collection, string code)
        {
            var document = ValidDocument();
            var items = (JArray)document[collection];
            items.Add(items[0].DeepClone());
            AssertFailure(Import(document), code);
        }

        [Test]
        public void RejectsTwoInitialClubsForTheSamePlayer()
        {
            var document = ValidDocument();
            ((JArray)document["clubs"]).Add(new JObject { ["id"] = "club-2", ["name"] = "Second" });
            ((JArray)document["memberships"]).Add(new JObject { ["clubId"] = "club-2", ["playerId"] = "player-1" });
            AssertFailure(Import(document), "duplicate_membership", "$.memberships[1].playerId");
        }

        [TestCase("memberships[0].clubId")]
        [TestCase("memberships[0].playerId")]
        [TestCase("visualProfiles[0].playerId")]
        public void RejectsDanglingReferences(string path)
        {
            var document = ValidDocument();
            document.SelectToken(path).Replace("does-not-exist");
            AssertFailure(Import(document), "unknown_reference", "$." + path);
        }

        [Test]
        public void RejectsRepeatedNaturalPositions()
        {
            var document = ValidDocument();
            ((JArray)document["players"][0]["naturalPositions"]).Add("ST");
            AssertFailure(Import(document), "duplicate_position", "$.players[0].naturalPositions[2]");
        }

        [Test]
        public void EnforcesUtf8ByteLimitAndNestingDepth()
        {
            var tooManyBytes = "\"" + new string('\u00e9', JsonDatabaseImporter.MaximumDocumentBytes / 2) + "\"";
            AssertFailure(importer.Import(tooManyBytes), "document_too_large");
            var tooDeep = new string('[', JsonDatabaseImporter.MaximumDepth + 1) + "0" +
                new string(']', JsonDatabaseImporter.MaximumDepth + 1);
            AssertFailure(importer.Import(tooDeep), "invalid_json");
        }

        [Test]
        public void CountsNameLengthAsUnicodeCodePoints()
        {
            var name = string.Concat(Enumerable.Repeat("\ud83d\ude00", 100));
            var document = ValidDocument();
            document["players"][0]["name"] = name;
            AssertSuccess(Import(document));
            Assert.That(new ClubDefinition("club", name).Name, Is.EqualTo(name));
            AssertSuccess(importer.Import(document.ToString(Formatting.None).Replace("\ud83d\ude00", "\\uD83D\\uDE00")));
            document["players"][0]["name"] = name + "x";
            AssertFailure(Import(document), "invalid_name", "$.players[0].name");
            Assert.Throws<ArgumentException>(() => new ClubDefinition("club", name + "x"));
        }

        [TestCase("\ufeff", true)]
        [TestCase("\u0085", false)]
        public void NameWhitespaceMatchesThePortableSchema(string name, bool accepted)
        {
            var document = ValidDocument();
            document["players"][0]["name"] = name;
            if (accepted)
            {
                AssertSuccess(Import(document));
                Assert.That(new ClubDefinition("club", name).Name, Is.EqualTo(name));
            }
            else
            {
                AssertFailure(Import(document), "invalid_name", "$.players[0].name");
                Assert.Throws<ArgumentException>(() => new ClubDefinition("club", name));
            }
        }

        [Test]
        public void RejectsUnpairedLiteralSurrogatesBeforeTheJsonReaderCanReplaceThem()
        {
            AssertFailure(importer.Import("\"" + '\ud800' + "\""), "invalid_json");
            AssertFailure(importer.Import("\"" + '\udc00' + "\""), "invalid_json");
        }

        [Test]
        public void FailedImportPreservesActiveCatalogAndReturnsNoPartialSnapshot()
        {
            var session = new CatalogSession();
            var valid = Import(ValidDocument());
            AssertSuccess(valid);
            session.Activate(valid.Catalog);
            var invalid = ValidDocument();
            invalid["memberships"][0]["playerId"] = "missing";
            var failed = Import(invalid);
            AssertFailure(failed, "unknown_reference");
            Assert.That(session.ActiveCatalog, Is.SameAs(valid.Catalog));
            Assert.Throws<ArgumentNullException>(() => session.Activate(null));
            Assert.That(session.ActiveCatalog, Is.SameAs(valid.Catalog));
            var next = ValidDocument();
            next["databaseRevision"] = 2;
            var accepted = Import(next);
            AssertSuccess(accepted);
            session.Activate(accepted.Catalog);
            Assert.That(session.ActiveCatalog.DatabaseRevision, Is.EqualTo(2));
            Assert.That(valid.Catalog.DatabaseRevision, Is.EqualTo(1));
        }

        [Test]
        public void DomainAndContractCollectionsAreImmutableSnapshots()
        {
            var positions = new List<PlayerPosition> {PlayerPosition.ST};
            var player = new PlayerDefinition("p", "Player", positions, 180, 80, Attributes());
            var clubs = new List<ClubDefinition> {new ClubDefinition("c", "Club")};
            var players = new List<PlayerDefinition> {player};
            var memberships = new List<RosterMembership> {new RosterMembership("c", "p")};
            var catalog = new DatabaseCatalog("d", 1, clubs, players, memberships);
            positions[0] = PlayerPosition.GK;
            clubs.Clear(); players.Clear(); memberships.Clear();
            Assert.That(player.NaturalPositions, Is.EqualTo(new[] {PlayerPosition.ST}));
            Assert.That(catalog.GetRoster("c")[0], Is.SameAs(player));
            Assert.Throws<NotSupportedException>(() => ((IList<PlayerDefinition>)catalog.GetRoster("c")).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<ClubDefinition>)catalog.Clubs).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<PlayerPosition>)player.NaturalPositions)[0] = PlayerPosition.CB);

            var visual = new VisualProfileData("p", new SkinReferenceData("skin", 1, "profile-v1"));
            var visuals = new List<VisualProfileData> {visual};
            var dto = new DatabaseDocument(1, "d", 1, new ClubData[0], new PlayerData[0], new MembershipData[0], visuals);
            visuals.Clear();
            Assert.That(dto.VisualProfiles.Single(), Is.SameAs(visual));
            Assert.Throws<NotSupportedException>(() => ((IList<VisualProfileData>)dto.VisualProfiles).Clear());
            var result = Import(ValidDocument());
            Assert.Throws<NotSupportedException>(() => ((IList<VisualProfileData>)result.VisualProfiles).Clear());
        }

        [Test]
        public void DomainProtectsItsInvariantsWithoutTheImporter()
        {
            Assert.Throws<ArgumentException>(() => new ClubDefinition("id\n", "Club"));
            Assert.Throws<ArgumentException>(() => new ClubDefinition("id", " \t"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerDefinition("p", "Player",
                new[] {PlayerPosition.ST}, 149, 80, Attributes()));
            Assert.Throws<ArgumentException>(() => new PlayerDefinition("p", "Player",
                new[] {(PlayerPosition)999}, 180, 80, Attributes()));
            Assert.Throws<ArgumentException>(() => new DatabaseCatalog("d", 1,
                new[] {new ClubDefinition("c", "Club")}, new PlayerDefinition[0],
                new[] {new RosterMembership("c", "missing")}));
        }

        [Test]
        public void CoreAndPortableContractsHaveNoUnityOrJsonAssemblyDependencies()
        {
            foreach (var assembly in new[] {typeof(DatabaseCatalog).Assembly, typeof(CatalogSession).Assembly,
                         typeof(DatabaseDocument).Assembly})
            {
                foreach (var dependency in assembly.GetReferencedAssemblies())
                {
                    Assert.That(dependency.Name, Does.Not.StartWith("Unity"), assembly.FullName);
                    Assert.That(dependency.Name, Does.Not.Contain("Newtonsoft"), assembly.FullName);
                    if (assembly != typeof(DatabaseDocument).Assembly)
                        Assert.That(dependency.Name, Is.Not.EqualTo("FootballWorld.DataContracts"), assembly.FullName);
                }
            }
            foreach (var dependency in typeof(JsonDatabaseImporter).Assembly.GetReferencedAssemblies())
                Assert.That(dependency.Name, Does.Not.StartWith("Unity"));
        }

        private DatabaseImportResult Import(JObject document) => importer.Import(document.ToString(Formatting.None));
        private static void AssertSuccess(DatabaseImportResult result)
            => Assert.That(result.Success, Is.True, string.Join("; ", result.Errors.Select(x => x.Path + ": " + x.Message)));

        private static void AssertFailure(DatabaseImportResult result, string code, string path = null)
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Catalog, Is.Null);
            Assert.That(result.VisualProfiles, Is.Empty);
            Assert.That(result.Errors.Any(error => error.Code == code && (path == null || error.Path == path)),
                Is.True, "Expected " + code + " at " + path + "; got " + string.Join("; ", result.Errors.Select(x => x.Code + " " + x.Path)));
        }

        private static PlayerAttributes Attributes() => new PlayerAttributes(50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50);

        private static Dictionary<string, IReadOnlyList<string>> AppearanceOptions() =>
            new Dictionary<string, IReadOnlyList<string>>
            {
                {"skinTone", BuiltinAppearancePresets.SkinTones},
                {"hairStyle", BuiltinAppearancePresets.HairStyles},
                {"hairColor", BuiltinAppearancePresets.HairColors},
                {"beardStyle", BuiltinAppearancePresets.BeardStyles},
                {"beardColor", BuiltinAppearancePresets.HairColors},
                {"bootsColor", BuiltinAppearancePresets.BootsColors},
                {"sockAccessoryColor", BuiltinAppearancePresets.SockAccessoryColors}
            };

        private static IEnumerable<TestCaseData> AppearancePresetCases()
        {
            foreach (var option in AppearanceOptions())
                foreach (var value in option.Value)
                    yield return new TestCaseData(option.Key, value);
        }

        private static JObject ValidAppearance() => JObject.Parse(@"{
            'skinTone':'tone-3','hairStyle':'short','hairColor':'black','beardStyle':'goatee',
            'beardColor':'brown','bootsColor':'cyan','sockAccessoryColor':'white'
        }");

        private static JObject ValidAppearanceDocument()
        {
            var document = ValidDocument();
            document["schemaVersion"] = 2;
            document["visualProfiles"][0]["skin"] = new JObject
            {
                ["skinId"] = "builtin-player", ["revision"] = 1, ["compatibilityProfile"] = "football-player-v1"
            };
            document["visualProfiles"][0]["appearance"] = ValidAppearance();
            return document;
        }

        private static JObject ValidDocument()
        {
            return JObject.Parse(@"{
                'schemaVersion':1,'databaseId':'test-database','databaseRevision':1,
                'clubs':[{'id':'club-1','name':'Club One'}],
                'players':[{'id':'player-1','name':'Player One','naturalPositions':['ST','RW'],
                    'heightCm':180,'weightKg':80,'attributes':{
                        'strength':1,'acceleration':2,'topSpeed':3,'dribbleSpeed':4,'jump':5,
                        'tackling':6,'ballKeeping':7,'passing':8,'longBall':9,'agility':10,
                        'shooting':11,'shootPower':12,'positioning':13,'reaction':14,'ballControl':15}}],
                'memberships':[{'clubId':'club-1','playerId':'player-1'}],
                'visualProfiles':[{'playerId':'player-1','skin':{'skinId':'skin-1','revision':2,'compatibilityProfile':'football-player-v1'}}]
            }");
        }
    }
}
