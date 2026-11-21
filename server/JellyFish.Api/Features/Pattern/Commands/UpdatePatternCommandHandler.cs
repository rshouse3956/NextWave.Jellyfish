using JellyFish.Api.Common;
using JellyFish.Api.Features.Pattern.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Pattern.Commands;

public sealed class UpdatePatternCommandHandler : IRequestHandler<UpdatePatternCommand, Result<PatternDto>>
{
    private readonly IPatternRepository _patterns;

    public UpdatePatternCommandHandler(IPatternRepository patterns)
    {
        _patterns = patterns;
    }

    public async Task<Result<PatternDto>> Handle(UpdatePatternCommand request, CancellationToken cancellationToken)
    {
        var get = await _patterns.GetAsync(request.FolderName, request.Name, cancellationToken);
        if (get.IsFailure) return Result.Failure<PatternDto>(get.Error);

        var entity = get.Value;
        // Allow toggling IsReadOnly even when currently read-only; no other attributes are updated via this command
        entity.Update(request.IsReadOnly);
        entity.MarkChanged();
        var save = await _patterns.UpdateAsync(entity, cancellationToken);
        if (save.IsFailure) return Result.Failure<PatternDto>(save.Error);

        return Result.Success(PatternMapper.ToDto(entity));
    }
}

