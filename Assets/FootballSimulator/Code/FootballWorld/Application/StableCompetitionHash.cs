namespace FStudio.FootballWorld.Application
{
    // FNV-1a is portable across runtimes; never use platform-dependent GetHashCode
    // for a season's prototype simulation or its simulated drawing of lots.
    internal static class StableCompetitionHash
    {
        internal static ulong Value(string text)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (var character in text) hash = (hash ^ character) * 1099511628211UL;
                return hash;
            }
        }
    }
}
