using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FStudio.FootballWorld.DataContracts
{
    // Portable data only. Parsing, validation and domain mapping belong to adapters.
    public sealed class DatabaseDocument
    {
        public int SchemaVersion { get; }
        public string DatabaseId { get; }
        public int DatabaseRevision { get; }
        public IReadOnlyList<ClubData> Clubs { get; }
        public IReadOnlyList<PlayerData> Players { get; }
        public IReadOnlyList<MembershipData> Memberships { get; }
        public IReadOnlyList<VisualProfileData> VisualProfiles { get; }
        public IReadOnlyList<CompetitionData> Competitions { get; }
        public IReadOnlyList<CompetitionEditionData> CompetitionEditions { get; }
        public IReadOnlyList<CountryData> Countries { get; }
        public IReadOnlyList<StadiumData> Stadiums { get; }
        public DatabaseSnapshotData Snapshot { get; }

        public DatabaseDocument(int schemaVersion, string databaseId, int databaseRevision,
            IEnumerable<ClubData> clubs, IEnumerable<PlayerData> players,
            IEnumerable<MembershipData> memberships, IEnumerable<VisualProfileData> visualProfiles,
            IEnumerable<CompetitionData> competitions = null, IEnumerable<CompetitionEditionData> competitionEditions = null,
            IEnumerable<CountryData> countries = null, IEnumerable<StadiumData> stadiums = null, DatabaseSnapshotData snapshot = null)
        {
            SchemaVersion = schemaVersion;
            DatabaseId = databaseId;
            DatabaseRevision = databaseRevision;
            Clubs = DataSnapshot.Copy(clubs);
            Players = DataSnapshot.Copy(players);
            Memberships = DataSnapshot.Copy(memberships);
            VisualProfiles = DataSnapshot.Copy(visualProfiles);
            Competitions = DataSnapshot.Copy(competitions ?? Array.Empty<CompetitionData>());
            CompetitionEditions = DataSnapshot.Copy(competitionEditions ?? Array.Empty<CompetitionEditionData>());
            Countries = DataSnapshot.Copy(countries ?? Array.Empty<CountryData>());
            Stadiums = DataSnapshot.Copy(stadiums ?? Array.Empty<StadiumData>()); Snapshot = snapshot;
        }
    }

    public sealed class ClubData
    {
        public string Id { get; }
        public string Name { get; }
        public string CountryCode { get; }
        public string City { get; }
        public string OfficialName { get; }
        public string ShortName { get; }
        public string StadiumId { get; }
        public int? Reputation { get; }
        public int? SupporterCount { get; }
        public int? TransferBudget { get; }
        public int? MonthlyWageBudget { get; }
        public string Currency { get; }
        public string Sponsorship { get; }
        public string Notes { get; }
        public ClubData(string id, string name, string countryCode = null, string city = null, string officialName = null,
            string shortName = null, string stadiumId = null, int? reputation = null, int? supporterCount = null,
            int? transferBudget = null, int? monthlyWageBudget = null, string currency = null, string sponsorship = null, string notes = null)
        {
            Id = id; Name = name; CountryCode = countryCode; City = city; OfficialName = officialName; ShortName = shortName;
            StadiumId = stadiumId; Reputation = reputation; SupporterCount = supporterCount; TransferBudget = transferBudget;
            MonthlyWageBudget = monthlyWageBudget; Currency = currency; Sponsorship = sponsorship; Notes = notes;
        }
    }

    public sealed class PlayerData
    {
        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<string> NaturalPositions { get; }
        public int HeightCm { get; }
        public int WeightKg { get; }
        public PlayerAttributesData Attributes { get; }
        public string FullName { get; }
        public string Nickname { get; }
        public string BirthDate { get; }
        public string PreferredFoot { get; }
        public string NationalityCode { get; }
        public string Notes { get; }

        public PlayerData(string id, string name, IEnumerable<string> naturalPositions,
            int heightCm, int weightKg, PlayerAttributesData attributes, string fullName = null,
            string birthDate = null, string preferredFoot = null, string nationalityCode = null, string notes = null, string nickname = null)
        {
            Id = id;
            Name = name;
            NaturalPositions = DataSnapshot.Copy(naturalPositions);
            HeightCm = heightCm;
            WeightKg = weightKg;
            Attributes = attributes;
            FullName = fullName; BirthDate = birthDate; PreferredFoot = preferredFoot; NationalityCode = nationalityCode; Notes = notes; Nickname = nickname;
        }
    }

    public sealed class PlayerAttributesData
    {
        public int Strength { get; }
        public int Acceleration { get; }
        public int TopSpeed { get; }
        public int DribbleSpeed { get; }
        public int Jump { get; }
        public int Tackling { get; }
        public int BallKeeping { get; }
        public int Passing { get; }
        public int LongBall { get; }
        public int Agility { get; }
        public int Shooting { get; }
        public int ShootPower { get; }
        public int Positioning { get; }
        public int Reaction { get; }
        public int BallControl { get; }

        public PlayerAttributesData(int strength, int acceleration, int topSpeed, int dribbleSpeed,
            int jump, int tackling, int ballKeeping, int passing, int longBall, int agility,
            int shooting, int shootPower, int positioning, int reaction, int ballControl)
        {
            Strength = strength; Acceleration = acceleration; TopSpeed = topSpeed;
            DribbleSpeed = dribbleSpeed; Jump = jump; Tackling = tackling; BallKeeping = ballKeeping;
            Passing = passing; LongBall = longBall; Agility = agility; Shooting = shooting;
            ShootPower = shootPower; Positioning = positioning; Reaction = reaction; BallControl = ballControl;
        }
    }

    public sealed class MembershipData
    {
        public string ClubId { get; }
        public string PlayerId { get; }
        public MembershipData(string clubId, string playerId) { ClubId = clubId; PlayerId = playerId; }
    }

    public sealed class VisualProfileData
    {
        public string PlayerId { get; }
        public SkinReferenceData Skin { get; }
        public BuiltinAppearanceData Appearance { get; }

        public VisualProfileData(string playerId, SkinReferenceData skin, BuiltinAppearanceData appearance = null)
        {
            PlayerId = playerId;
            Skin = skin;
            Appearance = appearance;
        }
    }

    public sealed class SkinReferenceData
    {
        public string SkinId { get; }
        public int Revision { get; }
        public string CompatibilityProfile { get; }
        public SkinReferenceData(string skinId, int revision, string compatibilityProfile)
        {
            SkinId = skinId; Revision = revision; CompatibilityProfile = compatibilityProfile;
        }
    }

    internal static class DataSnapshot
    {
        internal static IReadOnlyList<T> Copy<T>(IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new ReadOnlyCollection<T>(new List<T>(source));
        }
    }
}
