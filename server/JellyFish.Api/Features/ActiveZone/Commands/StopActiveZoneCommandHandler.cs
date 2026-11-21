using JellyFish.Api.Common;
using JellyFish.Api.Features.JellyfishController.Commands;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.ActiveZone.Commands;

public sealed class StopActiveZoneCommandHandler : IRequestHandler<StopActiveZoneCommand, Result>
{
    private readonly IActiveZoneRepository _activeZones;
    private readonly IMediator _mediator;

    public StopActiveZoneCommandHandler(IActiveZoneRepository activeZones, IMediator mediator)
    {
        _activeZones = activeZones;
        _mediator = mediator;
    }

    public async Task<Result> Handle(StopActiveZoneCommand request, CancellationToken cancellationToken)
    {
        var get = await _activeZones.GetByZoneNameAsync(request.ZoneName, cancellationToken);
        if (get.IsFailure) return Result.Failure(get.Error);
        var az = get.Value;

        // Turn off the zone using the current pattern
        var off = await _mediator.Send(new RunPatternCommand(
            FolderName: az.FolderName,
            Name: az.PatternName,
            State: 0,
            ZoneNames: new[] { az.ZoneName },
            Data: null,
            IsManual: true,
            ScheduleName: null
        ), cancellationToken);
        if (off.IsFailure) return off;

        // Remove active record
        var del = await _activeZones.RemoveByZoneNameAsync(request.ZoneName, cancellationToken);
        return del;
    }
}

