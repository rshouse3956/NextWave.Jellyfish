using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Pattern.Queries;

public sealed record ListPatternsByFolderQuery(string FolderName) : IRequest<Result<List<PatternDto>>>;

