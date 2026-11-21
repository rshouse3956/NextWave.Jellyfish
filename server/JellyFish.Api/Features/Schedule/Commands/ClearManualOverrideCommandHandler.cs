using JellyFish.Api.Common;
using JellyFish.Api.Domain.Services;
using JellyFish.Api.Features.JellyfishController.Commands;
using JellyFish.Api.Features.Pattern;
using JellyFish.Api.Features.Pattern.Queries;
using JellyFish.Api.Infrastructure.Notifications;
using JellyFish.Api.Infrastructure.Repositories;
using JellyFish.Api.Infrastructure.Services.Scheduling;
using MediatR;
using Microsoft.AspNetCore.Components.Forms;

namespace JellyFish.Api.Features.Schedule.Commands;

public sealed class ClearManualOverrideCommandHandler : IRequestHandler<ClearManualOverrideCommand, Result>
{
    private readonly IManualOverrideService _override;
    private readonly IScheduleRepository _schedules;
    private readonly IScheduleNextEventService _nextSvc;
    private readonly IMediator _mediator;
    private readonly IStateChangeBroadcaster _events;

    public ClearManualOverrideCommandHandler(
        IManualOverrideService @override,
        IScheduleRepository schedules,
        IScheduleNextEventService nextSvc,
        IMediator mediator,
        IStateChangeBroadcaster events)
    {
        _override = @override;
        _schedules = schedules;
        _nextSvc = nextSvc;
        _mediator = mediator;
        _events = events;
    }

    public async Task<Result> Handle(ClearManualOverrideCommand request, CancellationToken cancellationToken)
    {
        _override.Clear();

        // Reconcile to desired schedule immediately
        var listRes = await _schedules.ListAsync(cancellationToken);
        if (listRes.IsFailure) return Result.Failure(listRes.Error);

        var sel = _nextSvc.SelectDesiredActive(listRes.Value, DateTime.UtcNow);
        if (sel.IsFailure) return Result.Failure(sel.Error);

        if (sel.Value is not null)
        {
            var s = sel.Value;
            //check to see if we need to send data.
            PatternDetailsDto? data = null;
            try
            {
                var patRes = await _mediator.Send(new GetPatternQuery(s.FolderName, s.PatternName), cancellationToken);
                if (!patRes.IsFailure && patRes.Value.SyncRequired)
                {
                    data = patRes.Value.Details;
                }
            }
            catch { /* ignore; fall back to null data */ }
            var run = await _mediator.Send(new RunPatternCommand(s.FolderName, s.PatternName, 1, s.ZoneNames, data, IsManual: false), cancellationToken);
            if (run.IsFailure) return Result.Failure(run.Error);
        }

        _events.Broadcast("jellyfish:state-changed");
        return Result.Success();
    }
}

