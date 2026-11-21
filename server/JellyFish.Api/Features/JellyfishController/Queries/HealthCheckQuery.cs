using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.JellyfishController.Queries;

public sealed record HealthCheckQuery : IRequest<Result>;

