#nullable enable

using System;
using System.Collections.Generic;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public sealed class CatalogSession
    {
        /// <summary>Null until a validated catalog has been activated.</summary>
        public DatabaseCatalog? ActiveCatalog { get; private set; }

        public void Activate(DatabaseCatalog catalog)
        {
            ActiveCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public IReadOnlyList<PlayerDefinition> GetRoster(string clubId)
        {
            var catalog = ActiveCatalog;
            if (catalog == null)
            {
                throw new InvalidOperationException("No database catalog is active. Activate a catalog before querying a roster.");
            }

            return catalog.GetRoster(clubId);
        }
    }
}
