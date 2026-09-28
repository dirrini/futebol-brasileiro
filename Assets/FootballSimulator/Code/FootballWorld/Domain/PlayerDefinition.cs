using System;
using System.Collections.Generic;

namespace FStudio.FootballWorld.Domain
{
    public sealed class PlayerDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<PlayerPosition> NaturalPositions { get; }
        public int HeightCm { get; }
        public int WeightKg { get; }
        public PlayerAttributes Attributes { get; }

        public PlayerDefinition(
            string id,
            string name,
            IReadOnlyList<PlayerPosition> naturalPositions,
            int heightCm,
            int weightKg,
            PlayerAttributes attributes)
        {
            Id = DomainValidation.Id(id, nameof(id));
            Name = DomainValidation.Name(name, nameof(name));
            HeightCm = DomainValidation.InRange(heightCm, 150, 210, nameof(heightCm));
            WeightKg = DomainValidation.InRange(weightKg, 45, 100, nameof(weightKg));
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));

            if (naturalPositions == null)
            {
                throw new ArgumentNullException(nameof(naturalPositions));
            }

            if (naturalPositions.Count == 0)
            {
                throw new ArgumentException("A player must have at least one natural position.", nameof(naturalPositions));
            }

            var positions = new List<PlayerPosition>(naturalPositions.Count);
            var uniquePositions = new HashSet<PlayerPosition>();
            foreach (var position in naturalPositions)
            {
                if (!Enum.IsDefined(typeof(PlayerPosition), position))
                {
                    throw new ArgumentException($"Unknown player position: {position}.", nameof(naturalPositions));
                }

                if (!uniquePositions.Add(position))
                {
                    throw new ArgumentException($"Duplicate natural position: {position}.", nameof(naturalPositions));
                }

                positions.Add(position);
            }

            NaturalPositions = positions.AsReadOnly();
        }
    }
}
