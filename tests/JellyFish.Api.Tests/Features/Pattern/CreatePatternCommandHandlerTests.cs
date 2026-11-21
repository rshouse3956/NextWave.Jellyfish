using JellyFish.Api.Common;
using JellyFish.Api.Features.Pattern.Commands;
using JellyFish.Api.Infrastructure.Repositories;
using Moq;
using Shouldly;

namespace JellyFish.Api.Tests.Features.Pattern;

public class CreatePatternCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_Create_With_Minimal_Detail()
    {
        // Arrange
        var folders = new Mock<IFolderRepository>();
        var patterns = new Mock<IPatternRepository>();
        var details = new Mock<IPatternDetailRepository>();

        folders.Setup(f => f.ExistsByNameAsync("F", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        patterns.Setup(p => p.ExistsAsync("F", "N", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        patterns.Setup(p => p.AddAsync(It.IsAny<JellyFish.Api.Domain.Pattern>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        details.Setup(d => d.UpsertAsync(It.IsAny<JellyFish.Api.Domain.PatternDetail>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var sut = new CreatePatternCommandHandler(folders.Object, patterns.Object, details.Object);

        // Act
        var res = await sut.Handle(new CreatePatternCommand("F", "N", false), default);

        // Assert
        res.IsSuccess.ShouldBeTrue();
        res.Value.FolderName.ShouldBe("F");
        res.Value.Name.ShouldBe("N");
        res.Value.Details.ShouldNotBeNull();
        res.Value.Details!.Colors.ShouldNotBeNull();
    }
}

