using JellyFish.Api.Domain;

namespace JellyFish.Api.Features.Zone.Mappers;

public static class ZoneMapper
{
    public static ZoneDto ToDto(JellyFish.Api.Domain.Zone entity) => new(
        entity.Name,
        entity.CtlrName,
        entity.CreateDate,
        entity.LastModifiedDate,
        entity.IsReadOnly,
        entity.ZoneMap.Select(e => new ZoneMapEntryDto(e.Port, e.StartIdx, e.EndIdx, e.RgbStartIdx)).ToList(),
        entity.SyncRequired ?? false);

    public static List<JellyFish.Api.Domain.Zone.ZoneMapEntry> FromDtoEntries(IEnumerable<ZoneMapEntryDto> dtos)
        => dtos.Select(d => new JellyFish.Api.Domain.Zone.ZoneMapEntry { Port = d.Port, StartIdx = d.StartIdx, EndIdx = d.EndIdx, RgbStartIdx = d.RgbStartIdx }).ToList();
}

