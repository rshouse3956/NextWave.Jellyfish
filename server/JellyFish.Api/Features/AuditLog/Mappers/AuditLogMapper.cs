using AuditLogEntity = JellyFish.Api.Domain.AuditLog;

namespace JellyFish.Api.Features.AuditLog.Mappers;

public static class AuditLogMapper
{
    public static AuditLogDto ToDto(AuditLogEntity entity)
    {
        return new AuditLogDto(
            Id: entity.Id,
            Timestamp: entity.Timestamp,
            ActionType: entity.ActionType,
            SourceType: entity.SourceType,
            SourceId: entity.SourceId,
            FolderName: entity.FolderName,
            PatternName: entity.PatternName,
            ZoneNames: entity.ZoneNames,
            State: entity.State,
            Success: entity.Success,
            ErrorMessage: entity.ErrorMessage,
            RequestPayload: entity.RequestPayload,
            ResponsePayload: entity.ResponsePayload,
            DurationMs: entity.DurationMs
        );
    }
}

