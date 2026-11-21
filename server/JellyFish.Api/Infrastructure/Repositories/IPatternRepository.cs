using JellyFish.Api.Common;
using JellyFish.Api.Domain;

namespace JellyFish.Api.Infrastructure.Repositories;

public interface IPatternRepository
{
    Task<Result<Pattern>> GetAsync(string folderName, string name, CancellationToken ct = default);
    Task<Result<List<Pattern>>> ListByFolderAsync(string folderName, CancellationToken ct = default);
    Task<Result> AddAsync(Pattern pattern, CancellationToken ct = default);
    Task<Result> UpdateAsync(Pattern pattern, CancellationToken ct = default);
    Task<Result> DeleteAsync(string folderName, string name, CancellationToken ct = default);
    Task<bool> ExistsAsync(string folderName, string name, CancellationToken ct = default);
}

