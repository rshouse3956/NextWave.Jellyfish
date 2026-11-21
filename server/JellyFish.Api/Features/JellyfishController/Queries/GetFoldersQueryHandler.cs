using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Services.Jellyfish;
using JellyFish.Api.Infrastructure.Services.Jellyfish.Models;
using MediatR;

namespace JellyFish.Api.Features.JellyfishController.Queries;

public sealed class GetFoldersQueryHandler : IRequestHandler<GetFoldersQuery, Result<IReadOnlyList<ControllerFolderDto>>>
{
    private readonly IJellyfishControllerClient _client;
    public GetFoldersQueryHandler(IJellyfishControllerClient client) => _client = client;

    public Task<Result<IReadOnlyList<ControllerFolderDto>>> Handle(GetFoldersQuery request, CancellationToken cancellationToken)
        => _client.GetFoldersAsync(cancellationToken);
}
