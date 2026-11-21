using System.Diagnostics;
using System.Text.Json;
using JellyFish.Api.Common;
using JellyFish.Api.Features.Pattern.Mappers;
using JellyFish.Api.Features.PatternAssistant.Errors;
using JellyFish.Api.Infrastructure.Persistence;
using JellyFish.Api.Infrastructure.Repositories;
using JellyFish.Api.Infrastructure.Services.Auditing;
using JellyFish.Api.Infrastructure.Services.PatternAssistant;
using MediatR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace JellyFish.Api.Features.PatternAssistant.Commands;

public sealed class GeneratePatternCommandHandler : IRequestHandler<GeneratePatternCommand, Result<GeneratedPatternDto>>
{
    /// <summary>One retry, with the parse error fed back, if the reply isn't a usable pattern.</summary>
    private const int MaxAttempts = 2;

    /// <summary>Audit log action type for a generate or refine request.</summary>
    public const string AuditActionType = "GeneratePattern";

    /// <summary>The model's raw reply is kept in the audit log only when it was unusable, cut to this length.</summary>
    public const int MaxLoggedReplyLength = 4000;

    private readonly IChatClient _chat;
    private readonly IPatternDetailRepository _details;
    private readonly IAuditLogService _auditLog;
    private readonly IUnitOfWork _uow;
    private readonly PatternAssistantOptions _options;
    private readonly ILogger<GeneratePatternCommandHandler> _logger;

    public GeneratePatternCommandHandler(
        IChatClient chat,
        IPatternDetailRepository details,
        IAuditLogService auditLog,
        IUnitOfWork uow,
        IOptions<PatternAssistantOptions> options,
        ILogger<GeneratePatternCommandHandler> logger)
    {
        _chat = chat;
        _details = details;
        _auditLog = auditLog;
        _uow = uow;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<GeneratedPatternDto>> Handle(GeneratePatternCommand request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return Result.Failure<GeneratedPatternDto>(PatternAssistantErrors.Disabled);

        var examples = PatternAssistantPrompt.SelectExamples(request.Prompt, await LoadExamplesAsync(cancellationToken), _options.MaxExamples);
        var stopwatch = Stopwatch.StartNew();
        var outcome = await GenerateAsync(request, examples, stopwatch, cancellationToken);
        await AuditAsync(request, examples, outcome, stopwatch.ElapsedMilliseconds, cancellationToken);
        return outcome.Result;
    }

    /// <summary>What a generate attempt produced, for the response and the audit log.</summary>
    private sealed record Outcome(Result<GeneratedPatternDto> Result, int Attempts, string? LastReply = null);

    private async Task<Outcome> GenerateAsync(
        GeneratePatternCommand request, IReadOnlyList<PatternExample> examples, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var messages = PatternAssistantPrompt.Build(request, examples);
        var chatOptions = new ChatOptions
        {
            ModelId = _options.Model,
            Temperature = _options.Temperature,
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                AssistantPatternSchema.Schema, "jellyfish_pattern", "A Jellyfish lighting pattern")
        };

        string? lastReply = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ChatResponse response;
            try
            {
                response = await _chat.GetResponseAsync(messages, chatOptions, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Pattern assistant timed out after {Seconds}s (model {Model})", _options.TimeoutSeconds, _options.Model);
                return new(Result.Failure<GeneratedPatternDto>(PatternAssistantErrors.Timeout(_options.TimeoutSeconds)), attempt, lastReply);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is null)
            {
                _logger.LogWarning(ex, "Pattern assistant could not reach Ollama at {BaseUrl}", _options.BaseUrl);
                return new(Result.Failure<GeneratedPatternDto>(PatternAssistantErrors.Unavailable(_options.BaseUrl)), attempt, lastReply);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // e.g. the model isn't pulled (Ollama answers 404 "model not found")
                _logger.LogWarning(ex, "Pattern assistant model call failed (model {Model})", _options.Model);
                return new(Result.Failure<GeneratedPatternDto>(PatternAssistantErrors.ModelError(ex.Message)), attempt, lastReply);
            }

            var text = response.Text;
            lastReply = text;
            if (TryParse(text, out var pattern, out var parseError))
            {
                var generated = PatternDetailsSanitizer.ApplyRequestRules(
                    PatternDetailsSanitizer.ToGenerated(pattern!),
                    [request.Prompt, .. request.History ?? []],
                    request.CurrentDetails);
                _logger.LogInformation(
                    "Pattern assistant generated '{Name}' with {Model} in {Ms} ms ({Attempts} attempt(s), {Examples} examples)",
                    generated.SuggestedName, _options.Model, stopwatch.ElapsedMilliseconds, attempt, examples.Count);
                return new(Result.Success(generated), attempt);
            }

            _logger.LogWarning("Pattern assistant reply unusable (attempt {Attempt}): {Error}", attempt, parseError);
            messages.Add(new ChatMessage(ChatRole.Assistant, text));
            messages.Add(new ChatMessage(ChatRole.User,
                $"That reply could not be used ({parseError}). Reply again with only the JSON pattern."));
        }

        return new(Result.Failure<GeneratedPatternDto>(PatternAssistantErrors.InvalidResponse), MaxAttempts, lastReply);
    }

