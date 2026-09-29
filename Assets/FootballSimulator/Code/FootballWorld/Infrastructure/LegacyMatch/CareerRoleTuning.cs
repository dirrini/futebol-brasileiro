using System;
using FStudio.FootballWorld.Application;
using FStudio.MatchEngine.Tactics;
using UnityEngine;

namespace FStudio.FootballWorld.Infrastructure.LegacyMatch
{
    [CreateAssetMenu(menuName = "Football World/Career role tuning")]
    public sealed class CareerRoleTuning : ScriptableObject
    {
        public const string ResourcePath = "FootballWorld/CareerRoleTuning";

        [Serializable]
        public sealed class RoleProfile
        {
            public CareerPlayerRole Role;
            [Header("Shape offsets / fraction of pitch")]
            [Range(-.2f, .2f)] public float AttackDepthOffset;
            [Range(-.2f, .2f)] public float DefenceDepthOffset;
            [Range(0, 1)] public float CentrePull;
            [Range(0, 1)] public float WidthExpansion;
            [Range(0, .5f)] public float SupportToBall;
            [Header("Defence and attacking runs / 1 = unchanged")]
            [Range(0, 3)] public float MarkingWeight = 1;
            [Range(.25f, 2)] public float MarkingDistance = 1;
            [Range(0, 3)] public float JoinAttack = 1;
            [Range(0, 4)] public float CrossingChance = 1;
            [Header("AI pass scores; no changes to passing skill")]
            [Range(-100, 100)] public float ShortPassPriority;
            [Range(-100, 100)] public float LongPassPriority;
            [Range(-3, 3)] public float ForwardPassPriority;
            [Range(0, 100)] public float ReceivePassPriority;
            [Range(0, 3)] public float LayoffPriority;
            public bool EarlyPass;
            public bool EarlyCross;
            public bool PreferLayoff;

            public RoleTacticalSettings Snapshot()
            {
                RequireRange(AttackDepthOffset, -.2f, .2f, nameof(AttackDepthOffset));
                RequireRange(DefenceDepthOffset, -.2f, .2f, nameof(DefenceDepthOffset));
                RequireRange(CentrePull, 0, 1, nameof(CentrePull));
                RequireRange(WidthExpansion, 0, 1, nameof(WidthExpansion));
                RequireRange(SupportToBall, 0, .5f, nameof(SupportToBall));
                RequireRange(MarkingWeight, 0, 3, nameof(MarkingWeight));
                RequireRange(MarkingDistance, .25f, 2, nameof(MarkingDistance));
                RequireRange(JoinAttack, 0, 3, nameof(JoinAttack));
                RequireRange(CrossingChance, 0, 4, nameof(CrossingChance));
                RequireRange(ShortPassPriority, -100, 100, nameof(ShortPassPriority));
                RequireRange(LongPassPriority, -100, 100, nameof(LongPassPriority));
                RequireRange(ForwardPassPriority, -3, 3, nameof(ForwardPassPriority));
                RequireRange(ReceivePassPriority, 0, 100, nameof(ReceivePassPriority));
                RequireRange(LayoffPriority, 0, 3, nameof(LayoffPriority));
                return new RoleTacticalSettings(AttackDepthOffset, DefenceDepthOffset,
                    CentrePull, WidthExpansion, SupportToBall, MarkingWeight, MarkingDistance, JoinAttack,
                    CrossingChance, ShortPassPriority, LongPassPriority, ForwardPassPriority, ReceivePassPriority,
                    LayoffPriority, EarlyPass, EarlyCross, PreferLayoff);
            }

            private static void RequireRange(float value, float min, float max, string field)
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                    throw new InvalidOperationException("Career role tuning is outside its supported range: " + field);
            }
        }

        public RoleProfile[] Profiles = Array.Empty<RoleProfile>();

        public RoleTacticalSettings Resolve(CareerPlayerRole role)
        {
            if (role == CareerPlayerRole.Standard) return RoleTacticalSettings.Standard;
            RoleProfile found = null;
            foreach (var profile in Profiles ?? Array.Empty<RoleProfile>())
            {
                if (profile == null || profile.Role != role) continue;
                if (found != null) throw new InvalidOperationException("Duplicate career role tuning: " + role);
                found = profile;
            }
            if (found == null) throw new InvalidOperationException("Missing career role tuning: " + role);
            return found.Snapshot();
        }
    }
}
