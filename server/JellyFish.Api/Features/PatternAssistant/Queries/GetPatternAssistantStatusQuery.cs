using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.PatternAssistant.Queries;

public sealed record GetPatternAssistantStatusQuery : IRequest<Result<PatternAssistantStatusDto>>;
