#nullable enable

using System;
using System.Collections.Generic;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public sealed class LineupPlan
    {
        public bool Success { get; }
        public IReadOnlyList<PlayerDefinition> Players { get; }
        public IReadOnlyList<string> OutOfPositionPlayerIds { get; }
        public string? Error { get; }

        private LineupPlan(bool success, IEnumerable<PlayerDefinition> players,
            IEnumerable<string> outOfPositionPlayerIds, string? error)
        {
            Success = success;
            Players = new List<PlayerDefinition>(players).AsReadOnly();
            OutOfPositionPlayerIds = new List<string>(outOfPositionPlayerIds).AsReadOnly();
            Error = error;
        }

        internal static LineupPlan Accepted(IEnumerable<PlayerDefinition> players,
            IEnumerable<string> outOfPositionPlayerIds)
        {
            return new LineupPlan(true, players, outOfPositionPlayerIds, null);
        }

        internal static LineupPlan Rejected(string error)
        {
            return new LineupPlan(false, Array.Empty<PlayerDefinition>(), Array.Empty<string>(), error);
        }
    }
}
