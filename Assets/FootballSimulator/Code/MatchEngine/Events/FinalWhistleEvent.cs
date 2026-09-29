using FStudio.Database;
using FStudio.Events;
using Shared.Responses;

namespace FStudio.MatchEngine.Events {
    public class FinalWhistleEvent : IBaseEvent {
        public readonly TeamEntry HomeTeam, AwayTeam;
        public readonly int HomeGoals, AwayGoals;

        public FinalWhistleEvent (TeamEntry homeTeam, TeamEntry awayTeam, int homeGoals, int awayGoals) {
            this.HomeTeam = homeTeam;
            this.AwayTeam = awayTeam;
            HomeGoals = homeGoals;
            AwayGoals = awayGoals;
        }
    }
}
