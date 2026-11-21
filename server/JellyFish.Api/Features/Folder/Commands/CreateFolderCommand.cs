using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Folder.Commands;

public sealed record CreateFolderCommand(string Name, bool IsReadOnly) : IRequest<Result<FolderDto>>, ITransactionalRequest;

