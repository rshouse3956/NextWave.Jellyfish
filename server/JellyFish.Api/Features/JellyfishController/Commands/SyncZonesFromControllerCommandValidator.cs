using FluentValidation;

namespace JellyFish.Api.Features.JellyfishController.Commands;

public sealed class SyncZonesFromControllerCommandValidator : AbstractValidator<SyncZonesFromControllerCommand>
{
    public SyncZonesFromControllerCommandValidator()
    {
        // No inputs; existence implied. Validator present for consistency.
    }
}

