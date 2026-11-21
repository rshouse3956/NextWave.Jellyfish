using JellyFish.Api.Common;
using JellyFish.Api.Domain;

namespace JellyFish.Api.Infrastructure.Repositories;

public interface IHolidayRepository
{
    Task<Result<Holiday>> GetByNameAsync(string name, CancellationToken ct = default);
    Task<Result<List<Holiday>>> ListAsync(CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default);
    Task<Result> AddAsync(Holiday holiday, CancellationToken ct = default);
    Task<Result> UpdateAsync(Holiday holiday, CancellationToken ct = default);
    Task<Result> DeleteAsync(string name, CancellationToken ct = default);
}

