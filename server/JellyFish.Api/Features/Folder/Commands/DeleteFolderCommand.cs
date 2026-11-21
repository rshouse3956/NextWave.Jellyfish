using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Folder.Commands;

public sealed record DeleteFolderCommand(string Name) : IRequest<Result>, ITransactionalRequest;

