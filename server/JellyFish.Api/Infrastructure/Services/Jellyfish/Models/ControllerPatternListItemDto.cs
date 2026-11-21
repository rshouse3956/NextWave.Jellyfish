namespace JellyFish.Api.Infrastructure.Services.Jellyfish.Models;

public sealed class ControllerPatternListItemDto
{
    /// <summary>Trimmed folder name; how the app stores and shows it.</summary>
    public string FolderName { get; init; } = string.Empty;
    /// <summary>Trimmed pattern name; how the app stores and shows it.</summary>
    public string Name { get; init; } = string.Empty;
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// The folder name exactly as the controller has it. Use it in requests to the controller, which only
    /// matches exact names (some factory patterns end in a space, e.g. "Paint By Numbers ").
    /// </summary>
    public string ControllerFolderName { get; init; } = string.Empty;
    /// <summary>The pattern name exactly as the controller has it; see <see cref="ControllerFolderName"/>.</summary>
    public string ControllerName { get; init; } = string.Empty;
}
