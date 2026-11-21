using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Schedule.Queries;

public sealed record GetManualOverrideQuery() : IRequest<Result<ManualOverrideDto?>>;

