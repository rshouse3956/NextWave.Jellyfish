using JellyFish.Api.Common;

namespace JellyFish.Api.Domain;

public sealed class Zone : BaseEntity
{
    private Zone() { }

    public string Name { get; private set; } = string.Empty;
    public string CtlrName { get; private set; } = string.Empty;
    public bool IsReadOnly { get; private set; }
    public List<ZoneMapEntry> ZoneMap { get; private set; } = [];

    public static Result<Zone> Create(string name, string ctlrName, bool isReadOnly, List<ZoneMapEntry> zoneMap)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Zone>(Error.Validation("Name is required"));
        if (string.IsNullOrWhiteSpace(ctlrName))
            return Result.Failure<Zone>(Error.Validation("CtlrName is required"));
        if (zoneMap is null || zoneMap.Count == 0)
            return Result.Failure<Zone>(Error.Validation("ZoneMap is required"));

        foreach (var e in zoneMap)
        {
            if (e.Port < 0)
                return Result.Failure<Zone>(Error.Validation("ZoneMap.Port must be >= 0"));
            if (e.StartIdx < 0 || e.EndIdx < 0 || e.RgbStartIdx < 0 || e.StartIdx > e.EndIdx)
                return Result.Failure<Zone>(Error.Validation("Invalid ZoneMap entry indices"));
        }

        var z = new Zone
        {
            Name = name.Trim(),
            CtlrName = ctlrName.Trim(),
            IsReadOnly = isReadOnly,
            ZoneMap = zoneMap
        };
        return Result.Success(z);
    }

    public void Update(string ctlrName, bool isReadOnly, List<ZoneMapEntry> zoneMap)
    {
        CtlrName = ctlrName.Trim();
        IsReadOnly = isReadOnly;
        ZoneMap = zoneMap;
        Touch();
    }

    public sealed class ZoneMapEntry
    {
        public int Port { get; init; }
        public int StartIdx { get; init; }
        public int EndIdx { get; init; }
        public int RgbStartIdx { get; init; }
    }
}

