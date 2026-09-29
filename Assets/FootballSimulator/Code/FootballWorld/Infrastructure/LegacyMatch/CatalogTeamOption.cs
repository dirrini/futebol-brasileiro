using System.Collections.Generic;
using FStudio.Database;

namespace FStudio.FootballWorld.Infrastructure.LegacyMatch
{
    public sealed class CatalogTeamOption
    {
        public string ClubId { get; }
        public string Name { get; }
        public string CountryCode { get; }
        public TeamEntry Preview { get; }
        public LogoEntry Logo { get; }
        public string Error { get; }
        public string Warning { get; }
        public bool CanPlay => Preview != null && string.IsNullOrEmpty(Error);

        internal IReadOnlyList<string> PlayerIds { get; }

        internal CatalogTeamOption(string clubId, string name, TeamEntry preview, LogoEntry logo,
            string error, string warning, IReadOnlyList<string> playerIds, string countryCode = null)
        {
            ClubId = clubId;
            Name = name;
            CountryCode = countryCode ?? string.Empty;
            Preview = preview;
            Logo = logo;
            Error = error;
            Warning = warning;
            PlayerIds = playerIds;
        }
    }
}
