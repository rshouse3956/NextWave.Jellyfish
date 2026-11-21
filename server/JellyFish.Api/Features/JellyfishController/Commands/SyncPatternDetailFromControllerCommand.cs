using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.JellyfishController.Commands;

public sealed record SyncPatternDetailFromControllerCommand(string FolderName, string Name) : IRequest<Result>, ITransactionalRequest;

