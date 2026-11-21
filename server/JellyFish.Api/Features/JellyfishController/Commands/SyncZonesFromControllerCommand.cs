using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.JellyfishController.Commands;

public sealed record SyncZonesFromControllerCommand : IRequest<Result>, ITransactionalRequest;

