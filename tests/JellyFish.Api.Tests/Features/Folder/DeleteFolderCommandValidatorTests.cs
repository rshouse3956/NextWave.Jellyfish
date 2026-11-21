using FluentValidation.TestHelper;
using JellyFish.Api.Features.Folder.Commands;
using JellyFish.Api.Infrastructure.Repositories;
using Moq;

namespace JellyFish.Api.Tests.Features.Folder;

public class DeleteFolderCommandValidatorTests
{
    [Fact]
    public async Task EmptyName_IsInvalid()
    {
        var repo = new Mock<IFolderRepository>();
        var validator = new DeleteFolderCommandValidator(repo.Object);

        var result = await validator.TestValidateAsync(new DeleteFolderCommand(""));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task ExistingName_IsValid()
    {
        var repo = new Mock<IFolderRepository>();
        repo.Setup(r => r.ExistsByNameAsync("A", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new DeleteFolderCommandValidator(repo.Object);

        var result = await validator.TestValidateAsync(new DeleteFolderCommand("A"));
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }
}

