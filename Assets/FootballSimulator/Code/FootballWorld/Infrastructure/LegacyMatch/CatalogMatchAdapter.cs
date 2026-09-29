using System;
using System.Collections.Generic;
using FStudio.Data;
using FStudio.Database;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.DataContracts;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.GameModes;
using Shared.Responses;
using UnityEngine;

namespace FStudio.FootballWorld.Infrastructure.LegacyMatch
{
    // This adapter remains in Assembly-CSharp because the legacy engine is there.
    // The catalog and lineup planner do not reference Unity or legacy match objects.
    public sealed class CatalogMatchAdapter : IDisposable
    {
        private readonly Dictionary<string, CatalogTeamOption> teamsById =
            new Dictionary<string, CatalogTeamOption>(StringComparer.Ordinal);
        private readonly Dictionary<string, ClubVisualBinding> clubBindings =
            new Dictionary<string, ClubVisualBinding>(StringComparer.Ordinal);
        private readonly Dictionary<string, PlayerEntry> appearances =
            new Dictionary<string, PlayerEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualProfileData> profiles =
            new Dictionary<string, VisualProfileData>(StringComparer.Ordinal);
        private readonly LegacyMatchBindings bindings;
        private readonly CareerMatchOptions careerOptions;
        private bool disposed;

        public DatabaseCatalog Catalog { get; }
        public IReadOnlyList<CatalogTeamOption> Teams { get; }

        public CatalogMatchAdapter(DatabaseCatalog catalog, IReadOnlyList<VisualProfileData> visualProfiles,
            LegacyMatchBindings bindings, CareerMatchOptions careerOptions = null)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.bindings = bindings != null ? bindings : throw new ArgumentNullException(nameof(bindings));
            this.careerOptions = careerOptions;
            if (careerOptions != null) catalog.GetClub(careerOptions.ControlledClubId);
            if (visualProfiles == null) throw new ArgumentNullException(nameof(visualProfiles));

            foreach (var binding in bindings.Clubs ?? Array.Empty<ClubVisualBinding>())
            {
                if (binding == null || string.IsNullOrEmpty(binding.ClubId))
                    throw new ArgumentException("Club visual bindings require a stable ClubId.", nameof(bindings));
                clubBindings.Add(binding.ClubId, binding);
            }
            foreach (var binding in bindings.Players ?? Array.Empty<PlayerAppearanceBinding>())
            {
                if (binding == null || string.IsNullOrEmpty(binding.PlayerId))
                    throw new ArgumentException("Player appearance bindings require a stable PlayerId.", nameof(bindings));
                appearances.Add(binding.PlayerId, binding.Appearance);
            }
            foreach (var profile in visualProfiles)
            {
                if (profile == null || string.IsNullOrEmpty(profile.PlayerId))
                    throw new ArgumentException("Visual profiles require a stable PlayerId.", nameof(visualProfiles));
                profiles.Add(profile.PlayerId, profile);
            }

            var options = new List<CatalogTeamOption>(catalog.Clubs.Count);
            try
            {
                foreach (var club in catalog.Clubs)
                {
                    var option = CreateOption(club);
                    options.Add(option);
                    teamsById.Add(club.Id, option);
                }
                Teams = options.AsReadOnly();
            }
            catch
            {
                foreach (var option in options) LegacyMatchObjects.DestroyTeam(option.Preview);
                throw;
            }
        }

        public bool TryCreateMatch(string homeId, string awayId, out CatalogMatchLease lease, out string error)
        {
            lease = null;
            error = null;
            if (disposed)
            {
                error = GameText.Get("adapter.released");
                return false;
            }
            if (string.IsNullOrEmpty(homeId) || string.IsNullOrEmpty(awayId) ||
                !teamsById.TryGetValue(homeId, out var home) || !teamsById.TryGetValue(awayId, out var away))
            {
                error = GameText.Get("match.selectTeams");
                return false;
            }
            if (string.Equals(homeId, awayId, StringComparison.Ordinal))
            {
                error = GameText.Get("match.differentClubs");
                return false;
            }
            if (!home.CanPlay || !away.CanPlay)
            {
                error = !home.CanPlay ? home.Error : away.Error;
                return false;
            }

            var request = new MatchCreateRequest(home.Preview, away.Preview);
            if (careerOptions != null && (careerOptions.ControlledClubId == homeId || careerOptions.ControlledClubId == awayId))
                request.InitialUserTactic = careerOptions.LegacyMentality;
            try
            {
                if (careerOptions != null && careerOptions.ControlledClubId == homeId)
                    request.HomePlayerInstructions = careerOptions.CreateInstructions();
                else if (careerOptions != null && careerOptions.ControlledClubId == awayId)
                    request.AwayPlayerInstructions = careerOptions.CreateInstructions();
                PrepareOwnedClones(request.homeTeam);
                PrepareOwnedClones(request.awayTeam);
                var identities = new List<CatalogPlayerIdentity>(22);
                for (var i = 0; i < 11; i++)
                    identities.Add(new CatalogPlayerIdentity(request.homeTeam.Players[i].id, homeId, home.PlayerIds[i]));
                for (var i = 0; i < 11; i++)
                    identities.Add(new CatalogPlayerIdentity(request.awayTeam.Players[i].id, awayId, away.PlayerIds[i]));
                lease = new CatalogMatchLease(request, Catalog, homeId, awayId, identities);
                return true;
            }
            catch
            {
                LegacyMatchObjects.DestroyTeam(request.homeTeam);
                LegacyMatchObjects.DestroyTeam(request.awayTeam);
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var option in Teams) LegacyMatchObjects.DestroyTeam(option.Preview);
        }

