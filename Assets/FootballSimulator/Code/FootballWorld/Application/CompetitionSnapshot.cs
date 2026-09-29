using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public enum FixtureCompletion { Applied, AlreadyApplied, IgnoredStale }

    public sealed class FixtureExecution
    {
        public string FixtureId { get; }
        public string ExecutionId { get; }
        public FixtureExecution(string fixtureId, string executionId)
        {
            FixtureId = CompetitionIdentity.Validate(fixtureId);
            ExecutionId = CompetitionIdentity.Validate(executionId);
        }
    }

    // Application snapshot, not a serialization format. Storage adapters parse and
    // validate their DTOs before constructing this value and calling Restore.
    public sealed class CompetitionSnapshot
    {
        public string SeasonId { get; }
        public string DatabaseId { get; }
        public int DatabaseRevision { get; }
        public string EditionId { get; }
        public string ControlledClubId { get; }
        public IReadOnlyList<FixtureDefinition> Fixtures { get; }
        public IReadOnlyList<FixtureResult> Results { get; }
        public IReadOnlyList<string> UsedExecutionIds { get; }
        public CompetitionSnapshot(string seasonId, string databaseId, int databaseRevision, string editionId,
            string controlledClubId, IEnumerable<FixtureDefinition> fixtures, IEnumerable<FixtureResult> results,
            IEnumerable<string> usedExecutionIds)
        {
            SeasonId = CompetitionIdentity.Validate(seasonId);
            DatabaseId = CompetitionIdentity.Validate(databaseId);
            DatabaseRevision = databaseRevision;
            EditionId = CompetitionIdentity.Validate(editionId);
            ControlledClubId = CompetitionIdentity.Validate(controlledClubId);
            Fixtures = new List<FixtureDefinition>(fixtures ?? throw new ArgumentNullException(nameof(fixtures))).AsReadOnly();
            Results = new List<FixtureResult>(results ?? throw new ArgumentNullException(nameof(results))).AsReadOnly();
            UsedExecutionIds = new List<string>(usedExecutionIds ?? throw new ArgumentNullException(nameof(usedExecutionIds))).AsReadOnly();
        }
    }

    internal static class CompetitionIdentity
    {
        internal static string Validate(string value)
        {
            if (value == null || !Regex.IsMatch(value, @"\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant))
                throw new ArgumentException("Expected a stable ID containing 1-64 supported ASCII characters.", nameof(value));
            return value;
        }
    }
}
