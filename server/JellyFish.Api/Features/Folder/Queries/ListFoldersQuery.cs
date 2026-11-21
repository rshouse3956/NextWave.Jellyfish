using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Folder.Queries;

public sealed record ListFoldersQuery() : IRequest<Result<List<FolderDto>>>;

