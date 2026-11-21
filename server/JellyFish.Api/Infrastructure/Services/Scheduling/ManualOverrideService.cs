using System.Collections.Immutable;

namespace JellyFish.Api.Infrastructure.Services.Scheduling;

public sealed class ManualOverrideService : IManualOverrideService
{
    private ManualOverrideState? _current;
    private readonly object _gate = new();

    public ManualOverrideState? Current
    {
        get { lock (_gate) { return _current; } }
    }

    public void SetOn(string scheduleName, string folderName, string patternName, IReadOnlyList<string> zoneNames)
    {
        lock (_gate)
        {
            _current = new ManualOverrideState(
                Mode: ManualOverrideMode.On,
                ScheduleName: scheduleName,
                FolderName: folderName,
                PatternName: patternName,
                ZoneNames: zoneNames.ToImmutableArray(),
                SetAtUtc: DateTime.UtcNow,
                SuppressUntilUtc: null);
        }
    }

    public void SetBlockOff(string scheduleName, string folderName, string patternName, DateTime suppressUntilUtc)
    {
        lock (_gate)
        {
            _current = new ManualOverrideState(
                Mode: ManualOverrideMode.BlockOff,
                ScheduleName: scheduleName,
                FolderName: folderName,
                PatternName: patternName,
                ZoneNames: Array.Empty<string>(),
                SetAtUtc: DateTime.UtcNow,
                SuppressUntilUtc: suppressUntilUtc);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _current = null;
        }
    }
}

