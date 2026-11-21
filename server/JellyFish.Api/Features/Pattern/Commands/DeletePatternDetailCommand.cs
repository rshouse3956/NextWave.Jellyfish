using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Pattern.Commands;

public sealed record DeletePatternDetailCommand(
    string FolderName,
    string Name
) : IRequest<Result>, ITransactionalRequest;

