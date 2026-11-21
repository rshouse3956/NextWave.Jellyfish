using FluentValidation;
using JellyFish.Api.Infrastructure.Repositories;

namespace JellyFish.Api.Features.Zone.Commands;

public sealed class CreateZoneCommandValidator : AbstractValidator<CreateZoneCommand>
{
    public CreateZoneCommandValidator(IZoneRepository zoneRepo)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MustAsync(async (name, ct) => !await zoneRepo.ExistsByNameAsync(name, ct))
            .WithMessage(cmd => $"Zone '{cmd.Name}' already exists");

        RuleFor(x => x.CtlrName)
            .NotEmpty().WithMessage("CtlrName is required");

        RuleFor(x => x.ZoneMap)
            .NotNull().WithMessage("ZoneMap is required")
            .Must(z => z.Count > 0).WithMessage("ZoneMap must have at least one entry");

        RuleForEach(x => x.ZoneMap).ChildRules(entry =>
        {
            entry.RuleFor(e => e.Port).NotEmpty().WithMessage("Port is required");
            entry.RuleFor(e => e.StartIdx).GreaterThanOrEqualTo(0);
            entry.RuleFor(e => e.EndIdx).GreaterThanOrEqualTo(0);
            entry.RuleFor(e => e.RgbStartIdx).GreaterThanOrEqualTo(0);
            entry.RuleFor(e => e).Must(e => e.StartIdx <= e.EndIdx).WithMessage("StartIdx must be <= EndIdx");
        });
    }
}

