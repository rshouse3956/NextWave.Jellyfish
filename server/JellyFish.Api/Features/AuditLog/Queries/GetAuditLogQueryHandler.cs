using JellyFish.Api.Common;
using JellyFish.Api.Features.AuditLog.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.AuditLog.Queries;

public sealed class GetAuditLogQueryHandler : IRequestHandler<GetAuditLogQuery, Result<AuditLogDto>>
{
    private readonly IAuditLogRepository _auditLogs;

    public GetAuditLogQueryHandler(IAuditLogRepository auditLogs)
    {
        _auditLogs = auditLogs;
    }

    public async Task<Result<AuditLogDto>> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        var result = await _auditLogs.GetByIdAsync(request.Id, cancellationToken);
        
        if (result.IsFailure)
            return Result.Failure<AuditLogDto>(result.Error);

        return Result.Success(AuditLogMapper.ToDto(result.Value));
    }
}

