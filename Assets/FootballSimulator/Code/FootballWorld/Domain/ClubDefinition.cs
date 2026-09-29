namespace FStudio.FootballWorld.Domain
{
    public sealed class ClubDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string CountryCode { get; }
        public string StateCode { get; }
        public string City { get; }
        public string OfficialName { get; }
        public string ShortName { get; }
        public string StadiumId { get; }
        public int? Reputation { get; }
        public int? SupporterCount { get; }
        public int? TransferBudget { get; }
        public int? MonthlyWageBudget { get; }
        public string Currency { get; }
        public string Sponsorship { get; }
        public string Notes { get; }

        public ClubDefinition(string id, string name, string countryCode = null, string city = null,
            string officialName = null, string shortName = null, string stadiumId = null, int? reputation = null,
            int? supporterCount = null, int? transferBudget = null, int? monthlyWageBudget = null,
            string currency = null, string sponsorship = null, string notes = null, string stateCode = null)
        {
            Id = DomainValidation.Id(id, nameof(id));
            Name = DomainValidation.Name(name, nameof(name));
            if ((countryCode == null) != (city == null)) throw new System.ArgumentException("Country and city must be supplied together.");
            CountryCode = countryCode == null ? null : DomainValidation.Code(countryCode, 2, nameof(countryCode));
            StateCode = stateCode == null ? null : CompetitionRuleValidation.StateCode(stateCode, nameof(stateCode));
            if (stateCode != null && countryCode == null) throw new System.ArgumentException("A state code requires a country.");
            City = city == null ? null : DomainValidation.Name(city, nameof(city));
            OfficialName = officialName == null ? null : DomainValidation.Text(officialName, 200, false, nameof(officialName));
            ShortName = shortName == null ? null : DomainValidation.Name(shortName, nameof(shortName));
            StadiumId = stadiumId == null ? null : DomainValidation.Id(stadiumId, nameof(stadiumId));
            Reputation = reputation.HasValue ? DomainValidation.InRange(reputation.Value, 0, 100, nameof(reputation)) : (int?)null;
            SupporterCount = supporterCount.HasValue ? DomainValidation.InRange(supporterCount.Value, 0, int.MaxValue, nameof(supporterCount)) : (int?)null;
            TransferBudget = transferBudget.HasValue ? DomainValidation.InRange(transferBudget.Value, 0, int.MaxValue, nameof(transferBudget)) : (int?)null;
            MonthlyWageBudget = monthlyWageBudget.HasValue ? DomainValidation.InRange(monthlyWageBudget.Value, 0, int.MaxValue, nameof(monthlyWageBudget)) : (int?)null;
            if ((transferBudget.HasValue || monthlyWageBudget.HasValue) && currency == null)
                throw new System.ArgumentException("Budgets require a currency.", nameof(currency));
            Currency = currency == null ? null : DomainValidation.Code(currency, 3, nameof(currency));
            Sponsorship = sponsorship == null ? null : DomainValidation.Text(sponsorship, 200, false, nameof(sponsorship));
            Notes = notes == null ? null : DomainValidation.Text(notes, 4000, true, nameof(notes));
        }
    }
}
