using FluentValidation;
using JellyFish.Api.Infrastructure.Repositories;

namespace JellyFish.Api.Features.Schedule.Commands;

public sealed class DeleteScheduleCommandValidator : AbstractValidator<DeleteScheduleCommand>
{
    public DeleteScheduleCommandValidator(IScheduleRepository scheduleRepo)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MustAsync(async (name, ct) => await scheduleRepo.ExistsByNameAsync(name, ct))
            .WithMessage(cmd => $"Schedule '{cmd.Name}' not found");
    }
}

