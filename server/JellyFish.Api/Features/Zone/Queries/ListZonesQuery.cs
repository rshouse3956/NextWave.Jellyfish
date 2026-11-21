using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Zone.Queries;

public sealed record ListZonesQuery() : IRequest<Result<List<ZoneDto>>>;

