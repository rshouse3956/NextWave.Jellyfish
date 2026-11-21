using JellyFish.Api.Common;
using JellyFish.Api.Domain.Services;
using JellyFish.Api.Features.Schedule.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Schedule.Commands;

public sealed class CreateScheduleCommandHandler : IRequestHandler<CreateScheduleCommand, Result<ScheduleDto>>
{
    private readonly IScheduleRepository _schedules;
    private readonly IPatternRepository _patterns;
    private readonly IScheduleNextEventService _nextEventSvc;

    public CreateScheduleCommandHandler(IScheduleRepository schedules, IPatternRepository patterns, IScheduleNextEventService nextEventSvc)
    {
        _schedules = schedules;
        _patterns = patterns;
        _nextEventSvc = nextEventSvc;
    }

    public async Task<Result<ScheduleDto>> Handle(CreateScheduleCommand request, CancellationToken cancellationToken)
    {
        if (await _schedules.ExistsByNameAsync(request.Name, cancellationToken))
            return Result.Failure<ScheduleDto>(Error.Conflict($"Schedule '{request.Name}' already exists"));

        if (!await _patterns.ExistsAsync(request.FolderName, request.PatternName, cancellationToken))
            return Result.Failure<ScheduleDto>(Error.NotFound($"Pattern '{request.PatternName}' in folder '{request.FolderName}' not found"));

        var nextEventRes = _nextEventSvc.ComputeNextEventUtc(request.OnTime, request.OnDay, request.OffTime, request.OffDay);
        if (nextEventRes.IsFailure) return Result.Failure<ScheduleDto>(nextEventRes.Error);

        var created = JellyFish.Api.Domain.Schedule.Create(
            request.Name,
            request.FolderName,
            request.PatternName,
            request.Priority,
            request.OnTime,
            request.OnDay,
            request.OffTime,
            request.OffDay,
            request.ZoneNames,
            nextEventRes.Value.NextUtc);
        if (created.IsFailure) return Result.Failure<ScheduleDto>(created.Error);
        created.Value.SetNextEvent(nextEventRes.Value.NextUtc, nextEventRes.Value.Kind);

        var add = await _schedules.AddAsync(created.Value, cancellationToken);
        if (add.IsFailure) return Result.Failure<ScheduleDto>(add.Error);

        return Result.Success(ScheduleMapper.ToDto(created.Value));
    }
}

