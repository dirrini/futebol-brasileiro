using System;
using FStudio.Data;
using UnityEngine;

namespace FStudio.MatchEngine.Tactics
{
    // Match-owned values. No persistent TeamTactics/PlayerEntry is changed.
    public sealed class PlayerTacticalInstruction
    {
        public string SlotId { get; }
        public Positions Position { get; }
        public float HorizontalOffset { get; }
        public float DepthOffset { get; }
        public RoleTacticalSettings Role { get; }
        public bool HasPositionAdjustment => HorizontalOffset != 0 || DepthOffset != 0;

        public PlayerTacticalInstruction(string slotId, Positions position, float horizontalOffset,
            float depthOffset, RoleTacticalSettings role)
        {
            if (string.IsNullOrWhiteSpace(slotId)) throw new ArgumentException("A tactical slot is required.");
            if (float.IsNaN(horizontalOffset) || float.IsInfinity(horizontalOffset) ||
                float.IsNaN(depthOffset) || float.IsInfinity(depthOffset)) throw new ArgumentException("Invalid tactical offset.");
            SlotId = slotId;
            Position = position;
            HorizontalOffset = horizontalOffset;
            DepthOffset = depthOffset;
            Role = role ?? RoleTacticalSettings.Standard;
        }

        public Vector3 ApplyPosition(Vector3 position, Vector3 goalDirection, int length, int width,
            bool hasPossession, Vector3 ballPosition)
        {
            // Width and depth both rotate when the side attacks the opposite goal.
            position.x += DepthOffset * length * goalDirection.x;
            position.z += HorizontalOffset * width * goalDirection.x;
            position.x += (hasPossession ? Role.AttackDepthOffset : Role.DefenceDepthOffset) * length * goalDirection.x;
            if (hasPossession)
            {
                position.z = ApplyWidth(position.z, width, Role.CentrePull, Role.WidthExpansion);
                ballPosition.y = position.y;
                position = Vector3.Lerp(position, ballPosition, Role.SupportToBall);
            }
            return position;
        }

        public static float ApplyWidth(float lateral, int width, float centrePull, float expansion)
            => Mathf.Lerp(lateral + (lateral - width / 2f) * expansion, width / 2f, centrePull);

        public Vector3 ApplyBoundedPosition(Vector3 position, Vector3 goalDirection, int length, int width,
            bool hasPossession, Vector3 ballPosition)
        {
            position = ApplyPosition(position, goalDirection, length, width, hasPossession, ballPosition);
            position.x = Mathf.Clamp(position.x, 0, length);
            position.z = Mathf.Clamp(position.z, 0, width);
            return position;
        }

        public float PassPriority(bool longPass, float forwardDistance, float receiverBonus)
            => (longPass ? Role.LongPassPriority : Role.ShortPassPriority) +
                Mathf.Clamp(forwardDistance, -30, 30) * Role.ForwardPassPriority +
                (Role.PreferLayoff ? Mathf.Clamp(-forwardDistance, 0, 20) * Role.LayoffPriority : 0) +
                receiverBonus;
    }

    public sealed class RoleTacticalSettings
    {
        public static readonly RoleTacticalSettings Standard = new RoleTacticalSettings();
        public float AttackDepthOffset { get; }
        public float DefenceDepthOffset { get; }
        public float CentrePull { get; }
        public float WidthExpansion { get; }
        public float SupportToBall { get; }
        public float MarkingWeight { get; }
        public float MarkingDistance { get; }
        public float JoinAttack { get; }
        public float CrossingChance { get; }
        public float ShortPassPriority { get; }
        public float LongPassPriority { get; }
        public float ForwardPassPriority { get; }
        public float ReceivePassPriority { get; }
        public float LayoffPriority { get; }
        public bool EarlyPass { get; }
        public bool EarlyCross { get; }
        public bool PreferLayoff { get; }

        public RoleTacticalSettings(float attackDepthOffset = 0, float defenceDepthOffset = 0,
            float centrePull = 0, float widthExpansion = 0, float supportToBall = 0,
            float markingWeight = 1, float markingDistance = 1, float joinAttack = 1,
            float crossingChance = 1, float shortPassPriority = 0, float longPassPriority = 0,
            float forwardPassPriority = 0, float receivePassPriority = 0, float layoffPriority = 0,
            bool earlyPass = false, bool earlyCross = false, bool preferLayoff = false)
        {
            AttackDepthOffset = attackDepthOffset; DefenceDepthOffset = defenceDepthOffset;
            CentrePull = centrePull; WidthExpansion = widthExpansion; SupportToBall = supportToBall;
            MarkingWeight = markingWeight; MarkingDistance = markingDistance; JoinAttack = joinAttack;
            CrossingChance = crossingChance; ShortPassPriority = shortPassPriority; LongPassPriority = longPassPriority;
            ForwardPassPriority = forwardPassPriority; ReceivePassPriority = receivePassPriority;
            LayoffPriority = layoffPriority; EarlyPass = earlyPass; EarlyCross = earlyCross; PreferLayoff = preferLayoff;
        }
    }
}
