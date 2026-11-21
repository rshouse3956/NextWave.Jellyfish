using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using MediatR;

namespace JellyFish.Api.Features.JellyfishController.Queries;

public sealed record GetPatternDetailQuery(string FolderName, string Name) : IRequest<Result<PatternDetails>>;
