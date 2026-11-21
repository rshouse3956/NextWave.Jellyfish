using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Folder.Commands;

public sealed class DeleteFolderCommandHandler : IRequestHandler<DeleteFolderCommand, Result>
{
    private readonly IFolderRepository _repo;

    public DeleteFolderCommandHandler(IFolderRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result> Handle(DeleteFolderCommand request, CancellationToken cancellationToken)
    {
        var get = await _repo.GetByNameAsync(request.Name, cancellationToken);
        if (get.IsFailure) return Result.Failure(get.Error);

        if (get.Value.IsReadOnly)
            return Result.Failure(Error.Conflict($"Folder '{get.Value.Name}' is read-only"));

        return await _repo.DeleteAsync(request.Name, cancellationToken);
    }
}

