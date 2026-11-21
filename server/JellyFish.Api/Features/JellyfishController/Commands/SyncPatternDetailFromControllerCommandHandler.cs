using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Repositories;
using JellyFish.Api.Infrastructure.Services.Jellyfish;
using JellyFish.Api.Infrastructure.Services.Auditing;
using JellyFish.Api.Infrastructure.Persistence;
using MediatR;
using FolderEntity = JellyFish.Api.Domain.Folder;
using PatternEntity = JellyFish.Api.Domain.Pattern;
using PatternDetailEntity = JellyFish.Api.Domain.PatternDetail;

namespace JellyFish.Api.Features.JellyfishController.Commands;

public sealed class SyncPatternDetailFromControllerCommandHandler : IRequestHandler<SyncPatternDetailFromControllerCommand, Result>
{
    private readonly IJellyfishControllerClient _client;
    private readonly IFolderRepository _folders;
    private readonly IPatternRepository _patterns;
    private readonly IPatternDetailRepository _details;
    private readonly IAuditLogService _auditLog;
    private readonly IUnitOfWork _uow;

    public SyncPatternDetailFromControllerCommandHandler(
        IJellyfishControllerClient client,
        IFolderRepository folders,
        IPatternRepository patterns,
        IPatternDetailRepository details,
        IAuditLogService auditLog,
        IUnitOfWork uow)
    {
        _client = client;
        _folders = folders;
        _patterns = patterns;
        _details = details;
        _auditLog = auditLog;
        _uow = uow;
    }

    public async Task<Result> Handle(SyncPatternDetailFromControllerCommand request, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        // Ensure folder exists
        var folderExists = await _folders.ExistsByNameAsync(request.FolderName, cancellationToken);
        if (!folderExists)
        {
            var folderCreate = FolderEntity.Create(request.FolderName, isReadOnly: false);
            if (folderCreate.IsFailure) return Result.Failure(folderCreate.Error);
            folderCreate.Value.MarkSynced();
            var addFolder = await _folders.AddAsync(folderCreate.Value, cancellationToken);
            if (addFolder.IsFailure) return Result.Failure(addFolder.Error);
        }

        // Ensure pattern exists
        var patternExists = await _patterns.ExistsAsync(request.FolderName, request.Name, cancellationToken);
        if (!patternExists)
        {
            var patternCreate = PatternEntity.Create(request.FolderName, request.Name, isReadOnly: false);
            if (patternCreate.IsFailure) return Result.Failure(patternCreate.Error);
            patternCreate.Value.MarkSynced();
            var addPattern = await _patterns.AddAsync(patternCreate.Value, cancellationToken);
            if (addPattern.IsFailure) return Result.Failure(addPattern.Error);
        }

        var res = await _client.GetPatternDetailAsync(request.FolderName, request.Name, cancellationToken);
        if (res.IsFailure && res.Error.Type == ErrorType.NotFound)
        {
            // The controller only matches exact names, and some end in a space ("Paint By Numbers "), while the
            // app stores them trimmed. Look up the exact name and ask again.
            var list = await _client.GetPatternsAsync(cancellationToken);
            var match = list.IsSuccess
                ? list.Value.FirstOrDefault(p =>
                    string.Equals(p.FolderName, request.FolderName.Trim(), StringComparison.Ordinal) &&
                    string.Equals(p.Name, request.Name.Trim(), StringComparison.Ordinal) &&
                    (p.ControllerFolderName != request.FolderName || p.ControllerName != request.Name))
                : null;
            if (match is not null)
                res = await _client.GetPatternDetailAsync(match.ControllerFolderName, match.ControllerName, cancellationToken);
        }
        if (res.IsFailure)
        {
            if (res.Error.Type == ErrorType.NotFound)
            {
                // No detail available from controller; treat as no-op
                return Result.Success();
            }
            return Result.Failure(res.Error);
        }

        var detailEntityRes = PatternDetailEntity.Create(request.FolderName, request.Name, res.Value);
        if (detailEntityRes.IsFailure) return Result.Failure(detailEntityRes.Error);

        detailEntityRes.Value.MarkSynced();
        var upsertRes = await _details.UpsertAsync(detailEntityRes.Value, cancellationToken);
        if (upsertRes.IsFailure) return Result.Failure(upsertRes.Error);

        stopwatch.Stop();

        // Log audit entry
        await _auditLog.LogActionAsync(
            actionType: "SyncPatternDetail",
            sourceType: "System",
            sourceId: "SyncCommand",
            folderName: request.FolderName,
            patternName: request.Name,
            zoneNames: null,
            state: null,
            success: true,
            errorMessage: string.Empty,
            requestPayload: new { request.FolderName, request.Name },
            responsePayload: new { Success = true },
            durationMs: stopwatch.ElapsedMilliseconds,
            ct: cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

