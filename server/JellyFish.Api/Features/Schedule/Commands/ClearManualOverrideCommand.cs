using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Schedule.Commands;

public sealed record ClearManualOverrideCommand() : IRequest<Result>;

