using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Pattern.Commands;

public sealed record DeletePatternCommand(string FolderName, string Name) : IRequest<Result>, ITransactionalRequest;

