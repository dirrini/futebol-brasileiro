using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Infrastructure.LegacyMatch
{
    public sealed class CatalogCountryOption
    {
        public string Code { get; }
        public string Name { get; }
        public CatalogCountryOption(string code, string name) { Code = code ?? string.Empty; Name = name; }
    }

    // Selection contains IDs only. Filtering never changes the catalog or an edition's participants.
    public static class CatalogCountryFilter
    {
        public static IReadOnlyList<CatalogCountryOption> Countries(DatabaseCatalog catalog,
            IEnumerable<CatalogTeamOption> teams)
        {
            var codes = new HashSet<string>(teams.Select(team => team.CountryCode), StringComparer.Ordinal);
            var result = catalog == null ? new List<CatalogCountryOption>() : catalog.Countries
                .Where(country => codes.Contains(country.Code))
                .Select(country => new CatalogCountryOption(country.Code, country.Name)).ToList();
            if (codes.Contains(string.Empty)) result.Add(new CatalogCountryOption(string.Empty, string.Empty));
            return result.AsReadOnly();
        }

        public static IReadOnlyList<CatalogTeamOption> Teams(IEnumerable<CatalogTeamOption> teams, string countryCode)
            => teams.Where(team => countryCode != null && team.CountryCode == countryCode).ToArray();

        public static string RetainCountry(IReadOnlyList<CatalogCountryOption> countries, string selectedCode,
            IEnumerable<CatalogTeamOption> teams, string preferredClubId)
        {
            if (selectedCode != null && countries.Any(country => country.Code == selectedCode)) return selectedCode;
            var club = teams.FirstOrDefault(team => team.ClubId == preferredClubId);
            if (club != null && countries.Any(country => country.Code == club.CountryCode)) return club.CountryCode;
            return countries.FirstOrDefault()?.Code;
        }

        public static string RetainClub(IEnumerable<CatalogTeamOption> teams, string selectedId, string otherId = null)
        {
            var options = teams.ToArray();
            if (options.Any(team => team.ClubId == selectedId)) return selectedId;
            return options.FirstOrDefault(team => team.CanPlay && team.ClubId != otherId)?.ClubId ??
                options.FirstOrDefault(team => team.ClubId != otherId)?.ClubId ?? options.FirstOrDefault()?.ClubId;
        }
    }
}
