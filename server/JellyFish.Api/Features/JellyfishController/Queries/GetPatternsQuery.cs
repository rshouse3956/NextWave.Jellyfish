using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Services.Jellyfish.Models;
using MediatR;

namespace JellyFish.Api.Features.JellyfishController.Queries;

public sealed record GetPatternsQuery() : IRequest<Result<IReadOnlyList<ControllerPatternListItemDto>>>;
