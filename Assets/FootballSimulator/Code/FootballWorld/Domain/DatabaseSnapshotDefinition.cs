using System;
using System.Collections.Generic;

namespace FStudio.FootballWorld.Domain
{
    public sealed class DatabaseSourceDefinition
    {
        public string Id { get; }
        public string Title { get; }
        public string Url { get; }
        public DatabaseSourceDefinition(string id, string title, string url)
        {
            Id = DomainValidation.Id(id, nameof(id));
            Title = DomainValidation.Text(title, 200, false, nameof(title));
            Url = DomainValidation.HttpUrl(url, nameof(url));
        }
    }

    // An observation of authored content on a date, not a temporal roster engine or season state.
    public sealed class DatabaseSnapshotDefinition
    {
        public GameDate Date { get; }
        public string Label { get; }
        public string RosterScope { get; }
        public string Notes { get; }
        public IReadOnlyList<DatabaseSourceDefinition> Sources { get; }
        public DatabaseSnapshotDefinition(GameDate date, string label, string rosterScope, string notes,
            IEnumerable<DatabaseSourceDefinition> sources)
        {
            if (rosterScope != "matchday-squads" && rosterScope != "full-squads")
                throw new ArgumentException("Unsupported roster observation scope.", nameof(rosterScope));
            Date = date; Label = DomainValidation.Name(label, nameof(label)); RosterScope = rosterScope;
            Notes = DomainValidation.Text(notes, 4000, true, nameof(notes));
            var copy = new List<DatabaseSourceDefinition>(sources ?? throw new ArgumentNullException(nameof(sources)));
            DomainValidation.InRange(copy.Count, 1, 128, nameof(sources));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in copy)
                if (source == null || !ids.Add(source.Id)) throw new ArgumentException("Source IDs must be unique and non-null.", nameof(sources));
            Sources = copy.AsReadOnly();
        }
    }
}
