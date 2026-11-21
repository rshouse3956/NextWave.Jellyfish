using JellyFish.Api.Common;

namespace JellyFish.Api.Domain;

public sealed class ActiveZone : BaseEntity
{
    private ActiveZone() { }

    public string ZoneName { get; private set; } = string.Empty;
    public string SourceType { get; private set; } = ""; // Schedule | Manual
    public string SourceId { get; private set; } = string.Empty; // ScheduleName or Manual marker
    public int Priority { get; private set; } // Lower number = higher priority (0 = Manual)
    public string FolderName { get; private set; } = string.Empty;
    public string PatternName { get; private set; } = string.Empty;
    public DateTime ActivatedAtUtc { get; private set; }
    public DateTime? ExpectedEndUtc { get; private set; }

    public static Result<ActiveZone> Create(
        string zoneName,
        string sourceType,
        string sourceId,
        int priority,
        string folderName,
        string patternName,
        DateTime activatedAtUtc,
        DateTime? expectedEndUtc)
    {
        if (string.IsNullOrWhiteSpace(zoneName)) return Result.Failure<ActiveZone>(Error.Validation("ZoneName is required"));
        if (string.IsNullOrWhiteSpace(sourceType)) return Result.Failure<ActiveZone>(Error.Validation("SourceType is required"));
        if (string.IsNullOrWhiteSpace(folderName)) return Result.Failure<ActiveZone>(Error.Validation("FolderName is required"));
        if (string.IsNullOrWhiteSpace(patternName)) return Result.Failure<ActiveZone>(Error.Validation("PatternName is required"));

        var az = new ActiveZone
        {
            ZoneName = zoneName.Trim(),
            SourceType = sourceType.Trim(),
            SourceId = sourceId?.Trim() ?? string.Empty,
            Priority = priority,
            FolderName = folderName.Trim(),
            PatternName = patternName.Trim(),
            ActivatedAtUtc = activatedAtUtc,
            ExpectedEndUtc = expectedEndUtc
        };
        return Result.Success(az);
    }

    public void UpdateOwner(string sourceType, string sourceId, int priority, string folderName, string patternName, DateTime activatedAtUtc, DateTime? expectedEndUtc)
    {
        SourceType = sourceType.Trim();
        SourceId = sourceId?.Trim() ?? string.Empty;
        Priority = priority;
        FolderName = folderName.Trim();
        PatternName = patternName.Trim();
        ActivatedAtUtc = activatedAtUtc;
        ExpectedEndUtc = expectedEndUtc;
        Touch();
    }
}

