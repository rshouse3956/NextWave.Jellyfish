namespace JellyFish.Api.Features.Holiday.Queries;

public sealed record IsHolidayDto(bool IsHoliday, List<string> Matches);

