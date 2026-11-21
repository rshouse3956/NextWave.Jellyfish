using JellyFish.Api.Common;

namespace JellyFish.Api.Features.Zone.Errors;

public static class ZoneErrors
{
    public static Error NameRequired() => Error.Validation("Zone name is required");
    public static Error AlreadyExists(string name) => Error.Conflict($"Zone '{name}' already exists");
    public static Error NotFound(string name) => Error.NotFound($"Zone '{name}' not found");
    public static Error ReadOnly(string name) => Error.Conflict($"Zone '{name}' is read-only");
}

