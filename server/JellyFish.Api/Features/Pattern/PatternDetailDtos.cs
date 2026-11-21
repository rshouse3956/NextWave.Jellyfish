namespace JellyFish.Api.Features.Pattern;

public sealed record PatternRunDataDto(int Speed, int Brightness, string Effect, int EffectValue, int[] RgbAdj);

/// <summary>Settings for the "Firework" type (see <see cref="Domain.FireworkSettings"/>).</summary>
public sealed record FireworkSettingsDto(int SpawnRate, int ColorPerLed, int FireworkSpeed);

public sealed record PatternDetailsDto(
    int[] Colors,
    int SpaceBetweenPixels,
    string EffectBetweenPixels,
    int[]? ColorPos,
    string? SoffitZone,
    int? Cursor,
    string Type,
    int Skip,
    int NumOfLeds,
    PatternRunDataDto RunData,
    string Direction,
    Dictionary<string, int[]>? LedOnPos = null,
    FireworkSettingsDto? FireworkSettings = null);

