namespace JellyFish.Api.Features.Holiday;

public sealed record HolidayDto(
    string Name,
    string Category,
    string CronExpression,
    string Comment,
    bool IsEnabled,
    DateTime CreateDate,
    DateTime LastModifiedDate);

