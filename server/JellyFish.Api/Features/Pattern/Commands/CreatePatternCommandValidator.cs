using FluentValidation;
using JellyFish.Api.Infrastructure.Repositories;

namespace JellyFish.Api.Features.Pattern.Commands;

public sealed class CreatePatternCommandValidator : AbstractValidator<CreatePatternCommand>
{
    public CreatePatternCommandValidator(IFolderRepository folderRepo, IPatternRepository patternRepo)
    {
        RuleFor(x => x.FolderName)
            .NotEmpty().WithMessage("FolderName is required")
            .MustAsync(async (folder, ct) => await folderRepo.ExistsByNameAsync(folder, ct))
            .WithMessage(cmd => $"Folder '{cmd.FolderName}' not found");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required");

        RuleFor(x => x)
            .MustAsync(async (cmd, ct) => !await patternRepo.ExistsAsync(cmd.FolderName, cmd.Name, ct))
            .WithMessage(cmd => $"Pattern '{cmd.Name}' already exists in folder '{cmd.FolderName}'");
    }
}