        private CatalogTeamOption CreateOption(ClubDefinition club)
        {
            var hasBinding = clubBindings.TryGetValue(club.Id, out var binding);
            var template = hasBinding ? binding.VisualTemplate : bindings.DefaultVisualTemplate;
            var formation = hasBinding ? binding.Formation : bindings.DefaultFormation;
            if (careerOptions != null && careerOptions.ControlledClubId == club.Id)
                formation = careerOptions.LegacyFormation;
            var warnings = new List<string>();
            if (!hasBinding)
                warnings.Add(GameText.Get("adapter.genericClub"));
            var logo = template != null ? template.TeamLogo : null;
            var visualError = ValidateTeamVisuals(template);
            if (visualError != null) return Invalid(club, logo, visualError, warnings);

            Positions[] legacySlots;
            try { legacySlots = FormationRules.GetTeamFormation(formation).Positions; }
            catch (NotImplementedException)
            {
                return Invalid(club, logo, GameText.Get("adapter.formation"), warnings);
            }
            var slots = new PlayerPosition[legacySlots.Length];
            for (var i = 0; i < slots.Length; i++) slots[i] = ToCatalogPosition(legacySlots[i]);
            var plan = LineupPlanner.Plan(Catalog, club.Id, slots);
            if (!plan.Success)
            {
                var rosterCount = Catalog.GetRoster(club.Id).Count;
                var message = rosterCount < 11
                    ? GameText.Get("adapter.shortRoster", rosterCount)
                    : GameText.Get("adapter.goalkeeper");
                return Invalid(club, logo, message, warnings);
            }

            // Only the selected eleven need renderable visuals. Reserve players remain
            // in the complete catalog and are validated if selected in a future match.
            foreach (var player in plan.Players)
            {
                if (profiles.TryGetValue(player.Id, out var profile) && !IsBuiltin(profile.Skin))
                    return Invalid(club, logo,
                        GameText.Get("adapter.unsupportedVisual", player.Name), warnings);
            }

            var selectedAppearances = new PlayerEntry[11];
            var usesDefaultAppearance = false;
            for (var i = 0; i < plan.Players.Count; i++)
            {
                // A complete portable appearance owns all seven cosmetic fields.
                // It does not need a local PlayerEntry or a declared legacy fallback.
                if (profiles.TryGetValue(plan.Players[i].Id, out var profile) && profile.Appearance != null)
                    continue;
                if (!appearances.TryGetValue(plan.Players[i].Id, out selectedAppearances[i]))
                {
                    selectedAppearances[i] = bindings.DefaultPlayerAppearance;
                    usesDefaultAppearance = true;
                }
                if (selectedAppearances[i] == null)
                    return Invalid(club, logo, GameText.Get("adapter.missingAppearance", plan.Players[i].Name), warnings);
            }
            if (usesDefaultAppearance) warnings.Add(GameText.Get("adapter.defaultAppearance"));
            if (plan.OutOfPositionPlayerIds.Count > 0)
            {
                warnings.Add(GameText.Get("adapter.outOfPosition", plan.OutOfPositionPlayerIds.Count));
            }

            var team = ScriptableObject.CreateInstance<TeamEntry>();
            team.hideFlags = HideFlags.DontSave;
            try
            {
                team.name = club.Name;
                team.TeamName = club.Name;
                team.TeamLogo = template.TeamLogo;
                team.HomeKit = template.HomeKit;
                team.AwayKit = template.AwayKit;
                team.Formation = formation;
                team.Players = new PlayerEntry[11];
                var playerIds = new List<string>(11);
                for (var i = 0; i < 11; i++)
                {
                    var player = ScriptableObject.CreateInstance<PlayerEntry>();
                    team.Players[i] = player;
                    player.hideFlags = HideFlags.DontSave;
                    player.team = team;
                    ApplyCatalogPlayer(player, plan.Players[i]);
                    if (profiles.TryGetValue(plan.Players[i].Id, out var profile) && profile.Appearance != null)
                        BuiltinAppearanceMapper.Apply(player, profile.Appearance);
                    else
                        ApplyAppearance(player, selectedAppearances[i]);
                    playerIds.Add(plan.Players[i].Id);
                }
                team.IsValid = true;
                return new CatalogTeamOption(club.Id, club.Name, team, logo, null,
                    Warning(warnings), playerIds.AsReadOnly(), club.CountryCode);
            }
            catch
            {
                LegacyMatchObjects.DestroyTeam(team);
                throw;
            }
        }

