using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Pattern.Queries;

public sealed record GetPatternQuery(string FolderName, string Name) : IRequest<Result<PatternDto>>;

