using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Folder.Queries;

public sealed record GetFolderQuery(string Name) : IRequest<Result<FolderDto>>;

