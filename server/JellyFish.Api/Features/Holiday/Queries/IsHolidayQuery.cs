using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Holiday.Queries;

public sealed record IsHolidayQuery(DateOnly Date) : IRequest<Result<IsHolidayDto>>;

