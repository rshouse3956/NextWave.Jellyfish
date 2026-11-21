using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.AuditLog.Queries;

public sealed record GetAuditLogQuery(string Id) : IRequest<Result<AuditLogDto>>;

