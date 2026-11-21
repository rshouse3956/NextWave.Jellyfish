using JellyFish.Api.Common;
using JellyFish.Api.Features.JellyfishController.Errors;
using JellyFish.Api.Features.Pattern.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using JellyFish.Api.Infrastructure.Services.Jellyfish;
using JellyFish.Api.Infrastructure.Services.Scheduling;
using JellyFish.Api.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

using JellyFish.Api.Infrastructure.Notifications;
using JellyFish.Api.Infrastructure.Services.Auditing;
using JellyFish.Api.Infrastructure.Persistence;

namespace JellyFish.Api.Features.JellyfishController.Commands;

public sealed class RunPatternCommandHandler : IRequestHandler<RunPatternCommand, Result>
{
    private readonly IJellyfishControllerClient _client;
    private readonly IManualOverrideService _override;
    private readonly IScheduleRepository _schedules;
    private readonly IZoneRepository _zones;
    private readonly IScheduleNextEventService _nextSvc;
    private readonly ILogger<RunPatternCommandHandler> _logger;
    private readonly IStateChangeBroadcaster _events;
    private readonly IActiveZoneRepository _activeZones;
    private readonly IAuditLogService _auditLog;
    private readonly IUnitOfWork _uow;

    public RunPatternCommandHandler(
        IJellyfishControllerClient client,
        IManualOverrideService @override,
        IScheduleRepository schedules,
        IZoneRepository zones,
        IScheduleNextEventService nextSvc,
        ILogger<RunPatternCommandHandler> logger,
        IStateChangeBroadcaster events,
        IActiveZoneRepository activeZones,
        IAuditLogService auditLog,
        IUnitOfWork uow)
    {
        _client = client;
        _override = @override;
        _schedules = schedules;
        _zones = zones;
        _nextSvc = nextSvc;
        _logger = logger;
        _events = events;
        _activeZones = activeZones;
        _auditLog = auditLog;
        _uow = uow;
    }

    public async Task<Result> Handle(RunPatternCommand request, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var domainDetails = request.Data is null ? null : PatternMapper.FromDetailsDto(request.Data);
        var res = await _client.RunPatternAsync(
            folderName: request.FolderName,
            name: request.Name,
            state: request.State,
            zoneNames: request.ZoneNames,
            data: domainDetails,
            ct: cancellationToken);

        stopwatch.Stop();

        // Log audit entry
        var sourceType = request.IsManual ? "Manual" : "Schedule";
        var sourceId = request.IsManual ? (request.ScheduleName ?? "User") : (request.ScheduleName ?? string.Empty);

        await _auditLog.LogActionAsync(
            actionType: "RunPattern",
            sourceType: sourceType,
            sourceId: sourceId,
            folderName: request.FolderName,
            patternName: request.Name,
            zoneNames: request.ZoneNames,
            state: request.State,
            success: res.IsSuccess,
            errorMessage: res.IsFailure ? res.Error.Description : string.Empty,
            requestPayload: new { request.FolderName, request.Name, request.State, request.ZoneNames, HasData = request.Data != null },
            responsePayload: null,
            durationMs: stopwatch.ElapsedMilliseconds,
            ct: cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);


        if (res.IsSuccess)
        {
            // Maintain ActiveZone records
            if (request.State == 1)
            {
                // Determine priority
                int priority = request.IsManual ? 0 : 5; // Manual trumps schedules; default schedule priority is Normal (5)
                if (!request.IsManual && !string.IsNullOrWhiteSpace(request.ScheduleName))
                {
                    var s = await _schedules.GetByNameAsync(request.ScheduleName, cancellationToken);
                    if (s.IsSuccess)
                    {
                        priority = MapPriority(s.Value.Priority);
                    }
                }

                foreach (var zn in request.ZoneNames)
                {
                    var created = Domain.ActiveZone.Create(
                        zoneName: zn,
                        sourceType: sourceType,
                        sourceId: sourceId ?? string.Empty,
                        priority: priority,
                        folderName: request.FolderName,
                        patternName: request.Name,
                        activatedAtUtc: DateTime.UtcNow,
                        expectedEndUtc: null);
                    if (created.IsSuccess)
                    {
                        await _activeZones.UpsertAsync(created.Value, cancellationToken);
                    }
                }
            }
            else
            {
                // Off: remove active entries for these zones
                foreach (var zn in request.ZoneNames)
                {
                    await _activeZones.RemoveByZoneNameAsync(zn, cancellationToken);
                }
            }

            // Notify clients (SSE) that controller state changed
            _events.Broadcast("jellyfish:state-changed");
        }

        if (res.IsSuccess && request.IsManual)
        {
            if (request.State == 1)
            {
                var scheduleName = string.IsNullOrWhiteSpace(request.ScheduleName) ? request.Name : request.ScheduleName;
                _override.SetOn(scheduleName, request.FolderName, request.Name, request.ZoneNames);
            }
            else
            {
                // Manual Off: clear override and immediately reconcile to desired scheduled state
                _override.Clear();
                try
                {
                    var listRes = await _schedules.ListAsync(cancellationToken);
                    if (!listRes.IsFailure && listRes.Value.Count > 0)
                    {
                        var sel = _nextSvc.SelectDesiredActive(listRes.Value, DateTime.UtcNow);
                        if (sel.IsSuccess && sel.Value is not null)
                        {
                            var onRes = await _client.RunPatternAsync(
                                folderName: sel.Value.FolderName,
                                name: sel.Value.PatternName,
                                state: 1,
                                zoneNames: sel.Value.ZoneNames,
                                data: null,
                                ct: cancellationToken);
                            if (onRes.IsFailure)
                            {
                                _logger.LogWarning("Immediate reconcile failed to activate schedule {Folder}/{Name}: {Err}", sel.Value.FolderName, sel.Value.Name, onRes.Error.Description);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during immediate schedule reconcile after manual off");
                }
            }
        }

        return res;
    }

    private static int MapPriority(string priority)
    {
        if (string.IsNullOrWhiteSpace(priority)) return 5;
        return priority.Trim().ToLowerInvariant() switch
        {
            "high" => 1,
            "normal" => 5,
            "low" => 9,
            _ => 5
        };
    }
}
