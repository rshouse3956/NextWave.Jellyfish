using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Services.Jellyfish.Models;
using MediatR;

namespace JellyFish.Api.Features.JellyfishController.Queries;

public sealed record GetFoldersQuery() : IRequest<Result<IReadOnlyList<ControllerFolderDto>>>;