        private static CatalogTeamOption Invalid(ClubDefinition club, LogoEntry logo, string error, List<string> warnings)
            => new CatalogTeamOption(club.Id, club.Name, null, logo, error, Warning(warnings), Array.Empty<string>(), club.CountryCode);

        private static string Warning(List<string> warnings) => warnings.Count == 0 ? null : string.Join(" ", warnings);

        private static string ValidateTeamVisuals(TeamEntry template)
        {
            if (template == null || template.TeamLogo == null || template.TeamLogo.TeamLogoMaterial == null)
                return GameText.Get("adapter.missingBadge");
            if (!HasKit(template.HomeKit) || !HasKit(template.AwayKit))
                return GameText.Get("adapter.missingKits");
            return null;
        }

        private static bool HasKit(KitEntry kit) => kit != null && kit.PreviewTexture != null &&
            kit.KitMaterial != null && kit.GKKitMaterial != null;

        private static bool IsBuiltin(SkinReferenceData skin) => skin != null &&
            string.Equals(skin.SkinId, "builtin-player", StringComparison.Ordinal) && skin.Revision == 1 &&
            string.Equals(skin.CompatibilityProfile, "football-player-v1", StringComparison.Ordinal);

        private static void PrepareOwnedClones(TeamEntry team)
        {
            team.hideFlags = HideFlags.DontSave;
            team.name = team.TeamName;
            team.IsValid = true;
            foreach (var player in team.Players)
            {
                player.hideFlags = HideFlags.DontSave;
                // Legacy jersey rendering reads UnityEngine.Object.name; Clone only copies Name.
                player.name = player.Name;
            }
        }

        private static void ApplyCatalogPlayer(PlayerEntry target, PlayerDefinition source)
        {
            target.Name = source.DisplayName;
            target.name = source.DisplayName;
            target.height = source.HeightCm;
            target.weight = source.WeightKg;
            var attributes = source.Attributes;
            target.strength = attributes.Strength;
            target.acceleration = attributes.Acceleration;
            target.topSpeed = attributes.TopSpeed;
            target.dribbleSpeed = attributes.DribbleSpeed;
            target.jump = attributes.Jump;
            target.tackling = attributes.Tackling;
            target.ballKeeping = attributes.BallKeeping;
            target.passing = attributes.Passing;
            target.longBall = attributes.LongBall;
            target.agility = attributes.Agility;
            target.shooting = attributes.Shooting;
            target.shootPower = attributes.ShootPower;
            target.positioning = attributes.Positioning;
            target.reaction = attributes.Reaction;
            target.ballControl = attributes.BallControl;
        }

        private static void ApplyAppearance(PlayerEntry target, PlayerEntry source)
        {
            target.SkinColor = source.SkinColor;
            target.HairStyles = source.HairStyles;
            target.HairColor = source.HairColor;
            target.FacialHairStyles = source.FacialHairStyles;
            target.FacialHairColor = source.FacialHairColor;
            target.BootColor = source.BootColor;
            target.SockAccessoryColor = source.SockAccessoryColor;
        }

        internal static PlayerPosition ToCatalogPosition(Positions slot)
        {
            switch (PositionRules.GetBasePosition(slot))
            {
                case Positions.GK: return PlayerPosition.GK;
                case Positions.RB: return PlayerPosition.RB;
                case Positions.LB: return PlayerPosition.LB;
                case Positions.CB: return PlayerPosition.CB;
                case Positions.DMF: return PlayerPosition.DM;
                case Positions.CM: return PlayerPosition.CM;
                case Positions.RMF: return PlayerPosition.RM;
                case Positions.LMF: return PlayerPosition.LM;
                case Positions.AMF: return PlayerPosition.AM;
                case Positions.LW: return PlayerPosition.LW;
                case Positions.RW: return PlayerPosition.RW;
                case Positions.ST: return PlayerPosition.ST;
                default: throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unsupported legacy formation slot.");
            }
        }
    }
}
