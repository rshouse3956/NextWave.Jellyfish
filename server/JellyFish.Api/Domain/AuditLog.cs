using JellyFish.Api.Common;

namespace JellyFish.Api.Domain;

public sealed class AuditLog : BaseEntity
{
    private AuditLog() { }

    public DateTime Timestamp { get; private set; }
    public string ActionType { get; private set; } = string.Empty; // RunPattern, SyncFolders, SyncZones, SyncPatterns, SyncPatternDetail, GeneratePattern, HealthCheck
    public string SourceType { get; private set; } = string.Empty; // Manual, Schedule, System
    public string SourceId { get; private set; } = string.Empty; // ScheduleName or user identifier
    public string FolderName { get; private set; } = string.Empty;
    public string PatternName { get; private set; } = string.Empty;
    public List<string> ZoneNames { get; private set; } = new();
    public int? State { get; private set; } // 0=Off, 1=On (for RunPattern actions)
    public bool Success { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;
    public string RequestPayload { get; private set; } = string.Empty; // JSON
    public string ResponsePayload { get; private set; } = string.Empty; // JSON
    public long DurationMs { get; private set; }

    public static Result<AuditLog> Create(
        DateTime timestamp,
        string actionType,
        string sourceType,
        string sourceId,
        string folderName,
        string patternName,
        IReadOnlyList<string>? zoneNames,
        int? state,
        bool success,
        string errorMessage,
        string requestPayload,
        string responsePayload,
        long durationMs)
    {
        if (string.IsNullOrWhiteSpace(actionType))
            return Result.Failure<AuditLog>(Error.Validation("ActionType is required"));
        if (string.IsNullOrWhiteSpace(sourceType))
            return Result.Failure<AuditLog>(Error.Validation("SourceType is required"));

        var log = new AuditLog
        {
            Timestamp = timestamp,
            ActionType = actionType.Trim(),
            SourceType = sourceType.Trim(),
            SourceId = sourceId?.Trim() ?? string.Empty,
            FolderName = folderName?.Trim() ?? string.Empty,
            PatternName = patternName?.Trim() ?? string.Empty,
            ZoneNames = zoneNames?.ToList() ?? new List<string>(),
            State = state,
            Success = success,
            ErrorMessage = errorMessage?.Trim() ?? string.Empty,
            RequestPayload = requestPayload?.Trim() ?? string.Empty,
            ResponsePayload = responsePayload?.Trim() ?? string.Empty,
            DurationMs = durationMs
        };

        return Result.Success(log);
    }
}

