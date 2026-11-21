using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JellyFish.Api.Infrastructure.Repositories;

public class FolderRepository : IFolderRepository
{
    private readonly JellyfishDbContext _db;

    public FolderRepository(JellyfishDbContext db)
    {
        _db = db;
    }

    public async Task<Result<Folder>> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var entity = await _db.Folders.FirstOrDefaultAsync(f => f.Name == name, ct);
        return entity is null
            ? Result.Failure<Folder>(Error.NotFound($"Folder '{name}' not found"))
            : Result.Success(entity);
    }

    public async Task<Result<List<Folder>>> ListAsync(CancellationToken ct = default)
    {
        var list = await _db.Folders.OrderBy(f => f.Name).ToListAsync(ct);
        return Result.Success(list);
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
        => await _db.Folders.AnyAsync(f => f.Name == name, ct);

    public async Task<Result> AddAsync(Folder folder, CancellationToken ct = default)
    {
        await _db.Folders.AddAsync(folder, ct);
        return Result.Success();
    }

    public Task<Result> UpdateAsync(Folder folder, CancellationToken ct = default)
    {
        _db.Folders.Update(folder);
        return Task.FromResult(Result.Success());
    }

    public async Task<Result> DeleteAsync(string name, CancellationToken ct = default)
    {
        var existing = await _db.Folders.FirstOrDefaultAsync(f => f.Name == name, ct);
        if (existing is null) return Result.Failure(Error.NotFound($"Folder '{name}' not found"));
        _db.Folders.Remove(existing);
        return Result.Success();
    }
}

