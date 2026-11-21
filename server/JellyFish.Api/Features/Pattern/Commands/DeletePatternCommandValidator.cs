using FluentValidation;
using JellyFish.Api.Infrastructure.Repositories;

namespace JellyFish.Api.Features.Pattern.Commands;

public sealed class DeletePatternCommandValidator : AbstractValidator<DeletePatternCommand>
{
    public DeletePatternCommandValidator(IPatternRepository patternRepo)
    {
        RuleFor(x => x.FolderName).NotEmpty().WithMessage("FolderName is required");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MustAsync(async (cmd, name, ct) => await patternRepo.ExistsAsync(cmd.FolderName, name, ct))
            .WithMessage(cmd => $"Pattern '{cmd.Name}' in folder '{cmd.FolderName}' not found");
    }
}

