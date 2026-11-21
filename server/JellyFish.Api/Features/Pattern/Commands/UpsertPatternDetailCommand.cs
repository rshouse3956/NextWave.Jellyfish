using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Pattern.Commands;

public sealed record UpsertPatternDetailCommand(
    string FolderName,
    string Name,
    PatternDetailsDto Details
) : IRequest<Result<PatternDto>>, ITransactionalRequest;

