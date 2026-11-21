using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JellyFish.Api.Infrastructure.Repositories;

public class ScheduleRepository : IScheduleRepository
{
    private readonly JellyfishDbContext _db;

    public ScheduleRepository(JellyfishDbContext db)
    {
        _db = db;
    }

    public async Task<Result<Schedule>> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var entity = await _db.Schedules.FirstOrDefaultAsync(s => s.Name == name, ct);
        return entity is null
            ? Result.Failure<Schedule>(Error.NotFound($"Schedule '{name}' not found"))
            : Result.Success(entity);
    }

    public async Task<Result<List<Schedule>>> ListAsync(CancellationToken ct = default)
    {
        var list = await _db.Schedules.OrderBy(s => s.Name).ToListAsync(ct);
        return Result.Success(list);
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
        => await _db.Schedules.AnyAsync(s => s.Name == name, ct);

    public async Task<Result> AddAsync(Schedule schedule, CancellationToken ct = default)
    {
        await _db.Schedules.AddAsync(schedule, ct);
        return Result.Success();
    }

    public Task<Result> UpdateAsync(Schedule schedule, CancellationToken ct = default)
    {
        _db.Schedules.Update(schedule);
        return Task.FromResult(Result.Success());
    }

    public async Task<Result> DeleteAsync(string name, CancellationToken ct = default)
    {
        var existing = await _db.Schedules.FirstOrDefaultAsync(s => s.Name == name, ct);
        if (existing is null) return Result.Failure(Error.NotFound($"Schedule '{name}' not found"));
        _db.Schedules.Remove(existing);
        return Result.Success();
    }
}

