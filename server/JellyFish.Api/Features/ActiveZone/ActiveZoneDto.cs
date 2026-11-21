namespace JellyFish.Api.Features.ActiveZone;

public sealed record ActiveZoneDto(
    string ZoneName,
    string SourceType,
    string SourceId,
    int Priority,
    string FolderName,
    string PatternName,
    DateTime ActivatedAtUtc,
    DateTime? ExpectedEndUtc
);

