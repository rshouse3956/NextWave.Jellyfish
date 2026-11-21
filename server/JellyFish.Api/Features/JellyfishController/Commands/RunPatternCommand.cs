using JellyFish.Api.Common;
using JellyFish.Api.Features.Pattern;
using MediatR;

namespace JellyFish.Api.Features.JellyfishController.Commands;

public sealed record RunPatternCommand(
    string FolderName,
    string Name,
    int State,
    IReadOnlyList<string> ZoneNames,
    PatternDetailsDto? Data,
    bool IsManual = true,
    string? ScheduleName = null
) : IRequest<Result>, ITransactionalRequest;
