using Cronos;
using FluentValidation;
using JellyFish.Api.Infrastructure.Repositories;

namespace JellyFish.Api.Features.Schedule.Commands;

public sealed class UpdateScheduleCommandValidator : AbstractValidator<UpdateScheduleCommand>
{
    public UpdateScheduleCommandValidator(IScheduleRepository scheduleRepo, IPatternRepository patternRepo, IZoneRepository zoneRepo)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MustAsync(async (name, ct) => await scheduleRepo.ExistsByNameAsync(name, ct))
            .WithMessage(cmd => $"Schedule '{cmd.Name}' not found");

        RuleFor(x => x.FolderName)
            .NotEmpty().WithMessage("FolderName is required");

        RuleFor(x => x.PatternName)
            .NotEmpty().WithMessage("PatternName is required")
            .MustAsync(async (cmd, pattern, ct) => await patternRepo.ExistsAsync(cmd.FolderName, pattern, ct))
            .WithMessage(cmd => $"Pattern '{cmd.PatternName}' in folder '{cmd.FolderName}' not found");

        RuleFor(x => x.Priority)
            .NotEmpty().WithMessage("Priority is required")
            .Must(p => JellyFish.Api.Domain.Schedule.AllowedPriorities.Contains(p))
            .WithMessage(cmd => $"Invalid priority '{cmd.Priority}'");

        RuleFor(x => x.OnTime)
            .NotEmpty().WithMessage("OnTime is required")
            .Must(IsValidTimeSpec).WithMessage(cmd => $"OnTime must be HH:mm or Sunrise/Sunset (got '{cmd.OnTime}')");

        RuleFor(x => x.OffTime)
            .NotEmpty().WithMessage("OffTime is required")
            .Must(IsValidTimeSpec).WithMessage(cmd => $"OffTime must be HH:mm or Sunrise/Sunset (got '{cmd.OffTime}')");

        RuleFor(x => x.OnDay)
            .NotEmpty().WithMessage("OnDay is required")
            .Must(IsValidDayFragment).WithMessage(cmd => $"OnDay must be 3-field cron (dom month dow), got '{cmd.OnDay}'");

        RuleFor(x => x.OffDay)
            .NotEmpty().WithMessage("OffDay is required")
            .Must(IsValidDayFragment).WithMessage(cmd => $"OffDay must be 3-field cron (dom month dow), got '{cmd.OffDay}'");

        RuleFor(x => x.ZoneNames)
            .NotNull().WithMessage("ZoneNames is required")
            .Must(z => z.Count > 0).WithMessage("ZoneNames must include at least one zone")
            .MustAsync(async (_, list, ct) => {
                foreach (var name in list.Distinct(StringComparer.OrdinalIgnoreCase))
                    if (!await zoneRepo.ExistsByNameAsync(name, ct)) return false;
                return true;
            }).WithMessage("One or more zones were not found");
    }

    private static bool IsValidTimeSpec(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return false;
        if (spec.Equals("sunrise", StringComparison.OrdinalIgnoreCase)) return true;
        if (spec.Equals("sunset", StringComparison.OrdinalIgnoreCase)) return true;
        return TimeOnly.TryParseExact(spec, "HH:mm", out _);
    }

    private static bool IsValidDayFragment(string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment)) return false;
        var parts = fragment.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) return false;
        try { CronExpression.Parse($"0 0 {fragment}"); return true; }
        catch { return false; }
    }
}

