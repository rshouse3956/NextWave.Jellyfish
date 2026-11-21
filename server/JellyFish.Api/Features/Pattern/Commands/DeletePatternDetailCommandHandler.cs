using JellyFish.Api.Common;
using JellyFish.Api.Features.Pattern.Errors;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Pattern.Commands;

public sealed class DeletePatternDetailCommandHandler : IRequestHandler<DeletePatternDetailCommand, Result>
{
    private readonly IPatternRepository _patterns;
    private readonly IPatternDetailRepository _details;

    public DeletePatternDetailCommandHandler(IPatternRepository patterns, IPatternDetailRepository details)
    {
        _patterns = patterns;
        _details = details;
    }

    public async Task<Result> Handle(DeletePatternDetailCommand request, CancellationToken cancellationToken)
    {
        var pat = await _patterns.GetAsync(request.FolderName, request.Name, cancellationToken);
        if (pat.IsFailure) return Result.Failure(PatternErrors.NotFound(request.FolderName, request.Name));

        if (pat.Value.IsReadOnly)
            return Result.Failure(PatternErrors.ReadOnly(request.FolderName, request.Name));

        return await _details.DeleteAsync(request.FolderName, request.Name, cancellationToken);
    }
}

