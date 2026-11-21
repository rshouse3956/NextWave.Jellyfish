using JellyFish.Api.Common;
using JellyFish.Api.Features.Folder.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Folder.Commands;

public sealed class UpdateFolderCommandHandler : IRequestHandler<UpdateFolderCommand, Result<FolderDto>>
{
    private readonly IFolderRepository _repo;

    public UpdateFolderCommandHandler(IFolderRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<FolderDto>> Handle(UpdateFolderCommand request, CancellationToken cancellationToken)
    {
        var get = await _repo.GetByNameAsync(request.Name, cancellationToken);
        if (get.IsFailure) return Result.Failure<FolderDto>(get.Error);

        var entity = get.Value;
        // Allow updating IsReadOnly even when currently read-only (relax restriction)
        entity.Update(request.IsReadOnly);
        entity.MarkChanged();
        var save = await _repo.UpdateAsync(entity, cancellationToken);
        if (save.IsFailure) return Result.Failure<FolderDto>(save.Error);

        return Result.Success(FolderMapper.ToDto(entity));
    }
}

