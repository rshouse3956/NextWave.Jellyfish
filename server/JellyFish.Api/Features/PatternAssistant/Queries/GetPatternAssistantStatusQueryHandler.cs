using System.Text.Json;
using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Services.PatternAssistant;
using MediatR;
using Microsoft.Extensions.Options;

namespace JellyFish.Api.Features.PatternAssistant.Queries;

/// <summary>
/// Reports whether the assistant is enabled, Ollama answers, and the configured model is pulled.
/// Always succeeds (200): problems are described in the DTO for the UI to show.
/// </summary>
public sealed class GetPatternAssistantStatusQueryHandler : IRequestHandler<GetPatternAssistantStatusQuery, Result<PatternAssistantStatusDto>>
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    private readonly IHttpClientFactory _httpFactory;
    private readonly PatternAssistantOptions _options;
    private readonly ILogger<GetPatternAssistantStatusQueryHandler> _logger;

    public GetPatternAssistantStatusQueryHandler(
        IHttpClientFactory httpFactory,
        IOptions<PatternAssistantOptions> options,
        ILogger<GetPatternAssistantStatusQueryHandler> logger)
    {
        _httpFactory = httpFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<PatternAssistantStatusDto>> Handle(GetPatternAssistantStatusQuery request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Status(false, false, "The pattern assistant is not enabled");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(ProbeTimeout);
            var http = _httpFactory.CreateClient(PatternAssistantOptions.HttpClientName);
            using var resp = await http.GetAsync("api/tags", cts.Token);
            if (!resp.IsSuccessStatusCode)
                return Status(false, false, $"Ollama at {_options.BaseUrl} answered {(int)resp.StatusCode}");

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cts.Token));
            var installed = ModelNames(doc).Any(n => SameModel(n, _options.Model));
            return Status(true, installed, installed
                ? null
                : $"Model '{_options.Model}' is not pulled on the Ollama server (run: ollama pull {_options.Model})");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException
                                   && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Pattern assistant status: Ollama at {BaseUrl} unreachable ({Error})", _options.BaseUrl, ex.Message);
            return Status(false, false, $"Could not reach the Ollama server at {_options.BaseUrl}");
        }
    }

    private Result<PatternAssistantStatusDto> Status(bool reachable, bool installed, string? message) =>
        Result.Success(new PatternAssistantStatusDto(_options.Enabled, _options.Model, reachable, installed, message));

    private static IEnumerable<string> ModelNames(JsonDocument doc) =>
        doc.RootElement.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array
            ? models.EnumerateArray()
                .Select(m => m.TryGetProperty("name", out var n) ? n.GetString() : null)
                .OfType<string>()
            : [];

    /// <summary>Ollama treats a name without a tag as ":latest".</summary>
    internal static bool SameModel(string installed, string configured)
    {
        static string Normalize(string n) => n.Contains(':') ? n : n + ":latest";
        return string.Equals(Normalize(installed.Trim()), Normalize(configured.Trim()), StringComparison.OrdinalIgnoreCase);
    }
}
