using JellyFish.Api.Common;

namespace JellyFish.Api.Features.Folder.Errors;

public static class FolderErrors
{
    public static Error NameRequired() => Error.Validation("Folder name is required");
    public static Error AlreadyExists(string name) => Error.Conflict($"Folder '{name}' already exists");
    public static Error NotFound(string name) => Error.NotFound($"Folder '{name}' not found");
    public static Error ReadOnly(string name) => Error.Conflict($"Folder '{name}' is read-only");
}

