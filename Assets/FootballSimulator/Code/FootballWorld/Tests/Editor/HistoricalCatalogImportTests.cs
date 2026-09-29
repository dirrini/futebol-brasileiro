using System;
using System.IO;
using System.Linq;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.Importing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FStudio.FootballWorld.Tests
{
    public sealed class HistoricalCatalogImportTests
    {
        private readonly JsonDatabaseImporter importer = new JsonDatabaseImporter();

        [Test]
        public void V4MapsObservationCountriesVenuesBiographiesAndSimulationParametersWithoutChangingAttributes()
        {
            var document = Valid();
            var club = (JObject)document["clubs"][0];
            club["officialName"] = "Clube de teste oficial"; club["shortName"] = "Teste"; club["stadiumId"] = "venue-a";
            club["reputation"] = 55; club["supporterCount"] = 10000; club["transferBudget"] = 100000;
            club["monthlyWageBudget"] = 9000; club["currency"] = "BRL"; club["sponsorship"] = "Patrocinador de teste"; club["notes"] = "Parâmetros de simulação.";
            var player = (JObject)document["players"][0];
            player["fullName"] = "Nome completo de teste"; player["birthDate"] = "2000-02-29"; player["nickname"] = "Apelido";
            player["preferredFoot"] = "left"; player["nationalityCode"] = "BR"; player["notes"] = "Fonte de teste.";
            var result = Import(document); Success(result);
            var mappedClub = result.Catalog.Clubs[0]; var mappedPlayer = result.Catalog.Players[0];
            Assert.That(mappedClub.CountryCode, Is.EqualTo("BR")); Assert.That(mappedClub.City, Is.EqualTo("São Paulo"));
            Assert.That(mappedClub.OfficialName, Is.EqualTo((string)club["officialName"])); Assert.That(mappedClub.ShortName, Is.EqualTo("Teste"));
            Assert.That(mappedClub.Reputation, Is.EqualTo(55)); Assert.That(mappedClub.SupporterCount, Is.EqualTo(10000));
            Assert.That(mappedClub.TransferBudget, Is.EqualTo(100000)); Assert.That(mappedClub.MonthlyWageBudget, Is.EqualTo(9000));
            Assert.That(mappedClub.Currency, Is.EqualTo("BRL")); Assert.That(mappedClub.Sponsorship, Is.EqualTo((string)club["sponsorship"]));
            Assert.That(mappedClub.Notes, Is.EqualTo((string)club["notes"]));
            Assert.That(result.Catalog.GetStadium(mappedClub.StadiumId).Capacity, Is.EqualTo(12345));
            Assert.That(result.Catalog.Stadiums[0].CountryCode, Is.EqualTo("BR")); Assert.That(result.Catalog.Countries[0].Name, Is.EqualTo("Brasil"));
            Assert.That(mappedPlayer.FullName, Is.EqualTo((string)player["fullName"])); Assert.That(mappedPlayer.BirthDate.Value.ToString(), Is.EqualTo("2000-02-29"));
            Assert.That(mappedPlayer.Nickname, Is.EqualTo("Apelido")); Assert.That(mappedPlayer.DisplayName, Is.EqualTo("Apelido"));
            Assert.That(mappedPlayer.Name, Is.EqualTo((string)player["name"]));
            Assert.That(mappedPlayer.PreferredFoot, Is.EqualTo("left")); Assert.That(mappedPlayer.NationalityCode, Is.EqualTo("BR"));
            Assert.That(mappedPlayer.Notes, Is.EqualTo((string)player["notes"]));
            Assert.That(mappedPlayer.Attributes.Shooting, Is.EqualTo((int)player["attributes"]["shooting"]));
            Assert.That(result.Catalog.Snapshot.Date.ToString(), Is.EqualTo("2026-01-11"));
            Assert.That(result.Catalog.Snapshot.RosterScope, Is.EqualTo("matchday-squads"));
            Assert.That(result.Catalog.Snapshot.Sources[0].Url, Is.EqualTo("https://example.com/report.pdf"));
            document["countries"][0]["name"] = "Changed after import";
            document["snapshot"]["sources"][0]["title"] = "Changed";
            Assert.That(result.Catalog.Countries[0].Name, Is.EqualTo("Brasil"));
            Assert.That(result.Catalog.Snapshot.Sources[0].Title, Is.EqualTo("Documento de teste"));
        }

        [Test]
        public void OmittedOptionalDataStaysUnknownAndPreviousContractsRemainUsable()
        {
            var result = Import(Valid()); Success(result);
            Assert.That(result.Catalog.Players[0].BirthDate, Is.Null); Assert.That(result.Catalog.Players[0].PreferredFoot, Is.Null);
            Assert.That(result.Catalog.Players[0].Nickname, Is.Null); Assert.That(result.Catalog.Players[0].DisplayName, Is.EqualTo(result.Catalog.Players[0].Name));
            Assert.That(result.Catalog.Clubs[0].TransferBudget, Is.Null); Assert.That(result.Catalog.Clubs[0].StadiumId, Is.Null);
            foreach (var version in new[] {1, 2, 3})
            {
                var document = Legacy(); document["schemaVersion"] = version;
                if (version == 3) { document["competitions"] = new JArray(); document["competitionEditions"] = new JArray(); }
                result = Import(document); Success(result);
                Assert.That(result.Catalog.Countries, Is.Empty); Assert.That(result.Catalog.Stadiums, Is.Empty);
                Assert.That(result.Catalog.Snapshot, Is.Null); Assert.That(result.Catalog.Clubs[0].CountryCode, Is.Null);
                document["clubs"][0]["countryCode"] = "BR";
                Failure(Import(document), "unknown_property", "$.clubs[0].countryCode");
            }
        }

        [TestCase("countries", "null", "invalid_type")]
        [TestCase("stadiums", "null", "invalid_type")]
        [TestCase("snapshot", "null", "invalid_type")]
        [TestCase("countries[0].code", "\"br\"", "invalid_code")]
        [TestCase("clubs[0].countryCode", "\"XX\"", "unknown_reference")]
        [TestCase("clubs[0].city", "\" \"", "invalid_name")]
        [TestCase("stadiums[0].countryCode", "\"XX\"", "unknown_reference")]
        [TestCase("stadiums[0].capacity", "0", "out_of_range")]
        [TestCase("stadiums[0].capacity", "1000001", "out_of_range")]
        [TestCase("stadiums[0].capacity", "null", "invalid_type")]
        [TestCase("stadiums[0].capacity", "1.0", "invalid_type")]
        [TestCase("snapshot.date", "\"0000-01-01\"", "invalid_date")]
        [TestCase("snapshot.date", "\"1900-02-29\"", "invalid_date")]
        [TestCase("snapshot.date", "\"2026-02-29\"", "invalid_date")]
        [TestCase("snapshot.rosterScope", "\"unknown\"", "unsupported_roster_scope")]
        [TestCase("snapshot.sources[0].url", "\"file:///secret\"", "invalid_url")]
        [TestCase("snapshot.sources[0].url", "\"https:///report.pdf\"", "invalid_url")]
        [TestCase("snapshot.sources[0].url", "\"https://example.com/with space\"", "invalid_url")]
        [TestCase("snapshot.sources[0].url", "\"https://example.com/with\u0085space\"", "invalid_url")]
        [TestCase("snapshot.sources[0].url", "\"https://example.com/with\uFEFFspace\"", "invalid_url")]
        [TestCase("snapshot.notes", "null", "invalid_type")]
        public void RejectsInvalidRequiredData(string path, string value, string code)
        {
            var document = Valid(); document.SelectToken(path).Replace(new JRaw(value));
            Failure(Import(document), code, "$." + path);
        }

        [TestCase("clubs[0]", "stadiumId", "\"missing\"", "unknown_reference")]
        [TestCase("clubs[0]", "officialName", "null", "invalid_type")]
        [TestCase("clubs[0]", "reputation", "101", "out_of_range")]
        [TestCase("clubs[0]", "supporterCount", "-1", "out_of_range")]
        [TestCase("clubs[0]", "transferBudget", "2147483648", "out_of_range")]
        [TestCase("clubs[0]", "currency", "\"brl\"", "invalid_code")]
        [TestCase("players[0]", "birthDate", "\"2026-01-12\"", "birth_after_snapshot")]
        [TestCase("players[0]", "birthDate", "\"2026-1-11\"", "invalid_date")]
        [TestCase("players[0]", "birthDate", "null", "invalid_type")]
        [TestCase("players[0]", "preferredFoot", "\"Right\"", "unsupported_preferred_foot")]
        [TestCase("players[0]", "nationalityCode", "\"XX\"", "unknown_reference")]
        [TestCase("players[0]", "fullName", "\" \"", "invalid_text")]
        [TestCase("players[0]", "nickname", "null", "invalid_type")]
        [TestCase("players[0]", "nickname", "\" \"", "invalid_text")]
        public void RejectsInvalidOptionalData(string owner, string field, string value, string code)
        {
            var document = Valid(); document.SelectToken(owner)[field] = new JRaw(value);
            Failure(Import(document), code, "$." + owner + "." + field);
        }

        [TestCase("countries")]
        [TestCase("stadiums")]
        [TestCase("snapshot")]
        [TestCase("snapshot.sources")]
        [TestCase("clubs[0].countryCode")]
        [TestCase("clubs[0].city")]
        public void V4RequiresNewFieldsExplicitly(string path)
        {
            var document = Valid(); ((JProperty)document.SelectToken(path).Parent).Remove();
            Failure(Import(document), "required", "$." + path);
        }

        [TestCase("countries", "code")]
        [TestCase("stadiums", "id")]
        [TestCase("snapshot.sources", "id")]
        public void RejectsDuplicatePortableIdentities(string path, string key)
        {
            var document = Valid(); var items = (JArray)document.SelectToken(path); items.Add(items[0].DeepClone());
            Failure(Import(document), "duplicate_id", "$." + path + "[1]." + key);
        }

        [Test]
        public void BudgetsRequireCurrencyButZeroIsAValidExplicitSimulationValue()
        {
            var document = Valid(); document["clubs"][0]["transferBudget"] = 0;
            Failure(Import(document), "required", "$.clubs[0].currency");
            document["clubs"][0]["currency"] = "BRL"; Success(Import(document));
        }

        [Test]
        public void TextLimitsCountUnicodeCodePointsAndDoNotTrimOrInventMissingValues()
        {
            var document = Valid(); var name = string.Concat(Enumerable.Repeat("\U0001F600", 200));
            document["players"][0]["fullName"] = name;
            document["players"][0]["notes"] = new string('x', 4000);
            document["players"][0]["nickname"] = string.Concat(Enumerable.Repeat("\U0001F600", 100));
            Success(Import(document)); document["players"][0]["fullName"] = name + "x";
            Failure(Import(document), "invalid_text", "$.players[0].fullName");
            document["players"][0]["fullName"] = name; document["players"][0]["nickname"] = new string('x', 101);
            Failure(Import(document), "invalid_text", "$.players[0].nickname");
        }

        [Test]
        public void DomainEnforcesReferenceIntegrityAndCopiesNewCollections()
        {
            var data = Import(Valid()).Catalog;
            var countries = data.Countries.ToList(); var stadiums = data.Stadiums.ToList();
            var copy = new DatabaseCatalog(data.DatabaseId, data.DatabaseRevision, data.Clubs, data.Players, data.Memberships,
                countries: countries, stadiums: stadiums, snapshot: data.Snapshot);
            countries.Clear(); stadiums.Clear();
            Assert.That(copy.Countries.Count, Is.EqualTo(1)); Assert.That(copy.Stadiums.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => new DatabaseCatalog(data.DatabaseId, data.DatabaseRevision, data.Clubs,
                data.Players, data.Memberships, countries: new[] {new CountryDefinition("AR", "Argentina")}, stadiums: data.Stadiums));
            Assert.Throws<ArgumentException>(() => new ClubDefinition("club", "Club", transferBudget: 10));
            Assert.Throws<ArgumentException>(() => new CountryDefinition("br", "Brasil"));
        }

        private static JObject Legacy() => JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
            "FootballSimulator/Code/FootballWorld/Tests/Fixtures/legacy-four-clubs.database.json")));
        private static JObject Valid()
        {
            var document = Legacy(); document["schemaVersion"] = 4;
            document["competitions"] = new JArray(); document["competitionEditions"] = new JArray();
            document["countries"] = new JArray(new JObject { ["code"] = "BR", ["name"] = "Brasil" });
            document["stadiums"] = new JArray(new JObject { ["id"] = "venue-a", ["name"] = "Estádio de teste", ["countryCode"] = "BR", ["city"] = "São Paulo", ["capacity"] = 12345 });
            document["snapshot"] = new JObject { ["date"] = "2026-01-11", ["label"] = "Recorte de teste", ["rosterScope"] = "matchday-squads", ["notes"] = "",
                ["sources"] = new JArray(new JObject { ["id"] = "source-a", ["title"] = "Documento de teste", ["url"] = "https://example.com/report.pdf" }) };
            foreach (var club in document["clubs"]) { club["countryCode"] = "BR"; club["city"] = "São Paulo"; }
            return document;
        }
        private DatabaseImportResult Import(JObject document) => importer.Import(document.ToString(Formatting.None));
        private static void Success(DatabaseImportResult result) => Assert.That(result.Success, Is.True,
            string.Join("; ", result.Errors.Select(error => error.Code + " " + error.Path + " " + error.Message)));
        private static void Failure(DatabaseImportResult result, string code, string path)
        {
            Assert.That(result.Success, Is.False); Assert.That(result.Catalog, Is.Null);
            Assert.That(result.Errors.Any(error => error.Code == code && error.Path == path), Is.True,
                string.Join("; ", result.Errors.Select(error => error.Code + " " + error.Path)));
        }
    }
}
