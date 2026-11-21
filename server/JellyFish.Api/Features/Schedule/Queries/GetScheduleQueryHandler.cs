using JellyFish.Api.Common;
using JellyFish.Api.Features.Schedule.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Schedule.Queries;

public sealed class GetScheduleQueryHandler : IRequestHandler<GetScheduleQuery, Result<ScheduleDto>>
{
    private readonly IScheduleRepository _schedules;

    public GetScheduleQueryHandler(IScheduleRepository schedules)
    {
        _schedules = schedules;
    }

    public async Task<Result<ScheduleDto>> Handle(GetScheduleQuery request, CancellationToken cancellationToken)
    {
        var res = await _schedules.GetByNameAsync(request.Name, cancellationToken);
        return res.IsSuccess ? Result.Success(ScheduleMapper.ToDto(res.Value)) : Result.Failure<ScheduleDto>(res.Error);
    }
}

