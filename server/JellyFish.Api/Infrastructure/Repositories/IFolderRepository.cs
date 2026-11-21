using JellyFish.Api.Common;
using JellyFish.Api.Domain;

namespace JellyFish.Api.Infrastructure.Repositories;

public interface IFolderRepository
{
    Task<Result<Folder>> GetByNameAsync(string name, CancellationToken ct = default);
    Task<Result<List<Folder>>> ListAsync(CancellationToken ct = default);
    Task<Result> AddAsync(Folder folder, CancellationToken ct = default);
    Task<Result> UpdateAsync(Folder folder, CancellationToken ct = default);
    Task<Result> DeleteAsync(string name, CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default);
}

