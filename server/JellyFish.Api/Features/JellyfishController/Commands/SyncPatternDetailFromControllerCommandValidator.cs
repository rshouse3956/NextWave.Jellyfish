using FluentValidation;

namespace JellyFish.Api.Features.JellyfishController.Commands;

public sealed class SyncPatternDetailFromControllerCommandValidator : AbstractValidator<SyncPatternDetailFromControllerCommand>
{
    public SyncPatternDetailFromControllerCommandValidator()
    {
        RuleFor(x => x.FolderName).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
    }
}

