namespace JellyFish.Api.Infrastructure.Services.Scheduling;

public interface IManualOverrideService
{
    ManualOverrideState? Current { get; }
    void SetOn(string scheduleName, string folderName, string patternName, IReadOnlyList<string> zoneNames);
    void SetBlockOff(string scheduleName, string folderName, string patternName, DateTime suppressUntilUtc);
    void Clear();
}

public enum ManualOverrideMode { On, BlockOff }

public sealed record ManualOverrideState(
    ManualOverrideMode Mode,
    string ScheduleName,
    string FolderName,
    string PatternName,
    IReadOnlyList<string> ZoneNames,
    DateTime SetAtUtc,
    DateTime? SuppressUntilUtc
);

