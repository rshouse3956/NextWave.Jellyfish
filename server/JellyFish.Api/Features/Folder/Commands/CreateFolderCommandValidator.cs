using FluentValidation;
using JellyFish.Api.Infrastructure.Repositories;

namespace JellyFish.Api.Features.Folder.Commands;

public sealed class CreateFolderCommandValidator : AbstractValidator<CreateFolderCommand>
{
    public CreateFolderCommandValidator(IFolderRepository repo)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MustAsync(async (name, ct) => !await repo.ExistsByNameAsync(name, ct))
            .WithMessage(cmd => $"Folder '{cmd.Name}' already exists");
    }
}

