using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Persistence;
using MediatR;

namespace JellyFish.Api.Features.Health.Queries;

public class GetHealthQueryHandler : IRequestHandler<GetHealthQuery, Result<HealthDto>>
{
    private readonly JellyfishDbContext _db;

    public GetHealthQueryHandler(JellyfishDbContext db)
    {
        _db = db;
    }

    public Task<Result<HealthDto>> Handle(GetHealthQuery request, CancellationToken cancellationToken)
    {
        var dto = new HealthDto(
            Status: "OK",
            Utc: DateTime.UtcNow.ToString("O"),
            Version: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");

        return Task.FromResult(Result.Success(dto));
    }
}