    /// <summary>
    /// One audit entry per request: the prompt, model and examples, then the pattern or, on failure, the
    /// model's last raw reply. Never fails the request.
    /// </summary>
    private async Task AuditAsync(
        GeneratePatternCommand request, IReadOnlyList<PatternExample> examples, Outcome outcome, long durationMs, CancellationToken ct)
    {
        try
        {
            var result = outcome.Result;
            await _auditLog.LogActionAsync(
                actionType: AuditActionType,
                sourceType: "Manual",
                sourceId: "User",
                folderName: string.Empty,
                patternName: result.IsSuccess ? result.Value.SuggestedName : request.CurrentName ?? string.Empty,
                zoneNames: null,
                state: null,
                success: result.IsSuccess,
                errorMessage: result.IsFailure ? result.Error.Description : string.Empty,
                requestPayload: new
                {
                    request.Prompt,
                    Refine = request.CurrentDetails is not null,
                    request.CurrentName,
                    request.History,
                    _options.Model,
                    Examples = examples.Select(e => $"{e.FolderName}/{e.Name}").ToList()
                },
                responsePayload: result.IsSuccess
                    ? result.Value
                    : new { outcome.Attempts, LastReply = Truncate(outcome.LastReply) },
                durationMs: durationMs,
                ct: ct);
            await _uow.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not write the pattern assistant audit log entry");
        }
    }

    private static string? Truncate(string? text) =>
        text is { Length: > MaxLoggedReplyLength } ? text[..MaxLoggedReplyLength] + "…" : text;

    private async Task<IReadOnlyList<PatternExample>> LoadExamplesAsync(CancellationToken ct)
    {
        if (_options.MaxExamples <= 0) return [];
        try
        {
            var all = await _details.ListAsync(ct);
            return all
                .Where(d => d.Details?.RunData is not null && d.Details.Colors is not null)
                // Sync from the controller marks details synced (false); app-created details are true or unset.
                .Select(d => new PatternExample(d.FolderName, d.Name, PatternMapper.ToDetailsDto(d.Details), FromController: d.SyncRequired == false))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Examples only improve results; generate without them rather than fail.
            _logger.LogWarning(ex, "Could not load pattern examples for the assistant");
            return [];
        }
    }

    internal static bool TryParse(string? text, out AssistantPattern? pattern, out string? error)
    {
        pattern = null;
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "the reply was empty";
            return false;
        }

        try
        {
            pattern = JsonSerializer.Deserialize<AssistantPattern>(ExtractJson(text), AssistantPatternSchema.JsonOptions);
        }
        catch (JsonException ex)
        {
            error = $"invalid JSON: {ex.Message}";
            return false;
        }

        if (pattern?.Colors is not { Count: > 0 })
        {
            error = "the pattern has no colors";
            pattern = null;
            return false;
        }
        return true;
    }

    /// <summary>Structured output should be bare JSON, but tolerate a ```json fence or text around the object.</summary>
    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }
}
