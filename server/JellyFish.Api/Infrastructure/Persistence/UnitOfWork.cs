using Microsoft.EntityFrameworkCore;

namespace JellyFish.Api.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly JellyfishDbContext _db;

    public UnitOfWork(JellyfishDbContext db)
    {
        _db = db;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

