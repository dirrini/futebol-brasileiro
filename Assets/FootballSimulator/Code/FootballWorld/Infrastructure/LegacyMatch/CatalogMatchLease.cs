using System;
using System.Collections.Generic;
using FStudio.Database;
using FStudio.FootballWorld.Domain;
using Shared.Responses;
using UnityEngine;

namespace FStudio.FootballWorld.Infrastructure.LegacyMatch
{
    public sealed class CatalogPlayerIdentity
    {
        public int LocalId { get; }
        public string ClubId { get; }
        public string PlayerId { get; }

        internal CatalogPlayerIdentity(int localId, string clubId, string playerId)
        {
            LocalId = localId;
            ClubId = clubId;
            PlayerId = playerId;
        }
    }

    // Retain this lease through the preparation UI and the entire match. Dispose only
    // after consumers have unloaded, including cancellation or failure before kickoff.
    public sealed class CatalogMatchLease : IDisposable
    {
        public MatchCreateRequest Request { get; }
        public DatabaseCatalog Catalog { get; }
        public string HomeClubId { get; }
        public string AwayClubId { get; }
        public IReadOnlyList<CatalogPlayerIdentity> Players { get; }
        public bool IsDisposed { get; private set; }

        internal CatalogMatchLease(MatchCreateRequest request, DatabaseCatalog catalog,
            string homeClubId, string awayClubId, IEnumerable<CatalogPlayerIdentity> players)
        {
            Request = request;
            Catalog = catalog;
            HomeClubId = homeClubId;
            AwayClubId = awayClubId;
            Players = new List<CatalogPlayerIdentity>(players).AsReadOnly();
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            LegacyMatchObjects.DestroyTeam(Request.homeTeam);
            LegacyMatchObjects.DestroyTeam(Request.awayTeam);
        }
    }

    internal static class LegacyMatchObjects
    {
        // Called only for the teams and players allocated by this adapter/request.
        // Kits, logos, textures and appearance source assets are borrowed, never owned.
        internal static void DestroyTeam(TeamEntry team)
        {
            if (team == null) return;
            if (team.Players != null)
            {
                foreach (var player in team.Players) Destroy(player);
            }
            Destroy(team);
        }

        private static void Destroy(UnityEngine.Object ownedObject)
        {
            if (ownedObject == null) return;
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(ownedObject);
            else UnityEngine.Object.DestroyImmediate(ownedObject);
        }
    }
}
