using JellyFish.Api.Common;

namespace JellyFish.Api.Features.JellyfishController.Errors;

public static class JellyfishControllerErrors
{
    public static Error ConnectionFailed() => Error.Problem("Failed to communicate with Jellyfish controller");
    public static Error InvalidResponse(string detail) => Error.Problem($"Invalid controller response: {detail}");
    public static Error RunPatternNotImplemented() => Error.Problem("RunPattern not yet implemented; awaiting command spec");
}
