using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Services.Jellyfish;
using MediatR;

namespace JellyFish.Api.Features.JellyfishController.Queries;

public sealed class HealthCheckQueryHandler : IRequestHandler<HealthCheckQuery, Result>
{
    private readonly IJellyfishControllerClient _client;
    public HealthCheckQueryHandler(IJellyfishControllerClient client) => _client = client;

    public async Task<Result> Handle(HealthCheckQuery request, CancellationToken cancellationToken)
    {
        return await _client.HealthCheckAsync(cancellationToken);
    }
}

