using System.Collections.Generic;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Presentation
{
    public sealed class GameHubCareerSquad : GameHubCareerPlayers
    {
        protected override IEnumerable<PlayerDefinition> SourcePlayers(CareerSession career) => career.GetRoster();
    }
}
