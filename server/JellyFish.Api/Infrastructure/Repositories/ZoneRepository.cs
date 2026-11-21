using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JellyFish.Api.Infrastructure.Repositories;

public class ZoneRepository : IZoneRepository
{
    private readonly JellyfishDbContext _db;

    public ZoneRepository(JellyfishDbContext db)
    {
        _db = db;
    }

    public async Task<Result<Zone>> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var entity = await _db.Zones.FirstOrDefaultAsync(z => z.Name == name, ct);
        return entity is null
            ? Result.Failure<Zone>(Error.NotFound($"Zone '{name}' not found"))
            : Result.Success(entity);
    }

    public async Task<Result<List<Zone>>> ListAsync(CancellationToken ct = default)
    {
        var list = await _db.Zones.OrderBy(z => z.Name).ToListAsync(ct);
        return Result.Success(list);
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
        => await _db.Zones.AnyAsync(z => z.Name == name, ct);

    public async Task<Result> AddAsync(Zone zone, CancellationToken ct = default)
    {
        await _db.Zones.AddAsync(zone, ct);
        return Result.Success();
    }

    public Task<Result> UpdateAsync(Zone zone, CancellationToken ct = default)
    {
        _db.Zones.Update(zone);
        return Task.FromResult(Result.Success());
    }

    public async Task<Result> DeleteAsync(string name, CancellationToken ct = default)
    {
        var existing = await _db.Zones.FirstOrDefaultAsync(z => z.Name == name, ct);
        if (existing is null) return Result.Failure(Error.NotFound($"Zone '{name}' not found"));
        _db.Zones.Remove(existing);
        return Result.Success();
    }
}

