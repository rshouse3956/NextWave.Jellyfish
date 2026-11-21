namespace JellyFish.Api.Features.AuditLog;

public sealed record AuditLogDto(
    string Id,
    DateTime Timestamp,
    string ActionType,
    string SourceType,
    string SourceId,
    string FolderName,
    string PatternName,
    List<string> ZoneNames,
    int? State,
    bool Success,
    string ErrorMessage,
    string RequestPayload,
    string ResponsePayload,
    long DurationMs
);

public sealed record AuditLogStatsDto(
    int TotalCount,
    int TodayCount,
    double SuccessRate,
    Dictionary<string, int> ActionTypeCounts,
    List<AuditLogDto> RecentFailures
);

