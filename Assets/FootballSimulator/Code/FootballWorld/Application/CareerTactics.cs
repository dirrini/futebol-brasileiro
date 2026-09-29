using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public enum CareerPlayerRole
    {
        Standard, AdvancingCentreBack, CrossingFullBack, InvertedFullBack,
        BallWinningMidfielder, DeepLyingPlaymaker, CreativePlaymaker,
        TargetForward, MobileForward, WideWinger, InsideForward
    }

    // An immutable instruction for a football role slot, never for a player ID.
    // Coordinates use the attacking frame: depth zero is the controlled own goal.
    public sealed class CareerTacticSlot
    {
        public string SlotId { get; }
        public float X { get; }
        public float Depth { get; }
        public CareerPlayerRole Role { get; }
        public CareerTacticSlot(string slotId, float x, float depth, CareerPlayerRole role)
        {
            SlotId = CompetitionIdentity.Validate(slotId);
            X = Coordinate(x); Depth = Coordinate(depth);
            if (!Enum.IsDefined(typeof(CareerPlayerRole), role)) throw new ArgumentException("Unknown player role.", nameof(role));
            Role = role;
        }
        private static float Coordinate(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(nameof(value), "Tactical coordinates must be finite values between zero and one.");
            return value;
        }
    }

    public sealed class CareerTacticSlotDefinition
    {
        public string SlotId { get; }
        public PlayerPosition Position { get; }
        public float DefaultX { get; }
        public float DefaultDepth { get; }
        public float MinX { get; }
        public float MaxX { get; }
        public float MinDepth { get; }
        public float MaxDepth { get; }
        public IReadOnlyList<CareerPlayerRole> AllowedRoles { get; }
        internal CareerTacticSlotDefinition(string slotId, PlayerPosition position, float x, float depth)
        {
            SlotId = slotId; Position = position; DefaultX = x; DefaultDepth = depth;
            MinX = position == PlayerPosition.GK ? .35f : Math.Max(.05f, x - .18f);
            MaxX = position == PlayerPosition.GK ? .65f : Math.Min(.95f, x + .18f);
            MinDepth = position == PlayerPosition.GK ? .05f : Math.Max(.05f, depth - .20f);
            MaxDepth = position == PlayerPosition.GK ? .20f : Math.Min(.95f, depth + .20f);
            var roles = new List<CareerPlayerRole> { CareerPlayerRole.Standard };
            switch (position)
            {
                case PlayerPosition.CB: roles.Add(CareerPlayerRole.AdvancingCentreBack); break;
                case PlayerPosition.LB:
                case PlayerPosition.RB:
                    roles.Add(CareerPlayerRole.CrossingFullBack); roles.Add(CareerPlayerRole.InvertedFullBack); break;
                case PlayerPosition.DM:
                case PlayerPosition.CM:
                    roles.Add(CareerPlayerRole.BallWinningMidfielder); roles.Add(CareerPlayerRole.DeepLyingPlaymaker);
                    if (position == PlayerPosition.CM) roles.Add(CareerPlayerRole.CreativePlaymaker);
                    break;
                case PlayerPosition.AM: roles.Add(CareerPlayerRole.CreativePlaymaker); break;
                case PlayerPosition.ST:
                    roles.Add(CareerPlayerRole.TargetForward); roles.Add(CareerPlayerRole.MobileForward); break;
                case PlayerPosition.LM:
                case PlayerPosition.RM:
                case PlayerPosition.LW:
                case PlayerPosition.RW:
                    roles.Add(CareerPlayerRole.WideWinger); roles.Add(CareerPlayerRole.InsideForward); break;
            }
            AllowedRoles = roles.AsReadOnly();
        }
        internal void Validate(CareerTacticSlot slot)
        {
            if (slot.X < MinX || slot.X > MaxX || slot.Depth < MinDepth || slot.Depth > MaxDepth)
                throw new ArgumentException("Tactical slot lies outside its permitted adjustment area: " + SlotId);
            if (!AllowedRoles.Contains(slot.Role)) throw new ArgumentException("The selected player role is incompatible with tactical slot: " + SlotId);
        }
    }

    public sealed class CareerTacticPlan
    {
        private static readonly IReadOnlyDictionary<CareerFormation, IReadOnlyList<CareerTacticSlotDefinition>> Definitions = CreateDefinitions();
        private readonly Dictionary<string, CareerTacticSlot> slotsById;
        public int Version { get; }
        public CareerFormation Formation { get; }
        public IReadOnlyList<CareerTacticSlot> Slots { get; }
        public bool IsDefault => GetSlotDefinitions(Formation).All(definition =>
        {
            var slot = slotsById[definition.SlotId];
            return slot.X == definition.DefaultX && slot.Depth == definition.DefaultDepth && slot.Role == CareerPlayerRole.Standard;
        });

        public CareerTacticPlan(CareerFormation formation, IEnumerable<CareerTacticSlot> slots, int version = 1)
        {
            if (version != 1) throw new ArgumentException("Unsupported tactical plan version.", nameof(version));
            var definitions = GetSlotDefinitions(formation);
            var values = new List<CareerTacticSlot>(slots ?? throw new ArgumentNullException(nameof(slots)));
            if (values.Count != 11) throw new ArgumentException("A tactical plan requires all eleven role slots.", nameof(slots));
            slotsById = new Dictionary<string, CareerTacticSlot>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (value == null || slotsById.ContainsKey(value.SlotId)) throw new ArgumentException("Tactical slots must have unique non-null identities.");
                slotsById.Add(value.SlotId, value);
            }
            var ordered = new List<CareerTacticSlot>();
            foreach (var definition in definitions)
            {
                if (!slotsById.TryGetValue(definition.SlotId, out var value)) throw new ArgumentException("Tactical slots do not match the selected formation.");
                definition.Validate(value); ordered.Add(value);
            }
            Formation = formation; Version = version; Slots = ordered.AsReadOnly();
        }

        public static CareerTacticPlan CreateDefault(CareerFormation formation) => new CareerTacticPlan(formation,
            GetSlotDefinitions(formation).Select(value => new CareerTacticSlot(value.SlotId, value.DefaultX, value.DefaultDepth, CareerPlayerRole.Standard)));

        public static IReadOnlyList<CareerTacticSlotDefinition> GetSlotDefinitions(CareerFormation formation)
        {
            if (!Definitions.TryGetValue(formation, out var definitions)) throw new ArgumentException("Unsupported career formation.", nameof(formation));
            return definitions;
        }

        public CareerTacticSlot GetSlot(string slotId)
        {
            if (slotId == null || !slotsById.TryGetValue(slotId, out var slot)) throw new ArgumentException("Unknown tactical slot.", nameof(slotId));
            return slot;
        }

        public CareerTacticPlan WithSlot(string slotId, float x, float depth, CareerPlayerRole role)
        {
            GetSlot(slotId);
            var replacement = new CareerTacticSlot(slotId, x, depth, role);
            return new CareerTacticPlan(Formation, Slots.Select(value => value.SlotId == slotId ? replacement : value), Version);
        }

        public bool SameConfiguration(CareerTacticPlan other) => other != null && Formation == other.Formation && Version == other.Version &&
            Slots.All(value => { var candidate = other.GetSlot(value.SlotId); return candidate.X == value.X && candidate.Depth == value.Depth && candidate.Role == value.Role; });

        private static IReadOnlyDictionary<CareerFormation, IReadOnlyList<CareerTacticSlotDefinition>> CreateDefinitions()
        {
            var definitions = new Dictionary<CareerFormation, IReadOnlyList<CareerTacticSlotDefinition>>();
            definitions.Add(CareerFormation.FourFourTwo, Layout(
                Slot("left-midfielder", PlayerPosition.LM, .11f, .60f),
                Slot("left-central-midfielder", PlayerPosition.CM, .37f, .54f),
                Slot("right-central-midfielder", PlayerPosition.CM, .63f, .54f),
                Slot("right-midfielder", PlayerPosition.RM, .89f, .60f),
                Slot("left-striker", PlayerPosition.ST, .35f, .85f), Slot("right-striker", PlayerPosition.ST, .65f, .85f)));
            definitions.Add(CareerFormation.FourThreeThree, Layout(
                Slot("left-midfielder", PlayerPosition.LM, .16f, .56f), Slot("central-midfielder", PlayerPosition.CM, .50f, .51f),
                Slot("right-midfielder", PlayerPosition.RM, .84f, .56f), Slot("left-winger", PlayerPosition.LW, .16f, .85f),
                Slot("right-winger", PlayerPosition.RW, .84f, .85f), Slot("striker", PlayerPosition.ST, .50f, .83f)));
            definitions.Add(CareerFormation.FourTwoThreeOne, Layout(
                Slot("left-holding-midfielder", PlayerPosition.DM, .35f, .51f), Slot("right-holding-midfielder", PlayerPosition.DM, .65f, .51f),
                Slot("left-midfielder", PlayerPosition.LM, .12f, .71f), Slot("right-midfielder", PlayerPosition.RM, .88f, .71f),
                Slot("attacking-midfielder", PlayerPosition.AM, .50f, .72f), Slot("striker", PlayerPosition.ST, .50f, .90f)));
            return new System.Collections.ObjectModel.ReadOnlyDictionary<CareerFormation, IReadOnlyList<CareerTacticSlotDefinition>>(definitions);
        }

        private static CareerTacticSlotDefinition Slot(string id, PlayerPosition position, float x, float depth)
            => new CareerTacticSlotDefinition(id, position, x, depth);
        private static IReadOnlyList<CareerTacticSlotDefinition> Layout(params CareerTacticSlotDefinition[] outfield)
        {
            var layout = new List<CareerTacticSlotDefinition>
            {
                Slot("goalkeeper", PlayerPosition.GK, .50f, .12f), Slot("left-back", PlayerPosition.LB, .11f, .31f),
                Slot("left-centre-back", PlayerPosition.CB, .37f, .27f), Slot("right-centre-back", PlayerPosition.CB, .63f, .27f),
                Slot("right-back", PlayerPosition.RB, .89f, .31f)
            };
            layout.AddRange(outfield); return layout.AsReadOnly();
        }
    }
}
