using JellyFish.Api.Common;
using JellyFish.Api.Features.Pattern.Errors;
using JellyFish.Api.Features.Pattern.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Pattern.Queries;

public sealed class GetPatternQueryHandler : IRequestHandler<GetPatternQuery, Result<PatternDto>>
{
    private readonly IPatternRepository _patterns;
    private readonly IPatternDetailRepository _details;

    public GetPatternQueryHandler(IPatternRepository patterns, IPatternDetailRepository details)
    {
        _patterns = patterns;
        _details = details;
    }

    public async Task<Result<PatternDto>> Handle(GetPatternQuery request, CancellationToken cancellationToken)
    {
        var pat = await _patterns.GetAsync(request.FolderName, request.Name, cancellationToken);
        if (pat.IsFailure) return Result.Failure<PatternDto>(pat.Error);

        var det = await _details.GetAsync(request.FolderName, request.Name, cancellationToken);
        if (det.IsFailure)
        {
            // If details do not exist yet, return the pattern without details (HTTP 200)
            if (det.Error.Type == ErrorType.NotFound)
                return Result.Success(PatternMapper.ToDto(pat.Value));

            // Propagate any non-NotFound error
            return Result.Failure<PatternDto>(det.Error);
        }

        return Result.Success(PatternMapper.ToDto(pat.Value, det.Value));
    }
}

