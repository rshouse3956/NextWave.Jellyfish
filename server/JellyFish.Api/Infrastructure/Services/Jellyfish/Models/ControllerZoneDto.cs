namespace JellyFish.Api.Infrastructure.Services.Jellyfish.Models;

public sealed class ControllerZoneDto
{
    public string Name { get; init; } = string.Empty;
    public string CtlrName { get; init; } = string.Empty;
    public bool IsReadOnly { get; init; } = false;
    public IReadOnlyList<ZoneMapEntry> ZoneMap { get; init; } = [];

    public sealed class ZoneMapEntry
    {
        public int Port { get; init; }
        public int StartIdx { get; init; }
        public int EndIdx { get; init; }
        public int RgbStartIdx { get; init; }
    }
}
