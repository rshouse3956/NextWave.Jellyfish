using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Repositories;
using JellyFish.Api.Infrastructure.Services.Jellyfish;
using JellyFish.Api.Infrastructure.Services.Auditing;
using JellyFish.Api.Infrastructure.Persistence;
using MediatR;
using FolderEntity = JellyFish.Api.Domain.Folder;
using PatternEntity = JellyFish.Api.Domain.Pattern;

namespace JellyFish.Api.Features.JellyfishController.Commands;

public sealed class SyncPatternsFromControllerCommandHandler : IRequestHandler<SyncPatternsFromControllerCommand, Result>
{
    private readonly IJellyfishControllerClient _client;
    private readonly IFolderRepository _folders;
    private readonly IPatternRepository _patterns;
    private readonly IAuditLogService _auditLog;
    private readonly IUnitOfWork _uow;

    public SyncPatternsFromControllerCommandHandler(
        IJellyfishControllerClient client,
        IFolderRepository folders,
        IPatternRepository patterns,
        IAuditLogService auditLog,
        IUnitOfWork uow)
    {
        _client = client;
        _folders = folders;
        _patterns = patterns;
        _auditLog = auditLog;
        _uow = uow;
    }

    public async Task<Result> Handle(SyncPatternsFromControllerCommand request, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var res = await _client.GetPatternsAsync(cancellationToken);
        if (res.IsFailure) return Result.Failure(res.Error);

        var aggregate = new List<Result>();

        // De-duplicate controller responses to avoid duplicate inserts within the same unit-of-work
        var ensuredFolders = new HashSet<string>(StringComparer.Ordinal);
        var seenPatternKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var p in res.Value.OrderBy(p => p.FolderName).ThenBy(p => p.Name))
        {
            var patternKey = $"{p.FolderName.Trim()}||{p.Name.Trim()}";
            if (!seenPatternKeys.Add(patternKey))
            {
                // Skip duplicates from controller response
                continue;
            }

            // Ensure folder exists to maintain relational integrity (avoid re-adding in this batch)
            if (!ensuredFolders.Contains(p.FolderName.Trim()))
            {
                var folderExists = await _folders.ExistsByNameAsync(p.FolderName, cancellationToken);
                if (!folderExists)
                {
                    var folderCreate = FolderEntity.Create(p.FolderName, isReadOnly: false);
                    if (folderCreate.IsFailure) { aggregate.Add(Result.Failure(folderCreate.Error)); continue; }
                    folderCreate.Value.MarkSynced();
                    aggregate.Add(await _folders.AddAsync(folderCreate.Value, cancellationToken));
                }
                ensuredFolders.Add(p.FolderName.Trim());
            }

            var exists = await _patterns.ExistsAsync(p.FolderName, p.Name, cancellationToken);
            if (!exists)
            {
                var created = PatternEntity.Create(p.FolderName, p.Name, p.IsReadOnly);
                if (created.IsFailure) { aggregate.Add(Result.Failure(created.Error)); continue; }
                created.Value.MarkSynced();
                aggregate.Add(await _patterns.AddAsync(created.Value, cancellationToken));
            }
            else
            {
                var current = await _patterns.GetAsync(p.FolderName, p.Name, cancellationToken);
                if (current.IsFailure) { aggregate.Add(Result.Failure(current.Error)); continue; }
                current.Value.Update(p.IsReadOnly);
                current.Value.MarkSynced();
                aggregate.Add(await _patterns.UpdateAsync(current.Value, cancellationToken));
            }
        }

        stopwatch.Stop();

        var finalResult = aggregate.Any(r => r.IsFailure)
            ? Result.Failure(ValidationError.FromResults(aggregate))
            : Result.Success();

        // Log audit entry
        await _auditLog.LogActionAsync(
            actionType: "SyncPatterns",
            sourceType: "System",
            sourceId: "SyncCommand",
            folderName: string.Empty,
            patternName: string.Empty,
            zoneNames: null,
            state: null,
            success: finalResult.IsSuccess,
            errorMessage: finalResult.IsFailure ? finalResult.Error.Description : string.Empty,
            requestPayload: new { Action = "SyncPatterns" },
            responsePayload: new { PatternCount = res.Value.Count },
            durationMs: stopwatch.ElapsedMilliseconds,
            ct: cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        return finalResult;
    }
}

