using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JellyFish.Api.Infrastructure.Repositories;

public class PatternDetailRepository : IPatternDetailRepository
{
    private readonly JellyfishDbContext _db;

    public PatternDetailRepository(JellyfishDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PatternDetail>> GetAsync(string folderName, string name, CancellationToken ct = default)
    {
        var entity = await _db.PatternDetails.FirstOrDefaultAsync(p => p.FolderName == folderName && p.Name == name, ct);
        return entity is null
            ? Result.Failure<PatternDetail>(Error.NotFound($"PatternDetail '{name}' in folder '{folderName}' not found"))
            : Result.Success(entity);
    }

    public async Task<bool> ExistsAsync(string folderName, string name, CancellationToken ct = default)
        => await _db.PatternDetails.AnyAsync(p => p.FolderName == folderName && p.Name == name, ct);

    public async Task<IReadOnlyList<PatternDetail>> ListAsync(CancellationToken ct = default)
        => await _db.PatternDetails.AsNoTracking().ToListAsync(ct);

    public async Task<Result> UpsertAsync(PatternDetail detail, CancellationToken ct = default)
    {
        var existing = await _db.PatternDetails.FirstOrDefaultAsync(p => p.FolderName == detail.FolderName && p.Name == detail.Name, ct);
        if (existing is null)
        {
            await _db.PatternDetails.AddAsync(detail, ct);
        }
        else
        {
            existing.Update(detail.Details);
            if (detail.SyncRequired.HasValue)
            {
                if (detail.SyncRequired.Value)
                    existing.MarkChanged();
                else
                    existing.MarkSynced();
            }
            _db.PatternDetails.Update(existing);
        }
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(string folderName, string name, CancellationToken ct = default)
    {
        var entity = await _db.PatternDetails.FirstOrDefaultAsync(p => p.FolderName == folderName && p.Name == name, ct);
        if (entity is null) return Result.Success();
        _db.PatternDetails.Remove(entity);
        return Result.Success();
    }
}

