using JellyFish.Api.Common;
using JellyFish.Api.Features.Folder.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Folder.Queries;

public sealed class GetFolderQueryHandler : IRequestHandler<GetFolderQuery, Result<FolderDto>>
{
    private readonly IFolderRepository _repo;

    public GetFolderQueryHandler(IFolderRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<FolderDto>> Handle(GetFolderQuery request, CancellationToken cancellationToken)
    {
        var res = await _repo.GetByNameAsync(request.Name, cancellationToken);
        return res.IsSuccess ? Result.Success(FolderMapper.ToDto(res.Value)) : Result.Failure<FolderDto>(res.Error);
    }
}

