using System;
using System.Collections.Generic;
using System.Linq;

namespace FStudio.FootballWorld.Domain
{
    public sealed partial class DatabaseCatalog
    {
        public CompetitionFormatDefinition GetCompetitionFormat(string formatId)
            => CompetitionFormats.SingleOrDefault(value => value.Id == formatId) ?? throw new KeyNotFoundException("Unknown competition format.");

        public CompetitionDefinition GetCompetition(string competitionId)
            => Competitions.SingleOrDefault(value => value.Id == competitionId) ?? throw new KeyNotFoundException("Unknown competition.");

        private void ValidateCompetitionDefinitions(HashSet<string> countries, HashSet<string> competitionIds)
        {
            foreach (var competition in Competitions)
            {
                var applicableFormats = new List<CompetitionFormatDefinition>();
                if (competition.DefaultFormatId != null)
                {
                    var format = CompetitionFormats.SingleOrDefault(value => value.Id == competition.DefaultFormatId);
                    if (format == null) throw new ArgumentException("Competition references an unknown default format.");
                    applicableFormats.Add(format);
                }
                foreach (var code in competition.Eligibility.CountryCodes)
                    if (!countries.Contains(code)) throw new ArgumentException("Competition eligibility references an unknown country.");
                foreach (var clubId in competition.Eligibility.AllowedClubIds.Concat(competition.Eligibility.ExcludedClubIds))
                    if (!clubsById.ContainsKey(clubId)) throw new ArgumentException("Competition eligibility references an unknown club.");
                foreach (var edition in CompetitionEditions.Where(value => value.CompetitionId == competition.Id))
                {
                    foreach (var clubId in edition.ParticipantClubIds)
                        if (!competition.Eligibility.Allows(clubsById[clubId])) throw new ArgumentException("Edition contains a club outside its competition eligibility.");
                    if (!edition.IsDeclarative) continue;
                    var registered = CompetitionFormats.SingleOrDefault(value => value.Id == edition.Format.Id);
                    if (registered == null || !ReferenceEquals(registered, edition.Format))
                        throw new ArgumentException("Declarative editions must use their catalog's registered format definition.");
                    applicableFormats.Add(registered);
                    foreach (var schedule in edition.StageSchedules)
                    {
                        if (schedule.NeutralStadiumId != null && !stadiumsById.ContainsKey(schedule.NeutralStadiumId))
                            throw new ArgumentException("A neutral schedule references an unknown stadium.");
                        foreach (var fixture in schedule.AuthoredFixtures)
                            if (fixture.StadiumId != null && !stadiumsById.ContainsKey(fixture.StadiumId))
                                throw new ArgumentException("An authored fixture references an unknown stadium.");
                    }
                }
                foreach (var route in competition.QualificationRoutes)
                {
                    if (route.TargetCompetitionId == competition.Id || !competitionIds.Contains(route.TargetCompetitionId) || applicableFormats.Count == 0 ||
                        applicableFormats.Any(format => !format.Outcomes.Any(outcome => outcome.Id == route.OutcomeId)))
                        throw new ArgumentException("Qualification routes must resolve outcomes in every applicable format and an existing target competition.");
                }
                if (competition.Prizes == null) continue;
                foreach (var award in competition.Prizes.RankingAwards)
                {
                    if (applicableFormats.Count == 0) throw new ArgumentException("Stage ranking prizes require an applicable format.");
                    foreach (var format in applicableFormats)
                    {
                        var stage = format.Stages.SingleOrDefault(value => value.Id == award.StageId);
                        if (stage == null || (award.Ranking == "per-group" && stage.Kind != "league") ||
                            award.ToRank > format.GetStageParticipantCount(stage.Id) / (award.Ranking == "per-group" ? stage.GroupCount : 1))
                            throw new ArgumentException("Ranking prize stage or rank does not exist in an applicable format.");
                    }
                }
            }
        }
    }
}
