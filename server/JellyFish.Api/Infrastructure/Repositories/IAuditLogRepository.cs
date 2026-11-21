using JellyFish.Api.Common;
using JellyFish.Api.Domain;

namespace JellyFish.Api.Infrastructure.Repositories;

public interface IAuditLogRepository
{
    Task<Result> AddAsync(AuditLog auditLog, CancellationToken ct = default);
    Task<Result<AuditLog>> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Result<List<AuditLog>>> GetAllAsync(int skip = 0, int take = 50, CancellationToken ct = default);
    Task<Result<List<AuditLog>>> GetByActionTypeAsync(string actionType, int skip = 0, int take = 50, CancellationToken ct = default);
    Task<Result<List<AuditLog>>> GetByDateRangeAsync(DateTime startUtc, DateTime endUtc, int skip = 0, int take = 50, CancellationToken ct = default);
    Task<Result<List<AuditLog>>> GetBySourceAsync(string sourceType, string? sourceId = null, int skip = 0, int take = 50, CancellationToken ct = default);
    Task<Result<int>> GetTotalCountAsync(CancellationToken ct = default);
}

