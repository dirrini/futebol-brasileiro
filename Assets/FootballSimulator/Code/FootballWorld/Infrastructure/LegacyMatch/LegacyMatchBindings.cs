using System;
using FStudio.Data;
using FStudio.Database;
using UnityEngine;

namespace FStudio.FootballWorld.Infrastructure.LegacyMatch
{
    [CreateAssetMenu(fileName = "LegacyMatchBindings", menuName = "Football World/Legacy match bindings")]
    public sealed class LegacyMatchBindings : ScriptableObject
    {
        public const string ResourcePath = "FootballWorld/LegacyMatchBindings";

        [Header("Declared visuals for clubs and players without a binding")]
        [Tooltip("Only the logo and home/away kits are read from this asset.")]
        public TeamEntry DefaultVisualTemplate;
        public Formations DefaultFormation = Formations._4_4_2;
        [Tooltip("Only skin, hair, facial hair, boots and sock colors are read. No sporting data is copied.")]
        public PlayerEntry DefaultPlayerAppearance;

        [Header("Stable catalog IDs; names and array order are not identity")]
        public ClubVisualBinding[] Clubs = Array.Empty<ClubVisualBinding>();
        public PlayerAppearanceBinding[] Players = Array.Empty<PlayerAppearanceBinding>();
    }

    [Serializable]
    public sealed class ClubVisualBinding
    {
        public string ClubId;
        [Tooltip("Only the logo and home/away kits are used; its roster, ratings and formation are ignored.")]
        public TeamEntry VisualTemplate;
        public Formations Formation = Formations._4_1_4_1;
    }

    [Serializable]
    public sealed class PlayerAppearanceBinding
    {
        public string PlayerId;
        [Tooltip("Only the seven cosmetic fields are used. Names, measurements and ratings come from the catalog.")]
        public PlayerEntry Appearance;
    }
}
