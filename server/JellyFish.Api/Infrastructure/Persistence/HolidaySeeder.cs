using JellyFish.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JellyFish.Api.Infrastructure.Persistence;

public sealed class HolidaySeeder : IHostedService
{
    private readonly ILogger<HolidaySeeder> _logger;
    private readonly IServiceProvider _sp;

    public HolidaySeeder(ILogger<HolidaySeeder> logger, IServiceProvider sp)
    {
        _logger = logger;
        _sp = sp;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<JellyfishDbContext>();

            var any = await db.Holidays.AnyAsync(cancellationToken);
            if (any) return;

            var list = new List<Holiday>();

            void Add(string name, string category, string cron, string comment)
            {
                var created = Holiday.Create(name, category, cron, comment, isEnabled: true);
                if (created.IsSuccess) list.Add(created.Value);
                else _logger.LogWarning("Skipping holiday '{Name}': {Error}", name, created.Error.Description);
            }

            Add("New Year’s Day", "US Federal", "0 0 1 1 ?", "January 1 @ 12:00 AM");
            Add("Martin Luther King Jr Day", "US Federal", "0 0 ? 1 1#3", "Third Monday in January");
            Add("President’s Day", "US Federal", "0 0 ? 2 1#3", "Third Monday in Feb");
            Add("Memorial Day", "US Federal", "0 0 ? 5 1L", "Last Monday in May");
            Add("Independence Day", "US Federal", "0 0 4 7 ?", "July 4");
            Add("Labor Day", "US Federal", "0 0 ? 9 1#1", "First Monday");
            Add("Columbus Day", "US Federal", "0 0 ? 10 1#2", "Second Monday");
            Add("Veterans Day", "US Federal", "0 0 11 11 ?", "November 11");
            Add("Thanksgiving Day", "US Federal", "0 0 ? 11 4#4", "Fourth Thursday");
            Add("Christmas Day", "US Federal", "0 0 25 12 ?", "December 25");
            Add("Juneteenth", "US Federal", "0 0 19 6 ?", "June 19");
            Add("Halloween", "Special", "0 0 31 10 ?", "October 31");

            if (list.Count > 0)
            {
                await db.Holidays.AddRangeAsync(list, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Seeded {Count} holidays", list.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Holiday seeding failed (continuing startup)");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

