namespace JellyFish.Api.Features.Zone;

public sealed record ZoneDto(
    string Name,
    string CtlrName,
    DateTime CreateDate,
    DateTime LastModifiedDate,
    bool IsReadOnly,
    List<ZoneMapEntryDto> ZoneMap,
    bool SyncRequired);

public sealed record ZoneMapEntryDto(int Port, int StartIdx, int EndIdx, int RgbStartIdx);

