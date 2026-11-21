using JellyFish.Api.Common;
using JellyFish.Api.Features.AuditLog.Mappers;
using JellyFish.Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JellyFish.Api.Features.AuditLog.Queries;

public sealed class GetAuditLogStatsQueryHandler : IRequestHandler<GetAuditLogStatsQuery, Result<AuditLogStatsDto>>
{
    private readonly JellyfishDbContext _db;

    public GetAuditLogStatsQueryHandler(JellyfishDbContext db)
    {
        _db = db;
    }

    public async Task<Result<AuditLogStatsDto>> Handle(GetAuditLogStatsQuery request, CancellationToken cancellationToken)
    {
        var totalCount = await _db.AuditLogs.CountAsync(cancellationToken);

        // Compute user's local "today" start in UTC using optional timezone offset from client.
        // JS getTimezoneOffset() returns minutes to add to local time to get UTC (positive in US timezones).
        // We convert using: localNow = utcNow - offset; startUtc = localMidnight + offset.
        var utcNow = DateTime.UtcNow;
        DateTime todayStartUtc;
        if (request.TimezoneOffsetMinutes.HasValue)
        {
            var offset = request.TimezoneOffsetMinutes.Value;
            var localNow = utcNow.AddMinutes(-offset);
            var localMidnight = localNow.Date;
            todayStartUtc = localMidnight.AddMinutes(offset);
        }
        else
        {
            // Fallback to server-local day start converted to UTC (previous behavior)
            todayStartUtc = DateTime.Now.Date.ToUniversalTime();
        }

        var todayCount = await _db.AuditLogs
            .Where(a => a.Timestamp >= todayStartUtc && a.Timestamp <= utcNow)
            .CountAsync(cancellationToken);

        var successCount = await _db.AuditLogs.CountAsync(a => a.Success, cancellationToken);
        var successRate = totalCount > 0 ? (double)successCount / totalCount * 100.0 : 0.0;

        // MongoDB EF Core doesn't support GroupBy with ToDictionary, so we'll do it in memory
        var allLogs = await _db.AuditLogs.ToListAsync(cancellationToken);
        var actionTypeCounts = allLogs
            .GroupBy(a => a.ActionType)
            .ToDictionary(g => g.Key, g => g.Count());

        var recentFailures = await _db.AuditLogs
            .Where(a => !a.Success)
            .OrderByDescending(a => a.Timestamp)
            .Take(10)
            .ToListAsync(cancellationToken);

        var recentFailureDtos = recentFailures.Select(AuditLogMapper.ToDto).ToList();

        var stats = new AuditLogStatsDto(
            TotalCount: totalCount,
            TodayCount: todayCount,
            SuccessRate: successRate,
            ActionTypeCounts: actionTypeCounts,
            RecentFailures: recentFailureDtos
        );

        return Result.Success(stats);
    }
}

