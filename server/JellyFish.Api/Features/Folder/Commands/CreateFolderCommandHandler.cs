using JellyFish.Api.Common;
using JellyFish.Api.Features.Folder.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Folder.Commands;

public sealed class CreateFolderCommandHandler : IRequestHandler<CreateFolderCommand, Result<FolderDto>>
{
    private readonly IFolderRepository _repo;

    public CreateFolderCommandHandler(IFolderRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<FolderDto>> Handle(CreateFolderCommand request, CancellationToken cancellationToken)
    {
        var exists = await _repo.ExistsByNameAsync(request.Name, cancellationToken);
        if (exists)
            return Result.Failure<FolderDto>(Error.Conflict($"Folder '{request.Name}' already exists"));

        var createdResult = JellyFish.Api.Domain.Folder.Create(request.Name, request.IsReadOnly);
        if (createdResult.IsFailure) return Result.Failure<FolderDto>(createdResult.Error);

        createdResult.Value.MarkChanged();
        var addResult = await _repo.AddAsync(createdResult.Value, cancellationToken);
        if (addResult.IsFailure) return Result.Failure<FolderDto>(addResult.Error);

        return Result.Success(FolderMapper.ToDto(createdResult.Value));
    }
}

