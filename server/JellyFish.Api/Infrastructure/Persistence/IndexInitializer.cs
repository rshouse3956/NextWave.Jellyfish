using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace JellyFish.Api.Infrastructure.Persistence;

public sealed class IndexInitializer : IHostedService
{
    private readonly ILogger<IndexInitializer> _logger;
    private readonly MongoSettings _settings;

    public IndexInitializer(ILogger<IndexInitializer> logger, IOptions<MongoSettings> options)
    {
        _logger = logger;
        _settings = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = new MongoClient(_settings.ConnectionString);
            var db = client.GetDatabase(_settings.DatabaseName);

            // Folders: unique index on Name
            var folders = db.GetCollection<dynamic>("folder");
            var folderIndex = new CreateIndexModel<dynamic>(Builders<dynamic>.IndexKeys.Ascending("Name"), new CreateIndexOptions { Unique = true });
            await folders.Indexes.CreateOneAsync(folderIndex, cancellationToken: cancellationToken);

            // Patterns: compound unique index on FolderName + Name
            var patterns = db.GetCollection<dynamic>("pattern");
            var patternIndex = new CreateIndexModel<dynamic>(Builders<dynamic>.IndexKeys.Ascending("FolderName").Ascending("Name"), new CreateIndexOptions { Unique = true });
            await patterns.Indexes.CreateOneAsync(patternIndex, cancellationToken: cancellationToken);

            // PatternDetails: compound unique index on FolderName + Name
            var patternDetails = db.GetCollection<dynamic>("patternDetail");
            var patternDetailIndex = new CreateIndexModel<dynamic>(Builders<dynamic>.IndexKeys.Ascending("FolderName").Ascending("Name"), new CreateIndexOptions { Unique = true });
            await patternDetails.Indexes.CreateOneAsync(patternDetailIndex, cancellationToken: cancellationToken);

            // Holidays: unique index on Name
            var holidays = db.GetCollection<dynamic>("holiday");
            var holidayIndex = new CreateIndexModel<dynamic>(Builders<dynamic>.IndexKeys.Ascending("Name"), new CreateIndexOptions { Unique = true });
            await holidays.Indexes.CreateOneAsync(holidayIndex, cancellationToken: cancellationToken);

            // Zones: unique index on Name
            var zones = db.GetCollection<dynamic>("zone");
            var zoneIndex = new CreateIndexModel<dynamic>(Builders<dynamic>.IndexKeys.Ascending("Name"), new CreateIndexOptions { Unique = true });
            await zones.Indexes.CreateOneAsync(zoneIndex, cancellationToken: cancellationToken);

            // Schedules: unique index on Name, and index on NextEvent
            var schedules = db.GetCollection<dynamic>("schedule");
            var scheduleNameIndex = new CreateIndexModel<dynamic>(Builders<dynamic>.IndexKeys.Ascending("Name"), new CreateIndexOptions { Unique = true });
            await schedules.Indexes.CreateOneAsync(scheduleNameIndex, cancellationToken: cancellationToken);
            var scheduleNextEventIndex = new CreateIndexModel<dynamic>(Builders<dynamic>.IndexKeys.Ascending("NextEvent"));
            await schedules.Indexes.CreateOneAsync(scheduleNextEventIndex, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Index initialization failed (continuing startup)");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

