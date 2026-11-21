using JellyFish.Api.Domain;

namespace JellyFish.Api.Features.ActiveZone.Mappers;

public static class ActiveZoneMapper
{
    public static ActiveZoneDto ToDto(Domain.ActiveZone e) => new(
        e.ZoneName,
        e.SourceType,
        e.SourceId,
        e.Priority,
        e.FolderName,
        e.PatternName,
        e.ActivatedAtUtc,
        e.ExpectedEndUtc
    );
}

