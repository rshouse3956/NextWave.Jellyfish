namespace JellyFish.Api.Infrastructure.Persistence;

public sealed class MongoSettings
{
    public const string SectionName = "Mongo";
    public const string DefaultDatabaseName = "jellyfish";

    private readonly string _databaseName = DefaultDatabaseName;

    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// The database to use; blank falls back to <see cref="DefaultDatabaseName"/>, since an unset
    /// compose variable (Mongo__DatabaseName=${DB_NAME}) arrives as an empty string.
    /// </summary>
    public string DatabaseName
    {
        get => _databaseName;
        init => _databaseName = string.IsNullOrWhiteSpace(value) ? DefaultDatabaseName : value.Trim();
    }

    public bool DisableTransactions { get; init; } = true; // For standalone Mongo, disable EF's auto-transactions
}
