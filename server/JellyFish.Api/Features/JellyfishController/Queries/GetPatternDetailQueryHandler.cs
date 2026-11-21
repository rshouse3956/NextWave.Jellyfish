using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Infrastructure.Services.Jellyfish;
using MediatR;

namespace JellyFish.Api.Features.JellyfishController.Queries;

public sealed class GetPatternDetailQueryHandler : IRequestHandler<GetPatternDetailQuery, Result<PatternDetails>>
{
    private readonly IJellyfishControllerClient _client;
    public GetPatternDetailQueryHandler(IJellyfishControllerClient client) => _client = client;

    public Task<Result<PatternDetails>> Handle(GetPatternDetailQuery request, CancellationToken cancellationToken)
        => _client.GetPatternDetailAsync(request.FolderName, request.Name, cancellationToken);
}
