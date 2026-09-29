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
        public string FullName { get; }
        public string Nickname { get; }
        public string DisplayName => Nickname ?? Name;
        public GameDate? BirthDate { get; }
        public string PreferredFoot { get; }
        public string NationalityCode { get; }
        public string Notes { get; }

        public PlayerDefinition(
            string id,
            string name,
            IReadOnlyList<PlayerPosition> naturalPositions,
            int heightCm,
            int weightKg,
            PlayerAttributes attributes,
            string fullName = null, GameDate? birthDate = null, string preferredFoot = null,
            string nationalityCode = null, string notes = null, string nickname = null)
        {
            Id = DomainValidation.Id(id, nameof(id));
            Name = DomainValidation.Name(name, nameof(name));
            HeightCm = DomainValidation.InRange(heightCm, 150, 210, nameof(heightCm));
            WeightKg = DomainValidation.InRange(weightKg, 45, 100, nameof(weightKg));
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            FullName = fullName == null ? null : DomainValidation.Text(fullName, 200, false, nameof(fullName));
            Nickname = nickname == null ? null : DomainValidation.Name(nickname, nameof(nickname));
            BirthDate = birthDate;
            if (preferredFoot != null && preferredFoot != "right" && preferredFoot != "left" && preferredFoot != "both")
                throw new ArgumentException("Unsupported preferred foot.", nameof(preferredFoot));
            PreferredFoot = preferredFoot;
            NationalityCode = nationalityCode == null ? null : DomainValidation.Code(nationalityCode, 2, nameof(nationalityCode));
            Notes = notes == null ? null : DomainValidation.Text(notes, 4000, true, nameof(notes));

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
