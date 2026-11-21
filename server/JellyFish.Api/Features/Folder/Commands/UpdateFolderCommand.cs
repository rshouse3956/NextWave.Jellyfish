using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Folder.Commands;

public sealed record UpdateFolderCommand(string Name, bool IsReadOnly) : IRequest<Result<FolderDto>>, ITransactionalRequest;

