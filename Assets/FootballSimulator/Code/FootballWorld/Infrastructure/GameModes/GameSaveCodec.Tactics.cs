using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Application;
using Newtonsoft.Json.Linq;

namespace FStudio.FootballWorld.Infrastructure.GameModes
{
    public static partial class GameSaveCodec
    {
        // Explicit portable names keep save identities independent of enum order
        // and of translated labels in the tactical board.
        private static readonly IReadOnlyDictionary<string, CareerPlayerRole> TacticalRoleIds =
            new Dictionary<string, CareerPlayerRole>(StringComparer.Ordinal)
            {
                { "standard", CareerPlayerRole.Standard }, { "advancing-centre-back", CareerPlayerRole.AdvancingCentreBack },
                { "crossing-full-back", CareerPlayerRole.CrossingFullBack }, { "inverted-full-back", CareerPlayerRole.InvertedFullBack },
                { "ball-winning-midfielder", CareerPlayerRole.BallWinningMidfielder }, { "deep-lying-playmaker", CareerPlayerRole.DeepLyingPlaymaker },
                { "creative-playmaker", CareerPlayerRole.CreativePlaymaker }, { "target-forward", CareerPlayerRole.TargetForward },
                { "mobile-forward", CareerPlayerRole.MobileForward }, { "wide-winger", CareerPlayerRole.WideWinger },
                { "inside-forward", CareerPlayerRole.InsideForward }
            };

        private static JObject WriteTactics(CareerTacticPlan plan) => new JObject
        {
            ["version"] = plan.Version,
            ["slots"] = new JArray(plan.Slots.Select(slot => new JObject
            {
                ["slotId"] = slot.SlotId, ["x"] = slot.X, ["depth"] = slot.Depth,
                ["role"] = TacticalRoleIds.Single(value => value.Value == slot.Role).Key
            }))
        };

        private static CareerTacticPlan ReadTactics(JObject value, CareerFormation formation)
        {
            RequireFields(value, "version", "slots");
            var slots = List(value, "slots");
            if (slots.Count != 11) throw new InvalidOperationException("Saved tactical plans require all eleven slots.");
            var mapped = new List<CareerTacticSlot>();
            foreach (var token in slots)
            {
                var slot = Object(token); RequireFields(slot, "slotId", "x", "depth", "role");
                if (!TacticalRoleIds.TryGetValue(Text(slot, "role"), out var role)) throw new InvalidOperationException("Unsupported saved tactical role.");
                mapped.Add(new CareerTacticSlot(Text(slot, "slotId"), TacticalCoordinate(slot, "x"), TacticalCoordinate(slot, "depth"), role));
            }
            return new CareerTacticPlan(formation, mapped, Number(value, "version"));
        }

        private static float TacticalCoordinate(JObject value, string name)
        {
            var token = value[name];
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
                throw new InvalidOperationException("Invalid saved tactical coordinate: " + name);
            var coordinate = (double)token;
            if (double.IsNaN(coordinate) || double.IsInfinity(coordinate) || coordinate < 0 || coordinate > 1)
                throw new InvalidOperationException("Saved tactical coordinates must be finite normalized numbers.");
            return (float)coordinate;
        }
    }
}
