using FluentValidation;
using JellyFish.Api.Infrastructure.Repositories;

namespace JellyFish.Api.Features.Holiday.Commands;

public sealed class DeleteHolidayCommandValidator : AbstractValidator<DeleteHolidayCommand>
{
    public DeleteHolidayCommandValidator(IHolidayRepository repo)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MustAsync(async (name, ct) => await repo.ExistsByNameAsync(name, ct))
            .WithMessage(cmd => $"Holiday '{cmd.Name}' not found");
    }
}

