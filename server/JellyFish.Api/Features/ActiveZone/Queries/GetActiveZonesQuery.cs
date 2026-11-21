using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.ActiveZone.Queries;

public sealed record GetActiveZonesQuery() : IRequest<Result<List<ActiveZoneDto>>>;

