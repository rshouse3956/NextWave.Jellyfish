using JellyFish.Api.Common;
using JellyFish.Api.Domain.Services;
using JellyFish.Api.Features.Schedule.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Schedule.Commands;

public sealed class UpdateScheduleCommandHandler : IRequestHandler<UpdateScheduleCommand, Result<ScheduleDto>>
{
    private readonly IScheduleRepository _schedules;
    private readonly IPatternRepository _patterns;
    private readonly IScheduleNextEventService _nextEventSvc;

    public UpdateScheduleCommandHandler(IScheduleRepository schedules, IPatternRepository patterns, IScheduleNextEventService nextEventSvc)
    {
        _schedules = schedules;
        _patterns = patterns;
        _nextEventSvc = nextEventSvc;
    }

    public async Task<Result<ScheduleDto>> Handle(UpdateScheduleCommand request, CancellationToken cancellationToken)
    {
        var get = await _schedules.GetByNameAsync(request.Name, cancellationToken);
        if (get.IsFailure) return Result.Failure<ScheduleDto>(get.Error);

        if (!await _patterns.ExistsAsync(request.FolderName, request.PatternName, cancellationToken))
            return Result.Failure<ScheduleDto>(Error.NotFound($"Pattern '{request.PatternName}' in folder '{request.FolderName}' not found"));

        var nextEventRes = _nextEventSvc.ComputeNextEventUtc(request.OnTime, request.OnDay, request.OffTime, request.OffDay);
        if (nextEventRes.IsFailure) return Result.Failure<ScheduleDto>(nextEventRes.Error);

        var entity = get.Value;
        entity.Update(
            request.FolderName,
            request.PatternName,
            request.Priority,
            request.OnTime,
            request.OnDay,
            request.OffTime,
            request.OffDay,
            request.ZoneNames,
            nextEventRes.Value.NextUtc);
        entity.SetNextEvent(nextEventRes.Value.NextUtc, nextEventRes.Value.Kind);

        var save = await _schedules.UpdateAsync(entity, cancellationToken);
        if (save.IsFailure) return Result.Failure<ScheduleDto>(save.Error);

        return Result.Success(ScheduleMapper.ToDto(entity));
    }
}

