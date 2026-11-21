using FluentValidation;

namespace JellyFish.Api.Features.ActiveZone.Commands;

public sealed class StopActiveZoneCommandValidator : AbstractValidator<StopActiveZoneCommand>
{
    public StopActiveZoneCommandValidator()
    {
        RuleFor(x => x.ZoneName)
            .NotEmpty().WithMessage("ZoneName is required")
            .Must(z => !string.IsNullOrWhiteSpace(z)).WithMessage("ZoneName cannot be whitespace");
    }
}

