using FluentValidation;
using JellyFish.Api.Infrastructure.Repositories;

namespace JellyFish.Api.Features.Folder.Commands;

public sealed class DeleteFolderCommandValidator : AbstractValidator<DeleteFolderCommand>
{
    public DeleteFolderCommandValidator(IFolderRepository repo)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MustAsync(async (name, ct) => await repo.ExistsByNameAsync(name, ct))
            .WithMessage(cmd => $"Folder '{cmd.Name}' not found");
    }
}

