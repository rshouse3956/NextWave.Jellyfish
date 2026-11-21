using JellyFish.Api.Common;

namespace JellyFish.Api.Domain;

public sealed class Holiday : BaseEntity
{
    private Holiday() { }

    public string Name { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string CronExpression { get; private set; } = string.Empty;
    public string Comment { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }

    public static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "US Federal", "State", "Special"
    };

    public static Result<Holiday> Create(string name, string category, string cronExpression, string comment, bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(name)) return Result.Failure<Holiday>(Error.Validation("Name is required"));
        if (string.IsNullOrWhiteSpace(category)) return Result.Failure<Holiday>(Error.Validation("Category is required"));
        if (!AllowedCategories.Contains(category.Trim())) return Result.Failure<Holiday>(Error.Validation("Invalid category"));
        if (string.IsNullOrWhiteSpace(cronExpression)) return Result.Failure<Holiday>(Error.Validation("CronExpression is required"));

        var h = new Holiday
        {
            Name = name.Trim(),
            Category = category.Trim(),
            CronExpression = cronExpression.Trim(),
            Comment = comment?.Trim() ?? string.Empty,
            IsEnabled = isEnabled
        };
        return Result.Success(h);
    }

    public void Update(string category, string cronExpression, string comment, bool isEnabled)
    {
        if (!string.IsNullOrWhiteSpace(category))
            Category = category.Trim();
        if (!string.IsNullOrWhiteSpace(cronExpression))
            CronExpression = cronExpression.Trim();
        Comment = comment?.Trim() ?? string.Empty;
        IsEnabled = isEnabled;
        Touch();
    }
}

