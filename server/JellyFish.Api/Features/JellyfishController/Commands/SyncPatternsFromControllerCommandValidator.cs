using FluentValidation;

namespace JellyFish.Api.Features.JellyfishController.Commands;

public sealed class SyncPatternsFromControllerCommandValidator : AbstractValidator<SyncPatternsFromControllerCommand>
{
    public SyncPatternsFromControllerCommandValidator()
    {
        // No inputs; existence implied. Validator present for consistency.
    }
}

