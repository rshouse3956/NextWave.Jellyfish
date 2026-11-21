using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Infrastructure.Services.Jellyfish.Models;

namespace JellyFish.Api.Infrastructure.Services.Jellyfish;

public interface IJellyfishControllerClient
{
    Task<Result<IReadOnlyList<ControllerFolderDto>>> GetFoldersAsync(CancellationToken ct);
    Task<Result<IReadOnlyList<ControllerPatternListItemDto>>> GetPatternsAsync(CancellationToken ct);
    Task<Result<PatternDetails>> GetPatternDetailAsync(string folderName, string name, CancellationToken ct);
    Task<Result<IReadOnlyList<ControllerZoneDto>>> GetZonesAsync(CancellationToken ct);
    Task<Result> RunPatternAsync(string folderName, string name, int state, IReadOnlyList<string> zoneNames, PatternDetails? data, CancellationToken ct);
    Task<Result> HealthCheckAsync(CancellationToken ct);
}
