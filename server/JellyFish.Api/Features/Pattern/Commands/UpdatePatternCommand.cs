using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Pattern.Commands;

public sealed record UpdatePatternCommand(string FolderName, string Name, bool IsReadOnly) : IRequest<Result<PatternDto>>, ITransactionalRequest;

