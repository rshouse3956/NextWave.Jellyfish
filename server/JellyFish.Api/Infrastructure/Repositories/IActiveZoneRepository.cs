using JellyFish.Api.Common;
using JellyFish.Api.Domain;

namespace JellyFish.Api.Infrastructure.Repositories;

public interface IActiveZoneRepository
{
    Task<Result<ActiveZone>> GetByZoneNameAsync(string zoneName, CancellationToken ct = default);
    Task<Result<List<ActiveZone>>> ListAsync(CancellationToken ct = default);
    Task<Result> UpsertAsync(ActiveZone zone, CancellationToken ct = default);
    Task<Result> RemoveByZoneNameAsync(string zoneName, CancellationToken ct = default);
}

