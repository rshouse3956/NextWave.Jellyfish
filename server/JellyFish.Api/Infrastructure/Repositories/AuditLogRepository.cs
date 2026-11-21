using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JellyFish.Api.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly JellyfishDbContext _db;

    public AuditLogRepository(JellyfishDbContext db)
    {
        _db = db;
    }

    public async Task<Result> AddAsync(AuditLog auditLog, CancellationToken ct = default)
    {
        await _db.AuditLogs.AddAsync(auditLog, ct);
        return Result.Success();
    }

    public async Task<Result<AuditLog>> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var entity = await _db.AuditLogs.FirstOrDefaultAsync(a => a.Id == id, ct);
        return entity is null
            ? Result.Failure<AuditLog>(Error.NotFound($"AuditLog '{id}' not found"))
            : Result.Success(entity);
    }

    public async Task<Result<List<AuditLog>>> GetAllAsync(int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var list = await _db.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        return Result.Success(list);
    }

    public async Task<Result<List<AuditLog>>> GetByActionTypeAsync(string actionType, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var list = await _db.AuditLogs
            .Where(a => a.ActionType == actionType)
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        return Result.Success(list);
    }

    public async Task<Result<List<AuditLog>>> GetByDateRangeAsync(DateTime startUtc, DateTime endUtc, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var list = await _db.AuditLogs
            .Where(a => a.Timestamp >= startUtc && a.Timestamp <= endUtc)
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        return Result.Success(list);
    }

    public async Task<Result<List<AuditLog>>> GetBySourceAsync(string sourceType, string? sourceId = null, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var query = _db.AuditLogs.Where(a => a.SourceType == sourceType);
        
        if (!string.IsNullOrWhiteSpace(sourceId))
        {
            query = query.Where(a => a.SourceId == sourceId);
        }

        var list = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        return Result.Success(list);
    }

    public async Task<Result<int>> GetTotalCountAsync(CancellationToken ct = default)
    {
        var count = await _db.AuditLogs.CountAsync(ct);
        return Result.Success(count);
    }
}

