using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.AuditLog.Queries;

public sealed record ListAuditLogsQuery(
    string? ActionType = null,
    string? SourceType = null,
    DateTime? StartDateUtc = null,
    DateTime? EndDateUtc = null,
    int Skip = 0,
    int Take = 50
) : IRequest<Result<List<AuditLogDto>>>;

