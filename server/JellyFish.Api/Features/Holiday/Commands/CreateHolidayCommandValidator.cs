using Cronos;
using FluentValidation;
using JellyFish.Api.Infrastructure.Repositories;

namespace JellyFish.Api.Features.Holiday.Commands;

public sealed class CreateHolidayCommandValidator : AbstractValidator<CreateHolidayCommand>
{
    public CreateHolidayCommandValidator(IHolidayRepository repo)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MustAsync(async (name, ct) => !await repo.ExistsByNameAsync(name, ct))
            .WithMessage(cmd => $"Holiday '{cmd.Name}' already exists");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required")
            .Must(cat => JellyFish.Api.Domain.Holiday.AllowedCategories.Contains(cat))
            .WithMessage(cmd => $"Invalid category '{cmd.Category}'");

        RuleFor(x => x.CronExpression)
            .NotEmpty().WithMessage("CronExpression is required")
            .Must(IsValidCron).WithMessage(cmd => $"Invalid cron expression '{cmd.CronExpression}'");
    }

    private static bool IsValidCron(string expr)
    {
        try { CronExpression.Parse(expr); return true; }
        catch { try { CronExpression.Parse(expr, CronFormat.IncludeSeconds); return true; } catch { return false; } }
    }
}

