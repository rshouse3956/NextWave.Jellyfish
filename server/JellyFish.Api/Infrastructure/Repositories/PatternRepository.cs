using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JellyFish.Api.Infrastructure.Repositories;

public class PatternRepository : IPatternRepository
{
    private readonly JellyfishDbContext _db;

    public PatternRepository(JellyfishDbContext db)
    {
        _db = db;
    }

    public async Task<Result<Pattern>> GetAsync(string folderName, string name, CancellationToken ct = default)
    {
        var entity = await _db.Patterns.FirstOrDefaultAsync(p => p.FolderName == folderName && p.Name == name, ct);
        return entity is null
            ? Result.Failure<Pattern>(Error.NotFound($"Pattern '{name}' in folder '{folderName}' not found"))
            : Result.Success(entity);
    }

    public async Task<Result<List<Pattern>>> ListByFolderAsync(string folderName, CancellationToken ct = default)
    {
        var list = await _db.Patterns.Where(p => p.FolderName == folderName).OrderBy(p => p.Name).ToListAsync(ct);
        return Result.Success(list);
    }

    public async Task<bool> ExistsAsync(string folderName, string name, CancellationToken ct = default)
        => await _db.Patterns.AnyAsync(p => p.FolderName == folderName && p.Name == name, ct);

    public async Task<Result> AddAsync(Pattern pattern, CancellationToken ct = default)
    {
        await _db.Patterns.AddAsync(pattern, ct);
        return Result.Success();
    }

    public Task<Result> UpdateAsync(Pattern pattern, CancellationToken ct = default)
    {
        _db.Patterns.Update(pattern);
        return Task.FromResult(Result.Success());
    }

    public async Task<Result> DeleteAsync(string folderName, string name, CancellationToken ct = default)
    {
        var existing = await _db.Patterns.FirstOrDefaultAsync(p => p.FolderName == folderName && p.Name == name, ct);
        if (existing is null) return Result.Failure(Error.NotFound($"Pattern '{name}' in folder '{folderName}' not found"));
        _db.Patterns.Remove(existing);
        return Result.Success();
    }
}

