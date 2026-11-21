using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JellyFish.Api.Infrastructure.Repositories;

public class HolidayRepository : IHolidayRepository
{
    private readonly JellyfishDbContext _db;

    public HolidayRepository(JellyfishDbContext db)
    {
        _db = db;
    }

    public async Task<Result<Holiday>> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var entity = await _db.Holidays.FirstOrDefaultAsync(h => h.Name == name, ct);
        return entity is null
            ? Result.Failure<Holiday>(Error.NotFound($"Holiday '{name}' not found"))
            : Result.Success(entity);
    }

    public async Task<Result<List<Holiday>>> ListAsync(CancellationToken ct = default)
    {
        var list = await _db.Holidays.OrderBy(h => h.Name).ToListAsync(ct);
        return Result.Success(list);
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
        => await _db.Holidays.AnyAsync(h => h.Name == name, ct);

    public async Task<Result> AddAsync(Holiday holiday, CancellationToken ct = default)
    {
        await _db.Holidays.AddAsync(holiday, ct);
        return Result.Success();
    }

    public Task<Result> UpdateAsync(Holiday holiday, CancellationToken ct = default)
    {
        _db.Holidays.Update(holiday);
        return Task.FromResult(Result.Success());
    }

    public async Task<Result> DeleteAsync(string name, CancellationToken ct = default)
    {
        var existing = await _db.Holidays.FirstOrDefaultAsync(h => h.Name == name, ct);
        if (existing is null) return Result.Failure(Error.NotFound($"Holiday '{name}' not found"));
        _db.Holidays.Remove(existing);
        return Result.Success();
    }
}

