using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Schedule.Queries;

public sealed record GetTimelineTodayQuery(DateOnly? Date)
    : IRequest<Result<List<ScheduleTimelineItemDto>>>;

