using FluentValidation;

namespace JellyFish.Api.Features.JellyfishController.Commands;

public sealed class SyncFoldersFromControllerCommandValidator : AbstractValidator<SyncFoldersFromControllerCommand>
{
    public SyncFoldersFromControllerCommandValidator()
    {
        // No inputs; existence implied. Validator present for consistency.
    }
}

