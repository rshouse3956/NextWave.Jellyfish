using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Health.Queries;

public readonly record struct GetHealthQuery : IRequest<Result<HealthDto>>;

