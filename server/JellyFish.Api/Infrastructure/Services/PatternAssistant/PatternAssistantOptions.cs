namespace JellyFish.Api.Infrastructure.Services.PatternAssistant;

/// <summary>
/// Settings for the AI pattern assistant (section "PatternAssistant"; env vars PatternAssistant__*).
/// </summary>
public sealed class PatternAssistantOptions
{
    public const string SectionName = "PatternAssistant";
    public const string HttpClientName = "Ollama";

    /// <summary>Off by default: the assistant endpoints return 404 until this is turned on.</summary>
    public bool Enabled { get; set; }

    /// <summary>Ollama's base URL, e.g. http://ollama:11434 (compose) or http://localhost:11434.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Ollama model name, e.g. qwen2.5:7b. It must already be pulled on the Ollama server.</summary>
    public string Model { get; set; } = "qwen2.5:7b";

    /// <summary>How long to wait for one model reply. CPU-only inference can take 20+ seconds.</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Low for consistency: the same request should give the same pattern.</summary>
    public float Temperature { get; set; } = 0.2f;

    /// <summary>How many stored patterns to show the model as examples.</summary>
    public int MaxExamples { get; set; } = 4;
}
