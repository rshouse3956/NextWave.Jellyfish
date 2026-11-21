using JellyFish.Api.Common;
using JellyFish.Api.Domain;

namespace JellyFish.Api.Infrastructure.Repositories;

public interface IScheduleRepository
{
    Task<Result<Schedule>> GetByNameAsync(string name, CancellationToken ct = default);
    Task<Result<List<Schedule>>> ListAsync(CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default);
    Task<Result> AddAsync(Schedule schedule, CancellationToken ct = default);
    Task<Result> UpdateAsync(Schedule schedule, CancellationToken ct = default);
    Task<Result> DeleteAsync(string name, CancellationToken ct = default);
}

