using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Schedule.Queries;

public sealed record GetScheduleQuery(string Name) : IRequest<Result<ScheduleDto>>;

