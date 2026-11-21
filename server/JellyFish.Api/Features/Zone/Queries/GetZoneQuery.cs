using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Zone.Queries;

public sealed record GetZoneQuery(string Name) : IRequest<Result<ZoneDto>>;

