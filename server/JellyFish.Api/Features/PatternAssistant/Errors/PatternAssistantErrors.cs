using JellyFish.Api.Common;

namespace JellyFish.Api.Features.PatternAssistant.Errors;

public static class PatternAssistantErrors
{
    // NotFound: with the feature off, the endpoints behave as if they didn't exist (404).
    public static readonly Error Disabled = new(
        "PatternAssistant.Disabled", "The pattern assistant is not enabled", ErrorType.NotFound);

    public static Error Unavailable(string baseUrl) => new(
        "PatternAssistant.Unavailable", $"Could not reach the Ollama server at {baseUrl}", ErrorType.Problem);

    public static Error Timeout(int seconds) => new(
        "PatternAssistant.Timeout", $"The model did not answer within {seconds} seconds", ErrorType.Problem);

    public static Error ModelError(string message) => new(
        "PatternAssistant.ModelError", $"The model server returned an error: {message}", ErrorType.Problem);

    public static readonly Error InvalidResponse = new(
        "PatternAssistant.InvalidResponse", "The model did not return a usable pattern; try rewording the request", ErrorType.Problem);
}
