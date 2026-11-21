using JellyFish.Api.Common;
using JellyFish.Api.Features.Pattern.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Pattern.Commands;

public sealed class CreatePatternCommandHandler : IRequestHandler<CreatePatternCommand, Result<PatternDto>>
{
    private readonly IFolderRepository _folders;
    private readonly IPatternRepository _patterns;
    private readonly IPatternDetailRepository _details;

    public CreatePatternCommandHandler(IFolderRepository folders, IPatternRepository patterns, IPatternDetailRepository details)
    {
        _folders = folders;
        _patterns = patterns;
        _details = details;
    }

    public async Task<Result<PatternDto>> Handle(CreatePatternCommand request, CancellationToken cancellationToken)
    {
        var folderExists = await _folders.ExistsByNameAsync(request.FolderName, cancellationToken);
        if (!folderExists) return Result.Failure<PatternDto>(Error.NotFound($"Folder '{request.FolderName}' not found"));

        var already = await _patterns.ExistsAsync(request.FolderName, request.Name, cancellationToken);
        if (already) return Result.Failure<PatternDto>(Error.Conflict($"Pattern '{request.Name}' already exists in folder '{request.FolderName}'"));

        var created = JellyFish.Api.Domain.Pattern.Create(request.FolderName, request.Name, request.IsReadOnly);
        if (created.IsFailure) return Result.Failure<PatternDto>(created.Error);

        created.Value.MarkChanged();
        var add = await _patterns.AddAsync(created.Value, cancellationToken);
        if (add.IsFailure) return Result.Failure<PatternDto>(add.Error);

        // Create a minimal PatternDetail so GET returns details immediately (avoids 404 on fresh patterns)
        var minimalDetailRes = JellyFish.Api.Domain.PatternDetail.Create(
            request.FolderName,
            request.Name,
            new JellyFish.Api.Domain.PatternDetails());
        if (minimalDetailRes.IsFailure) return Result.Failure<PatternDto>(minimalDetailRes.Error);

        minimalDetailRes.Value.MarkChanged();
        var upsertDetail = await _details.UpsertAsync(minimalDetailRes.Value, cancellationToken);
        if (upsertDetail.IsFailure) return Result.Failure<PatternDto>(upsertDetail.Error);

        return Result.Success(PatternMapper.ToDto(created.Value, minimalDetailRes.Value));
    }
}

