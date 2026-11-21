using JellyFish.Api.Common;
using JellyFish.Api.Features.Schedule.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Schedule.Queries;

public sealed class ListSchedulesQueryHandler : IRequestHandler<ListSchedulesQuery, Result<List<ScheduleDto>>>
{
    private readonly IScheduleRepository _schedules;

    public ListSchedulesQueryHandler(IScheduleRepository schedules)
    {
        _schedules = schedules;
    }

    public async Task<Result<List<ScheduleDto>>> Handle(ListSchedulesQuery request, CancellationToken cancellationToken)
    {
        var res = await _schedules.ListAsync(cancellationToken);
        if (res.IsFailure) return Result.Failure<List<ScheduleDto>>(res.Error);
        return Result.Success(res.Value.Select(ScheduleMapper.ToDto).ToList());
    }
}

