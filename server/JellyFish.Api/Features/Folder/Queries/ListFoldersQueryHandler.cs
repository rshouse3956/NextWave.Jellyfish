using JellyFish.Api.Common;
using JellyFish.Api.Features.Folder.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Folder.Queries;

public sealed class ListFoldersQueryHandler : IRequestHandler<ListFoldersQuery, Result<List<FolderDto>>>
{
    private readonly IFolderRepository _repo;

    public ListFoldersQueryHandler(IFolderRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<List<FolderDto>>> Handle(ListFoldersQuery request, CancellationToken cancellationToken)
    {
        var res = await _repo.ListAsync(cancellationToken);
        if (res.IsFailure) return Result.Failure<List<FolderDto>>(res.Error);
        return Result.Success(res.Value.Select(FolderMapper.ToDto).ToList());
    }
}

