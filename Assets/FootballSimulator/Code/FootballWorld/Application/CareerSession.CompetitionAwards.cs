using System;
using System.Linq;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public sealed partial class CareerSession
    {
        private CompetitionPrizeDefinition CompetitionPrizes => !Competition.Edition.IsDeclarative ? null
            : Competition.Catalog.Competitions.First(value => value.Id == Competition.Edition.CompetitionId).Prizes;

        private void InitializeCompetitionAwards(GameDate date)
        {
            var prizes = CompetitionPrizes;
            if (prizes == null) return;
            if (prizes.Currency != Currency && (prizes.Participation > 0 || prizes.Win > 0 || prizes.Draw > 0 || prizes.RankingAwards.Any(value => value.Amount > 0)))
                throw new ArgumentException("Competition awards must use the club's career currency; currency conversion is not supported.");
            AddCompetitionAward("prize-participation", date, "prize-participation", prizes.Participation);
        }

        private void ApplyMatchAward(FixtureDefinition fixture, FixtureResult result)
        {
            var prizes = CompetitionPrizes;
            if (prizes == null) return;
            // Match awards use the full-time score, not aggregate/tie qualification.
            var draw = result.HomeGoals == result.AwayGoals;
            var won = fixture.HomeClubId == Competition.ControlledClubId
                ? result.HomeGoals > result.AwayGoals : result.AwayGoals > result.HomeGoals;
            var amount = draw ? prizes.Draw : won ? prizes.Win : 0;
            AddCompetitionAward(Key("prize-match", fixture.Id), fixture.Date, draw ? "prize-draw" : "prize-win", amount, fixture.Id);
        }

        private void ApplyStageAwardsThrough(GameDate date)
        {
            var prizes = CompetitionPrizes;
            if (prizes == null) return;
            for (var index = 0; index < prizes.RankingAwards.Count; index++)
            {
                var award = prizes.RankingAwards[index];
                if (award.Amount == 0 || !Competition.IsStageComplete(award.StageId)) continue;
                var stageFixtures = Competition.Fixtures.Where(value => value.StageId == award.StageId).ToArray();
                // Restore sees the final validated sporting snapshot while replaying days.
                // A future completed stage must never pay an earlier replay date.
                if (stageFixtures.Length == 0 || stageFixtures.Any(value => value.Date.CompareTo(date) > 0)) continue;
                var awardDate = stageFixtures.Max(value => value.Date);
                var tables = award.Ranking == "per-group" ? Competition.GetStageGroupIds(award.StageId).ToArray() : new string[] { null };
                foreach (var group in tables)
                {
                    var row = Competition.GetStageStandings(award.StageId, group).FirstOrDefault(value => value.ClubId == Competition.ControlledClubId);
                    if (row == null || row.Rank < award.FromRank || row.Rank > award.ToRank) continue;
                    AddCompetitionAward(Key("prize-rank", award.StageId + "|" + index + "|" + group), awardDate, "prize-ranking", award.Amount);
                }
            }
        }

        private void AddCompetitionAward(string id, GameDate date, string eventKey, long amount, string fixtureId = null)
        {
            if (amount <= 0 || ledger.Any(value => value.Id == id)) return;
            ledger.Add(new CareerLedgerEntry(id, date, eventKey, amount, fixtureId));
            news.Add(new CareerNewsItem(id, date, "diario-da-arquibancada", "competition-prize", amount: amount));
        }
    }
}
