using JellyFish.Api.Common;
using JellyFish.Api.Features.Pattern.Errors;
using JellyFish.Api.Features.Pattern.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Pattern.Commands;

public sealed class UpsertPatternDetailCommandHandler : IRequestHandler<UpsertPatternDetailCommand, Result<PatternDto>>
{
    private readonly IPatternRepository _patterns;
    private readonly IPatternDetailRepository _details;

    public UpsertPatternDetailCommandHandler(IPatternRepository patterns, IPatternDetailRepository details)
    {
        _patterns = patterns;
        _details = details;
    }

    public async Task<Result<PatternDto>> Handle(UpsertPatternDetailCommand request, CancellationToken cancellationToken)
    {
        var pat = await _patterns.GetAsync(request.FolderName, request.Name, cancellationToken);
        if (pat.IsFailure) return Result.Failure<PatternDto>(PatternErrors.NotFound(request.FolderName, request.Name));

        if (pat.Value.IsReadOnly)
            return Result.Failure<PatternDto>(PatternErrors.ReadOnly(request.FolderName, request.Name));

        var detailEntity = JellyFish.Api.Domain.PatternDetail.Create(
            request.FolderName,
            request.Name,
            PatternMapper.FromDetailsDto(request.Details)
        );
        if (detailEntity.IsFailure) return Result.Failure<PatternDto>(detailEntity.Error);

        detailEntity.Value.MarkChanged();
        var upsert = await _details.UpsertAsync(detailEntity.Value, cancellationToken);
        if (upsert.IsFailure) return Result.Failure<PatternDto>(upsert.Error);

        // Build response from the just-upserted entity to avoid pre-commit read issues
        return Result.Success(PatternMapper.ToDto(pat.Value, detailEntity.Value));
    }
}

