using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JellyFish.Api.Infrastructure.Repositories;

public sealed class ActiveZoneRepository : IActiveZoneRepository
{
    private readonly JellyfishDbContext _db;
    public ActiveZoneRepository(JellyfishDbContext db) => _db = db;

    public async Task<Result<ActiveZone>> GetByZoneNameAsync(string zoneName, CancellationToken ct = default)
    {
        var e = await _db.ActiveZones.FirstOrDefaultAsync(a => a.ZoneName == zoneName, ct);
        return e is null ? Result.Failure<ActiveZone>(Error.NotFound($"Active zone '{zoneName}' not found")) : Result.Success(e);
    }

    public async Task<Result<List<ActiveZone>>> ListAsync(CancellationToken ct = default)
    {
        var list = await _db.ActiveZones.OrderBy(a => a.ZoneName).ToListAsync(ct);
        return Result.Success(list);
    }

    public async Task<Result> UpsertAsync(ActiveZone zone, CancellationToken ct = default)
    {
        var existing = await _db.ActiveZones.FirstOrDefaultAsync(a => a.ZoneName == zone.ZoneName, ct);
        if (existing is null)
        {
            await _db.ActiveZones.AddAsync(zone, ct);
        }
        else
        {
            existing.UpdateOwner(zone.SourceType, zone.SourceId, zone.Priority, zone.FolderName, zone.PatternName, zone.ActivatedAtUtc, zone.ExpectedEndUtc);
            _db.ActiveZones.Update(existing);
        }
        return Result.Success();
    }

    public async Task<Result> RemoveByZoneNameAsync(string zoneName, CancellationToken ct = default)
    {
        var existing = await _db.ActiveZones.FirstOrDefaultAsync(a => a.ZoneName == zoneName, ct);
        if (existing is null) return Result.Success();
        _db.ActiveZones.Remove(existing);
        return Result.Success();
    }
}

