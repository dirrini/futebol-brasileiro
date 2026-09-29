using System;
using System.Collections.Generic;
using FStudio.Data;
using FStudio.FootballWorld.Application;
using FStudio.MatchEngine.Tactics;
using UnityEngine;

namespace FStudio.FootballWorld.Infrastructure.LegacyMatch
{
    // A snapshot of career choices for an adapter/lease. The portable career
    // owns the choices; only this boundary maps them to legacy engine types.
    public sealed class CareerMatchOptions
    {
        public string ControlledClubId { get; }
        public CareerFormation Formation { get; }
        public CareerMentality Mentality { get; }
        public CareerTacticPlan TacticPlan { get; }

        public CareerMatchOptions(string controlledClubId, CareerFormation formation, CareerMentality mentality,
            CareerTacticPlan tacticPlan = null)
        {
            if (string.IsNullOrWhiteSpace(controlledClubId)) throw new ArgumentException("A controlled club identity is required.", nameof(controlledClubId));
            if (!Enum.IsDefined(typeof(CareerFormation), formation)) throw new ArgumentException("Unsupported career formation.", nameof(formation));
            if (!Enum.IsDefined(typeof(CareerMentality), mentality)) throw new ArgumentException("Unsupported career mentality.", nameof(mentality));
            ControlledClubId = controlledClubId;
            Formation = formation;
            Mentality = mentality;
            TacticPlan = tacticPlan ?? CareerTacticPlan.CreateDefault(formation);
            if (TacticPlan.Formation != formation) throw new ArgumentException("The tactical plan must match its formation.", nameof(tacticPlan));
        }

        public IReadOnlyList<PlayerTacticalInstruction> CreateInstructions(CareerRoleTuning tuning = null)
        {
            var definitions = CareerTacticPlan.GetSlotDefinitions(Formation);
            var positions = FormationRules.GetTeamFormation(LegacyFormation).Positions;
            var instructions = new PlayerTacticalInstruction[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                var definition = definitions[i];
                var slot = TacticPlan.GetSlot(definition.SlotId);
                var role = RoleTacticalSettings.Standard;
                if (slot.Role != CareerPlayerRole.Standard)
                {
                    if (tuning == null) tuning = Resources.Load<CareerRoleTuning>(CareerRoleTuning.ResourcePath);
                    if (tuning == null) throw new InvalidOperationException("CareerRoleTuning is missing from Resources/FootballWorld.");
                    role = tuning.Resolve(slot.Role);
                }
                instructions[i] = new PlayerTacticalInstruction(slot.SlotId, positions[i],
                    definition.DefaultX - slot.X, slot.Depth - definition.DefaultDepth, role);
            }
            return Array.AsReadOnly(instructions);
        }

        internal Formations LegacyFormation
        {
            get
            {
                switch (Formation)
                {
                    case CareerFormation.FourFourTwo: return Formations._4_4_2;
                    case CareerFormation.FourThreeThree: return Formations._4_3_3;
                    case CareerFormation.FourTwoThreeOne: return Formations._4_2_3_1_A;
                    default: throw new InvalidOperationException("Unsupported career formation.");
                }
            }
        }

        internal TacticPresetTypes LegacyMentality
        {
            get
            {
                switch (Mentality)
                {
                    case CareerMentality.Defensive: return TacticPresetTypes.Defensive;
                    case CareerMentality.Balanced: return TacticPresetTypes.Balanced;
                    case CareerMentality.Attacking: return TacticPresetTypes.Offensive;
                    default: throw new InvalidOperationException("Unsupported career mentality.");
                }
            }
        }
    }
}
