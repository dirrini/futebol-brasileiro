namespace FStudio.FootballWorld.Domain
{
    // Descriptive authored venue data; selecting a Unity stadium remains a visual adapter concern.
    public sealed class StadiumDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string CountryCode { get; }
        public string City { get; }
        public int? Capacity { get; }
        public StadiumDefinition(string id, string name, string countryCode, string city, int? capacity = null)
        {
            Id = DomainValidation.Id(id, nameof(id)); Name = DomainValidation.Name(name, nameof(name));
            CountryCode = DomainValidation.Code(countryCode, 2, nameof(countryCode));
            City = DomainValidation.Name(city, nameof(city));
            Capacity = capacity.HasValue ? DomainValidation.InRange(capacity.Value, 1, 1000000, nameof(capacity)) : (int?)null;
        }
    }
}
