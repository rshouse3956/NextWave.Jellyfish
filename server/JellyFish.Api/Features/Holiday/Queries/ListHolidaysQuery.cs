using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Holiday.Queries;

public sealed record ListHolidaysQuery() : IRequest<Result<List<HolidayDto>>>;

