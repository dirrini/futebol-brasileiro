using System;

namespace FStudio.FootballWorld.Domain
{
    public sealed class FixtureDefinition
    {
        public string Id { get; }
        public int Round { get; }
        public GameDate Date { get; }
        public string HomeClubId { get; }
        public string AwayClubId { get; }
        public string StadiumId { get; }
        public FixtureDefinition(string id, int round, GameDate date, string homeClubId, string awayClubId, string stadiumId = null)
        {
            Id = DomainValidation.Id(id, nameof(id));
            Round = DomainValidation.InRange(round, 1, 128, nameof(round));
            Date = date;
            HomeClubId = DomainValidation.Id(homeClubId, nameof(homeClubId));
            AwayClubId = DomainValidation.Id(awayClubId, nameof(awayClubId));
            StadiumId = stadiumId == null ? null : DomainValidation.Id(stadiumId, nameof(stadiumId));
            if (HomeClubId == AwayClubId) throw new ArgumentException("A fixture needs two distinct clubs.");
        }
        public bool IncludesClub(string clubId) => HomeClubId == clubId || AwayClubId == clubId;
    }
}
