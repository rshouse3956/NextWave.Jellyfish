using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Repositories;
using JellyFish.Api.Infrastructure.Services.Jellyfish;
using JellyFish.Api.Infrastructure.Services.Auditing;
using JellyFish.Api.Infrastructure.Persistence;
using MediatR;
using FolderEntity = JellyFish.Api.Domain.Folder;

namespace JellyFish.Api.Features.JellyfishController.Commands;

public sealed class SyncFoldersFromControllerCommandHandler : IRequestHandler<SyncFoldersFromControllerCommand, Result>
{
    private readonly IJellyfishControllerClient _client;
    private readonly IFolderRepository _folders;
    private readonly IAuditLogService _auditLog;
    private readonly IUnitOfWork _uow;

    public SyncFoldersFromControllerCommandHandler(
        IJellyfishControllerClient client,
        IFolderRepository folders,
        IAuditLogService auditLog,
        IUnitOfWork uow)
    {
        _client = client;
        _folders = folders;
        _auditLog = auditLog;
        _uow = uow;
    }

    public async Task<Result> Handle(SyncFoldersFromControllerCommand request, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var res = await _client.GetFoldersAsync(cancellationToken);
        if (res.IsFailure) return Result.Failure(res.Error);

        var aggregate = new List<Result>();

        foreach (var f in res.Value)
        {
            var exists = await _folders.ExistsByNameAsync(f.Name, cancellationToken);
            if (!exists)
            {
                var created = FolderEntity.Create(f.Name, f.IsReadOnly);
                if (created.IsFailure) { aggregate.Add(Result.Failure(created.Error)); continue; }
                created.Value.MarkSynced();
                aggregate.Add(await _folders.AddAsync(created.Value, cancellationToken));
            }
            else
            {
                var current = await _folders.GetByNameAsync(f.Name, cancellationToken);
                if (current.IsFailure) { aggregate.Add(Result.Failure(current.Error)); continue; }
                current.Value.Update(f.IsReadOnly);
                current.Value.MarkSynced();
                aggregate.Add(await _folders.UpdateAsync(current.Value, cancellationToken));
            }
        }

        stopwatch.Stop();

        var finalResult = aggregate.Any(r => r.IsFailure)
            ? Result.Failure(ValidationError.FromResults(aggregate))
            : Result.Success();

        // Log audit entry
        await _auditLog.LogActionAsync(
            actionType: "SyncFolders",
            sourceType: "System",
            sourceId: "SyncCommand",
            folderName: string.Empty,
            patternName: string.Empty,
            zoneNames: null,
            state: null,
            success: finalResult.IsSuccess,
            errorMessage: finalResult.IsFailure ? finalResult.Error.Description : string.Empty,
            requestPayload: new { Action = "SyncFolders" },
            responsePayload: new { FolderCount = res.Value.Count },
            durationMs: stopwatch.ElapsedMilliseconds,
            ct: cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        return finalResult;
    }
}

