using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Zone.Commands;

public sealed record DeleteZoneCommand(string Name) : IRequest<Result>, ITransactionalRequest;

