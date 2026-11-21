namespace JellyFish.Api.Features.Holiday.Mappers;

public static class HolidayMapper
{
    public static HolidayDto ToDto(JellyFish.Api.Domain.Holiday entity)
        => new(entity.Name, entity.Category, entity.CronExpression, entity.Comment, entity.IsEnabled, entity.CreateDate, entity.LastModifiedDate);
}

