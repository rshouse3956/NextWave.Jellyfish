using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Holiday.Queries;

public sealed record GetHolidayQuery(string Name) : IRequest<Result<HolidayDto>>;

