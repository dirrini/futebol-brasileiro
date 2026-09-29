namespace FStudio.FootballWorld.Domain
{
    public sealed class CountryDefinition
    {
        public string Code { get; }
        public string Name { get; }
        public CountryDefinition(string code, string name)
        { Code = DomainValidation.Code(code, 2, nameof(code)); Name = DomainValidation.Name(name, nameof(name)); }
    }
}
