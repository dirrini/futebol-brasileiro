namespace FStudio.FootballWorld.Domain
{
    public sealed class ClubDefinition
    {
        public string Id { get; }
        public string Name { get; }

        public ClubDefinition(string id, string name)
        {
            Id = DomainValidation.Id(id, nameof(id));
            Name = DomainValidation.Name(name, nameof(name));
        }
    }
}
