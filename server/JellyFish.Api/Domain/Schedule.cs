using JellyFish.Api.Common;

namespace JellyFish.Api.Domain;

public sealed class Schedule : BaseEntity
{
    private Schedule() { }

    public string Name { get; private set; } = string.Empty;
    public string FolderName { get; private set; } = string.Empty;
    public string PatternName { get; private set; } = string.Empty;
    public string Priority { get; private set; } = "Normal";

    // New split fields
    public string OnTime { get; private set; } = string.Empty; // HH:mm or Sunrise/Sunset
    public string OnDay { get; private set; } = string.Empty;  // 3-field cron fragment: dom month dow
    public string OffTime { get; private set; } = string.Empty;
    public string OffDay { get; private set; } = string.Empty;

        // Targeted zones for this schedule
        public List<string> ZoneNames { get; private set; } = new();


    public DateTime? NextEvent { get; private set; }
    public string NextEventKind { get; private set; } = string.Empty; // "On" or "Off"

    public static readonly HashSet<string> AllowedPriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Low", "Normal", "High"
    };

    public static Result<Schedule> Create(
        string name,
        string folderName,
        string patternName,
        string priority,
        string onTime,
        string onDay,
        string offTime,
        string offDay,
        IReadOnlyList<string> zoneNames,
        DateTime? nextEventUtc)
    {
        if (string.IsNullOrWhiteSpace(name)) return Result.Failure<Schedule>(Error.Validation("Name is required"));
        if (string.IsNullOrWhiteSpace(folderName)) return Result.Failure<Schedule>(Error.Validation("FolderName is required"));
        if (string.IsNullOrWhiteSpace(patternName)) return Result.Failure<Schedule>(Error.Validation("PatternName is required"));
        if (string.IsNullOrWhiteSpace(priority)) return Result.Failure<Schedule>(Error.Validation("Priority is required"));
        if (!AllowedPriorities.Contains(priority.Trim())) return Result.Failure<Schedule>(Error.Validation("Invalid priority"));
        if (string.IsNullOrWhiteSpace(onTime)) return Result.Failure<Schedule>(Error.Validation("OnTime is required"));
        if (string.IsNullOrWhiteSpace(onDay)) return Result.Failure<Schedule>(Error.Validation("OnDay is required"));
        if (string.IsNullOrWhiteSpace(offTime)) return Result.Failure<Schedule>(Error.Validation("OffTime is required"));
        if (string.IsNullOrWhiteSpace(offDay)) return Result.Failure<Schedule>(Error.Validation("OffDay is required"));
        if (zoneNames is null || zoneNames.Count == 0) return Result.Failure<Schedule>(Error.Validation("ZoneNames is required"));

        var normalized = zoneNames
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .Select(z => z.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalized.Count == 0) return Result.Failure<Schedule>(Error.Validation("ZoneNames is required"));

        var s = new Schedule
        {
            Name = name.Trim(),
            FolderName = folderName.Trim(),
            PatternName = patternName.Trim(),
            Priority = priority.Trim(),
            OnTime = onTime.Trim(),
            OnDay = onDay.Trim(),
            OffTime = offTime.Trim(),
            OffDay = offDay.Trim(),
            ZoneNames = normalized,
            NextEvent = nextEventUtc
        };
        return Result.Success(s);
    }

    public void Update(string folderName, string patternName, string priority, string onTime, string onDay, string offTime, string offDay, IReadOnlyList<string>? zoneNames, DateTime? nextEventUtc)
    {
        if (!string.IsNullOrWhiteSpace(folderName)) FolderName = folderName.Trim();
        if (!string.IsNullOrWhiteSpace(patternName)) PatternName = patternName.Trim();
        if (!string.IsNullOrWhiteSpace(priority)) Priority = priority.Trim();
        if (!string.IsNullOrWhiteSpace(onTime)) OnTime = onTime.Trim();
        if (!string.IsNullOrWhiteSpace(onDay)) OnDay = onDay.Trim();
        if (!string.IsNullOrWhiteSpace(offTime)) OffTime = offTime.Trim();
        if (!string.IsNullOrWhiteSpace(offDay)) OffDay = offDay.Trim();
        if (zoneNames is not null)
        {
            var normalized = zoneNames
                .Where(z => !string.IsNullOrWhiteSpace(z))
                .Select(z => z.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (normalized.Count > 0) ZoneNames = normalized;
        }
        NextEvent = nextEventUtc;
        Touch();
    }

    public void SetNextEvent(DateTime? nextEventUtc)
    {
        NextEvent = nextEventUtc;
        Touch();
    }

    public void SetNextEvent(DateTime? nextEventUtc, string nextEventKind)
    {
        NextEvent = nextEventUtc;
        NextEventKind = nextEventKind?.Trim() ?? string.Empty;
        Touch();
    }
}

