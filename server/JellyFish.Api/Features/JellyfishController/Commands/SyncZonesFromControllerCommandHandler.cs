using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Repositories;
using JellyFish.Api.Infrastructure.Services.Jellyfish;
using JellyFish.Api.Infrastructure.Services.Auditing;
using JellyFish.Api.Infrastructure.Persistence;
using MediatR;
using ZoneEntity = JellyFish.Api.Domain.Zone;

namespace JellyFish.Api.Features.JellyfishController.Commands;

public sealed class SyncZonesFromControllerCommandHandler : IRequestHandler<SyncZonesFromControllerCommand, Result>
{
    private readonly IJellyfishControllerClient _client;
    private readonly IZoneRepository _zones;
    private readonly IAuditLogService _auditLog;
    private readonly IUnitOfWork _uow;

    public SyncZonesFromControllerCommandHandler(
        IJellyfishControllerClient client,
        IZoneRepository zones,
        IAuditLogService auditLog,
        IUnitOfWork uow)
    {
        _client = client;
        _zones = zones;
        _auditLog = auditLog;
        _uow = uow;
    }

    public async Task<Result> Handle(SyncZonesFromControllerCommand request, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var res = await _client.GetZonesAsync(cancellationToken);
        if (res.IsFailure) return Result.Failure(res.Error);

        var aggregate = new List<Result>();

        foreach (var z in res.Value)
        {
            var exists = await _zones.ExistsByNameAsync(z.Name, cancellationToken);
            if (!exists)
            {
                var entries = z.ZoneMap.Select(e => new ZoneEntity.ZoneMapEntry
                {
                    Port = e.Port,
                    StartIdx = e.StartIdx,
                    EndIdx = e.EndIdx,
                    RgbStartIdx = e.RgbStartIdx
                }).ToList();
                var created = ZoneEntity.Create(z.Name, z.CtlrName, z.IsReadOnly, entries);
                if (created.IsFailure) { aggregate.Add(Result.Failure(created.Error)); continue; }
                created.Value.MarkSynced();
                aggregate.Add(await _zones.AddAsync(created.Value, cancellationToken));
            }
            else
            {
                var current = await _zones.GetByNameAsync(z.Name, cancellationToken);
                if (current.IsFailure) { aggregate.Add(Result.Failure(current.Error)); continue; }
                var entries = z.ZoneMap.Select(e => new ZoneEntity.ZoneMapEntry
                {
                    Port = e.Port,
                    StartIdx = e.StartIdx,
                    EndIdx = e.EndIdx,
                    RgbStartIdx = e.RgbStartIdx
                }).ToList();
                current.Value.Update(z.CtlrName, z.IsReadOnly, entries);
                current.Value.MarkSynced();
                aggregate.Add(await _zones.UpdateAsync(current.Value, cancellationToken));
            }
        }

        stopwatch.Stop();

        var finalResult = aggregate.Any(r => r.IsFailure)
            ? Result.Failure(ValidationError.FromResults(aggregate))
            : Result.Success();

        // Log audit entry
        await _auditLog.LogActionAsync(
            actionType: "SyncZones",
            sourceType: "System",
            sourceId: "SyncCommand",
            folderName: string.Empty,
            patternName: string.Empty,
            zoneNames: null,
            state: null,
            success: finalResult.IsSuccess,
            errorMessage: finalResult.IsFailure ? finalResult.Error.Description : string.Empty,
            requestPayload: new { Action = "SyncZones" },
            responsePayload: new { ZoneCount = res.Value.Count },
            durationMs: stopwatch.ElapsedMilliseconds,
            ct: cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        return finalResult;
    }
}

