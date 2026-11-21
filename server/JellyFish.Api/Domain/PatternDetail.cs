using JellyFish.Api.Common;

namespace JellyFish.Api.Domain;

public sealed class PatternDetail : BaseEntity
{
    private PatternDetail() { }

    public string FolderName { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public PatternDetails Details { get; private set; } = new();

    public static Result<PatternDetail> Create(string folderName, string name, PatternDetails details)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return Result.Failure<PatternDetail>(Error.Validation("FolderName is required"));
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<PatternDetail>(Error.Validation("Name is required"));
        if (details is null)
            return Result.Failure<PatternDetail>(Error.Validation("Details are required"));

        var p = new PatternDetail
        {
            FolderName = folderName.Trim(),
            Name = name.Trim(),
            Details = details
        };
        return Result.Success(p);
    }

    public void Update(PatternDetails details)
    {
        Details = details;
        Touch();
    }
}

public sealed class PatternDetails
{
    public int[] Colors { get; set; } = [];
    public int SpaceBetweenPixels { get; set; }
    public string EffectBetweenPixels { get; set; } = string.Empty;
    /// <summary>
    /// Undocumented controller field kept as synced, e.g. {"0":[],"1":[0]}; mostly on Paint and Legacy patterns.
    /// Null when the controller's pattern has none.
    /// </summary>
    public Dictionary<string, int[]>? LedOnPos { get; set; }
    public int[]? ColorPos { get; set; } = [];
    public string? SoffitZone { get; set; }
    public int? Cursor { get; set; }
    public string Type { get; set; } = string.Empty;
    public int Skip { get; set; }
    public int NumOfLeds { get; set; }
    public PatternRunData RunData { get; set; } = new();
    public string Direction { get; set; } = string.Empty;
    /// <summary>Settings for the undocumented "Firework" type, as the controller stores them; null for other types.</summary>
    public FireworkSettings? FireworkSettings { get; set; }
}

/// <summary>
/// The controller's fireworkSettings (seen on a pattern saved from the Jellyfish app, 2026-11-20:
/// spawnRate 0, colorPerLED 2, fireworkSpeed 50). What each value does hasn't been measured.
/// </summary>
public sealed class FireworkSettings
{
    public int SpawnRate { get; set; }
    public int ColorPerLed { get; set; }
    public int FireworkSpeed { get; set; }
}

public sealed class PatternRunData
{
    public int Speed { get; set; }
    public int Brightness { get; set; }
    public string Effect { get; set; } = string.Empty;
    public int EffectValue { get; set; }
    public int[] RgbAdj { get; set; } = [];
}

