using FluentValidation;
using JellyFish.Api.Infrastructure.Repositories;

namespace JellyFish.Api.Features.Zone.Commands;

public sealed class DeleteZoneCommandValidator : AbstractValidator<DeleteZoneCommand>
{
    public DeleteZoneCommandValidator(IZoneRepository zoneRepo)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MustAsync(async (name, ct) => await zoneRepo.ExistsByNameAsync(name, ct))
            .WithMessage(cmd => $"Zone '{cmd.Name}' not found");
    }
}

