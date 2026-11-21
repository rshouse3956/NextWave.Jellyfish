using JellyFish.Api.Common;
using JellyFish.Api.Domain;

namespace JellyFish.Api.Infrastructure.Repositories;

public interface IZoneRepository
{
    Task<Result<Zone>> GetByNameAsync(string name, CancellationToken ct = default);
    Task<Result<List<Zone>>> ListAsync(CancellationToken ct = default);
    Task<Result> AddAsync(Zone zone, CancellationToken ct = default);
    Task<Result> UpdateAsync(Zone zone, CancellationToken ct = default);
    Task<Result> DeleteAsync(string name, CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default);
}

