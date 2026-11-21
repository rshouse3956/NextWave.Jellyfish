using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Services.Jellyfish;
using JellyFish.Api.Infrastructure.Services.Jellyfish.Models;
using MediatR;

namespace JellyFish.Api.Features.JellyfishController.Queries;

public sealed class GetPatternsQueryHandler : IRequestHandler<GetPatternsQuery, Result<IReadOnlyList<ControllerPatternListItemDto>>>
{
    private readonly IJellyfishControllerClient _client;
    public GetPatternsQueryHandler(IJellyfishControllerClient client) => _client = client;

    public Task<Result<IReadOnlyList<ControllerPatternListItemDto>>> Handle(GetPatternsQuery request, CancellationToken cancellationToken)
        => _client.GetPatternsAsync(cancellationToken);
}
