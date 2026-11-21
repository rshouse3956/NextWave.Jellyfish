using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Services.Jellyfish;
using JellyFish.Api.Infrastructure.Services.Jellyfish.Models;
using MediatR;

namespace JellyFish.Api.Features.JellyfishController.Queries;

public sealed class GetZonesQueryHandler : IRequestHandler<GetZonesQuery, Result<IReadOnlyList<ControllerZoneDto>>>
{
    private readonly IJellyfishControllerClient _client;
    public GetZonesQueryHandler(IJellyfishControllerClient client) => _client = client;

    public Task<Result<IReadOnlyList<ControllerZoneDto>>> Handle(GetZonesQuery request, CancellationToken cancellationToken)
        => _client.GetZonesAsync(cancellationToken);
}
