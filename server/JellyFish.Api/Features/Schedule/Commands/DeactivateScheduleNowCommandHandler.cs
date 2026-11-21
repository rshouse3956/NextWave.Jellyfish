using JellyFish.Api.Common;
using JellyFish.Api.Domain.Services;
using JellyFish.Api.Features.JellyfishController.Commands;
using JellyFish.Api.Features.Pattern;
using JellyFish.Api.Features.Pattern.Queries;

using JellyFish.Api.Infrastructure.Repositories;
using JellyFish.Api.Infrastructure.Services.Scheduling;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JellyFish.Api.Features.Schedule.Commands;

public sealed class DeactivateScheduleNowCommandHandler : IRequestHandler<DeactivateScheduleNowCommand, Result>
{
    private readonly IScheduleRepository _schedules;
    private readonly IZoneRepository _zones;
    private readonly IScheduleNextEventService _nextSvc;
    private readonly IManualOverrideService _override;
    private readonly IMediator _mediator;
    private readonly ILogger<DeactivateScheduleNowCommandHandler> _logger;

    public DeactivateScheduleNowCommandHandler(
        IScheduleRepository schedules,
        IZoneRepository zones,
        IScheduleNextEventService nextSvc,
        IManualOverrideService @override,
        IMediator mediator,
        ILogger<DeactivateScheduleNowCommandHandler> logger)
    {
        _schedules = schedules;
        _zones = zones;
        _nextSvc = nextSvc;
        _override = @override;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result> Handle(DeactivateScheduleNowCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure(Error.Validation("Name is required"));

        var getRes = await _schedules.GetByNameAsync(request.Name, cancellationToken);
        if (getRes.IsFailure) return Result.Failure(getRes.Error);
        var s = getRes.Value;

        var now = DateTime.UtcNow;
        var isActiveRes = _nextSvc.IsActiveAt(s.OnTime, s.OnDay, s.OffTime, s.OffDay, now);
        if (isActiveRes.IsFailure) return Result.Failure(isActiveRes.Error);

        // Always set an off-block to prevent immediate re-activation.
        // Prefer blocking until the next Off if determinable; otherwise use a short fallback window.
        var nextRes = _nextSvc.ComputeNextEventUtc(s.OnTime, s.OnDay, s.OffTime, s.OffDay);
        DateTime? until = null;
        if (!nextRes.IsFailure && nextRes.Value.NextUtc is not null && string.Equals(nextRes.Value.Kind, "Off", StringComparison.OrdinalIgnoreCase))
        {
            until = nextRes.Value.NextUtc.Value;
        }
        else
        {
            until = now.AddMinutes(5);
            _logger.LogWarning("Deactivate-now: unable to determine current window end for {Name}; applying short off-block until {Until}", s.Name, until);
        }
        _override.SetBlockOff(s.Name, s.FolderName, s.PatternName, until.Value);

        // Turn off now (only the schedule's zones)
        // If the Pattern requires sync, include its details so the controller accepts the command
        PatternDetailsDto? data = null;
        var patRes = await _mediator.Send(new GetPatternQuery(s.FolderName, s.PatternName), cancellationToken);
        if (!patRes.IsFailure && patRes.Value.SyncRequired && patRes.Value.Details is not null)
        {
            data = patRes.Value.Details;
        }

        var offRes = await _mediator.Send(new RunPatternCommand(s.FolderName, s.PatternName, 0, s.ZoneNames, data, IsManual: false), cancellationToken);
        if (offRes.IsFailure)
        {
            _logger.LogWarning("Failed to turn off schedule {Name} during deactivate-now: {Err}", s.Name, offRes.Error.Description);
        }

        return Result.Success();
    }
}

