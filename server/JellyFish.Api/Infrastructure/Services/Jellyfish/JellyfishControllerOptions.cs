using System;

namespace JellyFish.Api.Infrastructure.Services.Jellyfish;

public sealed class JellyfishControllerOptions
{
    public string WebSocketUri { get; set; } = string.Empty;
    public int RequestTimeoutMs { get; set; } = 3000; // default 3s; controller may hang on error
}
