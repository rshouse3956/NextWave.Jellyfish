using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Repositories;
using JellyFish.Api.Infrastructure.Services.Jellyfish;
using MediatR;
using Microsoft.Extensions.Logging;
using FolderEntity = JellyFish.Api.Domain.Folder;
using PatternEntity = JellyFish.Api.Domain.Pattern;
using PatternDetailEntity = JellyFish.Api.Domain.PatternDetail;

namespace JellyFish.Api.Features.JellyfishController.Commands;

/// <summary>
/// Fetches every pattern's details from the controller and stores them. One pattern failing (e.g. a controller
/// timeout) doesn't discard the rest: the others are saved and the failures are listed in the summary.
/// </summary>
public sealed class SyncAllPatternDetailsFromControllerCommandHandler
    : IRequestHandler<SyncAllPatternDetailsFromControllerCommand, Result<SyncPatternDetailsSummaryDto>>
{
    private readonly IJellyfishControllerClient _client;
    private readonly IFolderRepository _folders;
    private readonly IPatternRepository _patterns;
    private readonly IPatternDetailRepository _details;
    private readonly ILogger<SyncAllPatternDetailsFromControllerCommandHandler> _logger;

    public SyncAllPatternDetailsFromControllerCommandHandler(
        IJellyfishControllerClient client,
        IFolderRepository folders,
        IPatternRepository patterns,
        IPatternDetailRepository details,
        ILogger<SyncAllPatternDetailsFromControllerCommandHandler> logger)
    {
        _client = client;
        _folders = folders;
        _patterns = patterns;
        _details = details;
        _logger = logger;
    }

    public async Task<Result<SyncPatternDetailsSummaryDto>> Handle(SyncAllPatternDetailsFromControllerCommand request, CancellationToken cancellationToken)
    {
        var listRes = await _client.GetPatternsAsync(cancellationToken);
        if (listRes.IsFailure) return Result.Failure<SyncPatternDetailsSummaryDto>(listRes.Error);

        var ensuredFolders = new HashSet<string>(StringComparer.Ordinal);
        var seenPatternKeys = new HashSet<string>(StringComparer.Ordinal);
        var noDetails = new List<string>();
        var failed = new List<SyncPatternFailureDto>();
        var synced = 0;

        foreach (var p in listRes.Value.OrderBy(p => p.FolderName).ThenBy(p => p.Name))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var folderName = p.FolderName?.Trim() ?? string.Empty;
            var name = p.Name?.Trim() ?? string.Empty;
            // The app stores trimmed names, so "Turquoise" and "Turquoise " on the controller are one pattern here.
            if (!seenPatternKeys.Add($"{folderName}||{name}")) continue;

            void Fail(Error error) => failed.Add(new SyncPatternFailureDto(folderName, name, error.Description));

            if (ensuredFolders.Add(folderName) && !await _folders.ExistsByNameAsync(folderName, cancellationToken))
            {
                var folderCreate = FolderEntity.Create(folderName, isReadOnly: false);
                if (folderCreate.IsFailure) { Fail(folderCreate.Error); continue; }
                folderCreate.Value.MarkSynced();
                var addFolder = await _folders.AddAsync(folderCreate.Value, cancellationToken);
                if (addFolder.IsFailure) { Fail(addFolder.Error); continue; }
            }

            if (!await _patterns.ExistsAsync(folderName, name, cancellationToken))
            {
                var patternCreate = PatternEntity.Create(folderName, name, p.IsReadOnly);
                if (patternCreate.IsFailure) { Fail(patternCreate.Error); continue; }
                patternCreate.Value.MarkSynced();
                var addPattern = await _patterns.AddAsync(patternCreate.Value, cancellationToken);
                if (addPattern.IsFailure) { Fail(addPattern.Error); continue; }
            }

            // Ask with the controller's exact names: it doesn't match a trimmed name.
            var detailRes = await GetDetailWithRetryAsync(ControllerFolder(p), ControllerName(p), cancellationToken);
            if (detailRes.IsFailure)
            {
                if (detailRes.Error.Type == ErrorType.NotFound) noDetails.Add($"{folderName}/{name}");
                else Fail(detailRes.Error);
                continue;
            }

            var detailEntityRes = PatternDetailEntity.Create(folderName, name, detailRes.Value);
            if (detailEntityRes.IsFailure) { Fail(detailEntityRes.Error); continue; }

            detailEntityRes.Value.MarkSynced();
            var upsertRes = await _details.UpsertAsync(detailEntityRes.Value, cancellationToken);
            if (upsertRes.IsFailure) { Fail(upsertRes.Error); continue; }
            synced++;
        }

        if (failed.Count > 0)
            _logger.LogWarning("Pattern detail sync: {Failed} of {Total} patterns failed", failed.Count, seenPatternKeys.Count);

        // Success, so the unit of work saves the patterns that did sync; failures are reported in the summary.
        return Result.Success(new SyncPatternDetailsSummaryDto(seenPatternKeys.Count, synced, noDetails, failed));
    }

    /// <summary>One retry for transient controller errors (timeouts); "no details" is an answer, not an error.</summary>
    private async Task<Result<Domain.PatternDetails>> GetDetailWithRetryAsync(string folderName, string name, CancellationToken ct)
    {
        var res = await _client.GetPatternDetailAsync(folderName, name, ct);
        if (res.IsSuccess || res.Error.Type == ErrorType.NotFound) return res;
        return await _client.GetPatternDetailAsync(folderName, name, ct);
    }

    private static string ControllerFolder(Infrastructure.Services.Jellyfish.Models.ControllerPatternListItemDto p) =>
        string.IsNullOrEmpty(p.ControllerFolderName) ? p.FolderName : p.ControllerFolderName;

    private static string ControllerName(Infrastructure.Services.Jellyfish.Models.ControllerPatternListItemDto p) =>
        string.IsNullOrEmpty(p.ControllerName) ? p.Name : p.ControllerName;
}
