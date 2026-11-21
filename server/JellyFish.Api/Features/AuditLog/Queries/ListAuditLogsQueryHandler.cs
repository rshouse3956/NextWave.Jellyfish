using JellyFish.Api.Common;
using JellyFish.Api.Features.AuditLog.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.AuditLog.Queries;

public sealed class ListAuditLogsQueryHandler : IRequestHandler<ListAuditLogsQuery, Result<List<AuditLogDto>>>
{
    private readonly IAuditLogRepository _auditLogs;

    public ListAuditLogsQueryHandler(IAuditLogRepository auditLogs)
    {
        _auditLogs = auditLogs;
    }

    public async Task<Result<List<AuditLogDto>>> Handle(ListAuditLogsQuery request, CancellationToken cancellationToken)
    {
        Result<List<Domain.AuditLog>> result;

        // Apply filters based on query parameters
        if (!string.IsNullOrWhiteSpace(request.ActionType))
        {
            result = await _auditLogs.GetByActionTypeAsync(request.ActionType, request.Skip, request.Take, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.SourceType))
        {
            result = await _auditLogs.GetBySourceAsync(request.SourceType, null, request.Skip, request.Take, cancellationToken);
        }
        else if (request.StartDateUtc.HasValue && request.EndDateUtc.HasValue)
        {
            result = await _auditLogs.GetByDateRangeAsync(request.StartDateUtc.Value, request.EndDateUtc.Value, request.Skip, request.Take, cancellationToken);
        }
        else
        {
            result = await _auditLogs.GetAllAsync(request.Skip, request.Take, cancellationToken);
        }

        if (result.IsFailure)
            return Result.Failure<List<AuditLogDto>>(result.Error);

        var dtos = result.Value.Select(AuditLogMapper.ToDto).ToList();
        return Result.Success(dtos);
    }
}

