namespace FStudio.FootballWorld.Domain
{
    public sealed class CompetitionDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public CompetitionDefinition(string id, string name)
        {
            Id = DomainValidation.Id(id, nameof(id));
            Name = DomainValidation.Name(name, nameof(name));
        }
    }
}
