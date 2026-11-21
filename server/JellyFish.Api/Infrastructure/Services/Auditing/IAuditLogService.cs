using JellyFish.Api.Common;

namespace JellyFish.Api.Infrastructure.Services.Auditing;

public interface IAuditLogService
{
    Task<Result> LogActionAsync(
        string actionType,
        string sourceType,
        string sourceId,
        string folderName,
        string patternName,
        IReadOnlyList<string>? zoneNames,
        int? state,
        bool success,
        string errorMessage,
        object? requestPayload,
        object? responsePayload,
        long durationMs,
        CancellationToken ct = default);
}

