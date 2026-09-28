using System.Collections.Generic;
using System.Collections.ObjectModel;
using FStudio.FootballWorld.DataContracts;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Infrastructure.Importing
{
    public sealed class DatabaseImportError
    {
        public string Code { get; }
        public string Path { get; }
        public string Message { get; }

        public DatabaseImportError(string code, string path, string message)
        {
            Code = code; Path = path; Message = message;
        }
    }

    public sealed class DatabaseImportResult
    {
        public bool Success => Catalog != null;
        public DatabaseCatalog Catalog { get; }
        public IReadOnlyList<VisualProfileData> VisualProfiles { get; }
        public IReadOnlyList<DatabaseImportError> Errors { get; }

        private DatabaseImportResult(DatabaseCatalog catalog, IEnumerable<VisualProfileData> visuals,
            IEnumerable<DatabaseImportError> errors)
        {
            Catalog = catalog;
            VisualProfiles = new ReadOnlyCollection<VisualProfileData>(new List<VisualProfileData>(visuals));
            Errors = new ReadOnlyCollection<DatabaseImportError>(new List<DatabaseImportError>(errors));
        }

        internal static DatabaseImportResult Accepted(DatabaseCatalog catalog, IEnumerable<VisualProfileData> visuals)
            => new DatabaseImportResult(catalog, visuals, new DatabaseImportError[0]);

        internal static DatabaseImportResult Rejected(IEnumerable<DatabaseImportError> errors)
            => new DatabaseImportResult(null, new VisualProfileData[0], errors);
    }
}
