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
        public string StageId { get; }
        public string TieId { get; }
        public int Leg { get; }
        public bool IsNeutral { get; }
        public FixtureDefinition(string id, int round, GameDate date, string homeClubId, string awayClubId, string stadiumId = null,
            string stageId = null, string tieId = null, int leg = 1, bool isNeutral = false)
        {
            Id = DomainValidation.Id(id, nameof(id));
            Round = DomainValidation.InRange(round, 1, 2048, nameof(round));
            Date = date;
            HomeClubId = DomainValidation.Id(homeClubId, nameof(homeClubId));
            AwayClubId = DomainValidation.Id(awayClubId, nameof(awayClubId));
            StadiumId = stadiumId == null ? null : DomainValidation.Id(stadiumId, nameof(stadiumId));
            StageId = stageId == null ? null : DomainValidation.Id(stageId, nameof(stageId));
            TieId = tieId == null ? null : DomainValidation.Id(tieId, nameof(tieId));
            Leg = DomainValidation.InRange(leg, 1, 2, nameof(leg)); IsNeutral = isNeutral;
            if ((tieId != null || leg != 1 || isNeutral) && stageId == null) throw new ArgumentException("Declarative fixture context requires a stage.");
            if (isNeutral && stadiumId == null) throw new ArgumentException("Neutral fixtures require a named venue.");
            if (HomeClubId == AwayClubId) throw new ArgumentException("A fixture needs two distinct clubs.");
        }
        public bool IncludesClub(string clubId) => HomeClubId == clubId || AwayClubId == clubId;
    }
}
