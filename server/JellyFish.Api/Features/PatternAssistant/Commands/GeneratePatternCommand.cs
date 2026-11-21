using JellyFish.Api.Common;
using JellyFish.Api.Features.Pattern;
using MediatR;

namespace JellyFish.Api.Features.PatternAssistant.Commands;

/// <summary>
/// Generate a pattern from a description, or refine one: pass the current pattern in <see cref="CurrentDetails"/>
/// and the change in <see cref="Prompt"/> (e.g. "slower, add white"). Nothing is run or saved.
/// </summary>
public sealed record GeneratePatternCommand(
    string Prompt,
    PatternDetailsDto? CurrentDetails = null,
    string? CurrentName = null,
    IReadOnlyList<string>? History = null
) : IRequest<Result<GeneratedPatternDto>>;
