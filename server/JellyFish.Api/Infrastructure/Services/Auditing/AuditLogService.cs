using System.Text.Json;
using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Infrastructure.Repositories;

namespace JellyFish.Api.Infrastructure.Services.Auditing;

public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogs;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(IAuditLogRepository auditLogs, ILogger<AuditLogService> logger)
    {
        _auditLogs = auditLogs;
        _logger = logger;
    }

    public async Task<Result> LogActionAsync(
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
        CancellationToken ct = default)
    {
        try
        {
            var requestJson = requestPayload is not null
                ? JsonSerializer.Serialize(requestPayload)
                : string.Empty;

            var responseJson = responsePayload is not null
                ? JsonSerializer.Serialize(responsePayload)
                : string.Empty;

            var auditLogResult = AuditLog.Create(
                timestamp: DateTime.UtcNow,
                actionType: actionType,
                sourceType: sourceType,
                sourceId: sourceId ?? string.Empty,
                folderName: folderName ?? string.Empty,
                patternName: patternName ?? string.Empty,
                zoneNames: zoneNames,
                state: state,
                success: success,
                errorMessage: errorMessage ?? string.Empty,
                requestPayload: requestJson,
                responsePayload: responseJson,
                durationMs: durationMs);

            if (auditLogResult.IsFailure)
            {
                _logger.LogWarning("Failed to create audit log: {Error}", auditLogResult.Error);
                return Result.Failure(auditLogResult.Error);
            }

            var addResult = await _auditLogs.AddAsync(auditLogResult.Value, ct);
            if (addResult.IsFailure)
            {
                _logger.LogWarning("Failed to persist audit log: {Error}", addResult.Error);
                return addResult;
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while logging audit action {ActionType}", actionType);
            // Don't fail the operation if audit logging fails
            return Result.Success();
        }
    }
}

