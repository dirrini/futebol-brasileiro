using System.Collections.Generic;

namespace FStudio.FootballWorld.DataContracts
{
    public sealed class CountryData
    {
        public string Code { get; }
        public string Name { get; }
        public CountryData(string code, string name) { Code = code; Name = name; }
    }
    public sealed class StadiumData
    {
        public string Id { get; }
        public string Name { get; }
        public string CountryCode { get; }
        public string City { get; }
        public int? Capacity { get; }
        public StadiumData(string id, string name, string countryCode, string city, int? capacity)
        { Id = id; Name = name; CountryCode = countryCode; City = city; Capacity = capacity; }
    }
    public sealed class DatabaseSourceData
    {
        public string Id { get; }
        public string Title { get; }
        public string Url { get; }
        public DatabaseSourceData(string id, string title, string url) { Id = id; Title = title; Url = url; }
    }
    public sealed class DatabaseSnapshotData
    {
        public string Date { get; }
        public string Label { get; }
        public string RosterScope { get; }
        public string Notes { get; }
        public IReadOnlyList<DatabaseSourceData> Sources { get; }
        public DatabaseSnapshotData(string date, string label, string rosterScope, string notes, IEnumerable<DatabaseSourceData> sources)
        { Date = date; Label = label; RosterScope = rosterScope; Notes = notes; Sources = DataSnapshot.Copy(sources); }
    }
}
