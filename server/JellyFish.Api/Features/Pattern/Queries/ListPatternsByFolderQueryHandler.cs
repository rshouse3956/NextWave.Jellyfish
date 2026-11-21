using JellyFish.Api.Common;
using JellyFish.Api.Features.Pattern.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Pattern.Queries;

public sealed class ListPatternsByFolderQueryHandler : IRequestHandler<ListPatternsByFolderQuery, Result<List<PatternDto>>>
{
    private readonly IPatternRepository _patterns;

    public ListPatternsByFolderQueryHandler(IPatternRepository patterns)
    {
        _patterns = patterns;
    }

    public async Task<Result<List<PatternDto>>> Handle(ListPatternsByFolderQuery request, CancellationToken cancellationToken)
    {
        var res = await _patterns.ListByFolderAsync(request.FolderName, cancellationToken);
        if (res.IsFailure) return Result.Failure<List<PatternDto>>(res.Error);
        return Result.Success(res.Value.Select(PatternMapper.ToDto).ToList());
    }
}

