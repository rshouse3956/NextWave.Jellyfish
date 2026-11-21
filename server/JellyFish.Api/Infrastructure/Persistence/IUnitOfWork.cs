using System.Threading;
using System.Threading.Tasks;

namespace JellyFish.Api.Infrastructure.Persistence;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}

