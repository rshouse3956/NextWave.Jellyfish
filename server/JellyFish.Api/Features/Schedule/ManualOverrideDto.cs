namespace JellyFish.Api.Features.Schedule;

public sealed record ManualOverrideDto(
    string Mode,
    string ScheduleName,
    string FolderName,
    string PatternName,
    DateTime SetAtUtc,
    DateTime? SuppressUntilUtc
);

