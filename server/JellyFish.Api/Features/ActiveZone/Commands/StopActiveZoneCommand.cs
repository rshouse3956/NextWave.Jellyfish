using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.ActiveZone.Commands;

public sealed record StopActiveZoneCommand(string ZoneName) : IRequest<Result>, ITransactionalRequest;

