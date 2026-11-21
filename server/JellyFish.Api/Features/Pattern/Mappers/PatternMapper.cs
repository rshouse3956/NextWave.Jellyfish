using DomainPattern = JellyFish.Api.Domain.Pattern;
using DomainPatternDetail = JellyFish.Api.Domain.PatternDetail;
using DomainPatternDetails = JellyFish.Api.Domain.PatternDetails;
using DomainPatternRunData = JellyFish.Api.Domain.PatternRunData;

namespace JellyFish.Api.Features.Pattern.Mappers;

public static class PatternMapper
{
    public static PatternDto ToDto(DomainPattern entity) => new(entity.FolderName, entity.Name, entity.CreateDate, entity.LastModifiedDate, entity.IsReadOnly, null, entity.SyncRequired ?? false);

    public static PatternDto ToDto(DomainPattern entity, DomainPatternDetail detail)
    {
        // SyncRequired should be true if EITHER Pattern or PatternDetail requires sync
        var syncRequired = (entity.SyncRequired ?? false) || (detail.SyncRequired ?? false);

        return new PatternDto(
            entity.FolderName,
            entity.Name,
            entity.CreateDate,
            entity.LastModifiedDate,
            entity.IsReadOnly,
            ToDetailsDto(detail.Details),
            syncRequired
        );
    }

    public static PatternDetailsDto ToDetailsDto(DomainPatternDetails d) => new(
        Colors: d.Colors,
        SpaceBetweenPixels: d.SpaceBetweenPixels,
        EffectBetweenPixels: d.EffectBetweenPixels,
        ColorPos: d.ColorPos,
        SoffitZone: d.SoffitZone,
        Cursor: d.Cursor,
        Type: d.Type,
        Skip: d.Skip,
        NumOfLeds: d.NumOfLeds,
        RunData: new PatternRunDataDto(d.RunData.Speed, d.RunData.Brightness, d.RunData.Effect, d.RunData.EffectValue, d.RunData.RgbAdj),
        Direction: d.Direction,
        LedOnPos: d.LedOnPos,
        FireworkSettings: d.FireworkSettings is { } f ? new FireworkSettingsDto(f.SpawnRate, f.ColorPerLed, f.FireworkSpeed) : null
    );

    public static DomainPatternDetails FromDetailsDto(PatternDetailsDto d) => new DomainPatternDetails
    {
        Colors = d.Colors,
        SpaceBetweenPixels = d.SpaceBetweenPixels,
        EffectBetweenPixels = d.EffectBetweenPixels,
        LedOnPos = d.LedOnPos,
        ColorPos = d.ColorPos ?? [],
        SoffitZone = d.SoffitZone,
        Cursor = d.Cursor,
        Type = d.Type,
        Skip = d.Skip,
        NumOfLeds = d.NumOfLeds,
        RunData = new DomainPatternRunData
        {
            Speed = d.RunData.Speed,
            Brightness = d.RunData.Brightness,
            Effect = d.RunData.Effect,
            EffectValue = d.RunData.EffectValue,
            RgbAdj = d.RunData.RgbAdj
        },
        Direction = d.Direction,
        FireworkSettings = d.FireworkSettings is { } f
            ? new JellyFish.Api.Domain.FireworkSettings { SpawnRate = f.SpawnRate, ColorPerLed = f.ColorPerLed, FireworkSpeed = f.FireworkSpeed }
            : null
    };
}

