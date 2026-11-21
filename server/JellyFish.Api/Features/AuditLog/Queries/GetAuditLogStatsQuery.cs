using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.AuditLog.Queries;

public sealed record GetAuditLogStatsQuery(int? TimezoneOffsetMinutes = null) : IRequest<Result<AuditLogStatsDto>>;

