using JellyFish.Api.Domain.Services;
using JellyFish.Api.Features.JellyfishController.Commands;
using JellyFish.Api.Features.Pattern;
using JellyFish.Api.Features.Pattern.Queries;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Options;


namespace JellyFish.Api.Infrastructure.Services.Scheduling;

public sealed class SchedulePollingService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<SchedulePollingService> _logger;
    private readonly IOptions<ScheduleSettings> _options;

    public SchedulePollingService(IServiceProvider sp, ILogger<SchedulePollingService> logger, IOptions<ScheduleSettings> options)
    {
        _sp = sp;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = Math.Max(1, _options.Value.PollingMinutes);
        _logger.LogInformation("SchedulePollingService starting with interval {Minutes} minute(s)", minutes);

        var delay = TimeSpan.FromMinutes(minutes);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during schedule polling iteration");
            }

            try { await Task.Delay(delay, stoppingToken); }
            catch (TaskCanceledException) { /* ignore */ }
        }

        _logger.LogInformation("SchedulePollingService stopped");
    }

    private async Task PollOnce(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var schedulesRepo = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();
        var zonesRepo = scope.ServiceProvider.GetRequiredService<IZoneRepository>();
        var nextSvc = scope.ServiceProvider.GetRequiredService<IScheduleNextEventService>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var activeRepo = scope.ServiceProvider.GetRequiredService<IActiveZoneRepository>();

        var nowUtc = DateTime.UtcNow;

        var listRes = await schedulesRepo.ListAsync(ct);
        if (listRes.IsFailure)
        {
            _logger.LogWarning("Failed to list schedules: {Error}", listRes.Error.Description);
            return;
        }
        var schedules = listRes.Value;

        var zonesRes = await zonesRepo.ListAsync(ct);
        if (zonesRes.IsFailure || zonesRes.Value.Count == 0)
        {
            _logger.LogWarning("No zones available, skipping schedule processing");
            return;
        }
        var zoneNames = zonesRes.Value.Select(z => z.Name).ToList();

        // Manual override handling
        var overrideSvc = scope.ServiceProvider.GetRequiredService<IManualOverrideService>();
        var ov = overrideSvc.Current;
        if (ov is not null && ov.Mode == ManualOverrideMode.BlockOff)
        {
            // Off-block: if expired, clear; else exclude the blocked schedule
            if (ov.SuppressUntilUtc.HasValue && ov.SuppressUntilUtc.Value <= nowUtc)
            {
                overrideSvc.Clear();
            }
            else
            {
                schedules = schedules.Where(s => !string.Equals(s.Name, ov.ScheduleName, StringComparison.Ordinal)).ToList();
            }
        }

        // Build desired owner per zone (Manual ON trumps)
        var desired = new Dictionary<string, (string Folder, string Pattern, int Priority, string? ScheduleName)>();

        // Manual ON override: pin desired for listed zones
        if (ov is not null && ov.Mode == ManualOverrideMode.On)
        {
            foreach (var zn in ov.ZoneNames)
            {
                desired[zn] = (ov.FolderName, ov.PatternName, 0 /*highest priority*/, null);
            }
        }

        // Evaluate schedules that are active now
        foreach (var s in schedules)
        {
            var isActiveRes = nextSvc.IsActiveAt(s.OnTime, s.OnDay, s.OffTime, s.OffDay, nowUtc);
            if (isActiveRes.IsFailure || !isActiveRes.Value) continue;
            var pri = MapPriority(s.Priority);
            foreach (var zn in s.ZoneNames)
            {
                if (!desired.TryGetValue(zn, out var cur) || pri < cur.Priority)
                {
                    desired[zn] = (s.FolderName, s.PatternName, pri, s.Name);
                }
            }
        }

        // Apply desired state per zone
        foreach (var zn in zoneNames)
        {
            if (desired.TryGetValue(zn, out var d))
            {
                // Avoid chatty ON: only send when not already active with same folder/pattern
                var curAz = await activeRepo.GetByZoneNameAsync(zn, ct);
                if (curAz.IsSuccess && string.Equals(curAz.Value.FolderName, d.Folder, StringComparison.Ordinal) && string.Equals(curAz.Value.PatternName, d.Pattern, StringComparison.Ordinal))
                {
                    continue; // already active as desired
                }

                var onRes = await SendCommand(mediator, d.Folder, d.Pattern, 1, new List<string> { zn }, false, d.ScheduleName, ct);
                if (onRes.IsFailure)
                {
                    _logger.LogWarning("Failed to activate zone {Zone} -> {Folder}/{Pattern}: {Err}", zn, d.Folder, d.Pattern, onRes.Error.Description);
                }
            }
            else
            {
                // No desired owner: turn off if currently active
                var getAz = await activeRepo.GetByZoneNameAsync(zn, ct);
                if (getAz.IsSuccess)
                {
                    var az = getAz.Value;
                    var offRes = await SendCommand(mediator, az.FolderName, az.PatternName, 0, new List<string> { zn }, false, null, ct);
                    if (offRes.IsFailure)
                    {
                        _logger.LogWarning("Failed to turn off zone {Zone}: {Err}", zn, offRes.Error.Description);
                    }
                }
            }
        }
    }

    private static async Task<Common.Result> SendCommand(IMediator mediator, string folderName, string patternName, int state, List<string> zoneNames, bool isManual, string? scheduleName, CancellationToken ct)
    {
        PatternDetailsDto? data = null;
        try
        {
            var patRes = await mediator.Send(new GetPatternQuery(folderName, patternName), ct);
            if (!patRes.IsFailure && patRes.Value.SyncRequired)
            {
                data = patRes.Value.Details;
            }
        }
        catch { /* ignore; fall back to null data */ }

        var onRes = await mediator.Send(new RunPatternCommand(folderName, patternName, state, zoneNames, data, IsManual: isManual, ScheduleName: scheduleName), ct);
        return onRes;
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

