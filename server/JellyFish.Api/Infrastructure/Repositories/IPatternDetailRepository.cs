using JellyFish.Api.Common;
using JellyFish.Api.Domain;

namespace JellyFish.Api.Infrastructure.Repositories;

public interface IPatternDetailRepository
{
    Task<Result<PatternDetail>> GetAsync(string folderName, string name, CancellationToken ct = default);
    Task<bool> ExistsAsync(string folderName, string name, CancellationToken ct = default);
    /// <summary>All stored pattern details, read-only (e.g. as examples for the pattern assistant).</summary>
    Task<IReadOnlyList<PatternDetail>> ListAsync(CancellationToken ct = default);
    Task<Result> UpsertAsync(PatternDetail detail, CancellationToken ct = default);
    Task<Result> DeleteAsync(string folderName, string name, CancellationToken ct = default);
}

