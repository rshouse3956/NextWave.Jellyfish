using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Pattern.Commands;

public sealed class DeletePatternCommandHandler : IRequestHandler<DeletePatternCommand, Result>
{
    private readonly IPatternRepository _patterns;

    public DeletePatternCommandHandler(IPatternRepository patterns)
    {
        _patterns = patterns;
    }

    public async Task<Result> Handle(DeletePatternCommand request, CancellationToken cancellationToken)
    {
        var get = await _patterns.GetAsync(request.FolderName, request.Name, cancellationToken);
        if (get.IsFailure) return Result.Failure(get.Error);

        if (get.Value.IsReadOnly)
            return Result.Failure(Error.Conflict($"Pattern '{get.Value.Name}' in folder '{get.Value.FolderName}' is read-only"));

        return await _patterns.DeleteAsync(request.FolderName, request.Name, cancellationToken);
    }
}

