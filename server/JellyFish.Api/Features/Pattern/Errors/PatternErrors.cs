using JellyFish.Api.Common;

namespace JellyFish.Api.Features.Pattern.Errors;

public static class PatternErrors
{
    public static Error NameRequired() => Error.Validation("Pattern name is required");
    public static Error FolderRequired() => Error.Validation("Folder name is required");
    public static Error NotFound(string folder, string name) => Error.NotFound($"Pattern '{name}' in folder '{folder}' not found");
    public static Error AlreadyExists(string folder, string name) => Error.Conflict($"Pattern '{name}' already exists in folder '{folder}'");
    public static Error FolderMissing(string folder) => Error.NotFound($"Folder '{folder}' not found");
    public static Error ReadOnly(string folder, string name) => Error.Conflict($"Pattern '{name}' in folder '{folder}' is read-only");
    public static Error DetailMissing(string folder, string name) => Error.NotFound($"Pattern detail for '{name}' in folder '{folder}' not found");
}

