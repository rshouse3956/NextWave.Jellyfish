using JellyFish.Api.Common;
using JellyFish.Api.Features.Pattern.Commands;
using JellyFish.Api.Infrastructure.Repositories;
using Moq;
using Shouldly;
using DomainPattern = JellyFish.Api.Domain.Pattern;

namespace JellyFish.Api.Tests.Features.Pattern;

public class DeletePatternDetailCommandHandlerTests
{
    [Fact]
    public async Task PatternNotFound_ReturnsNotFound()
    {
        var patterns = new Mock<IPatternRepository>();
        patterns.Setup(p => p.GetAsync("F", "N", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<DomainPattern>(Error.NotFound("not found")));
        var details = new Mock<IPatternDetailRepository>();

        var sut = new DeletePatternDetailCommandHandler(patterns.Object, details.Object);
        var res = await sut.Handle(new DeletePatternDetailCommand("F", "N"), default);

        res.IsFailure.ShouldBeTrue();
        res.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task PatternReadOnly_ReturnsConflict()
    {
        var patterns = new Mock<IPatternRepository>();
        var pat = DomainPattern.Create("F", "N", true).Value;
        patterns.Setup(p => p.GetAsync("F", "N", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(pat));
        var details = new Mock<IPatternDetailRepository>();

        var sut = new DeletePatternDetailCommandHandler(patterns.Object, details.Object);
        var res = await sut.Handle(new DeletePatternDetailCommand("F", "N"), default);

        res.IsFailure.ShouldBeTrue();
        res.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task Delete_Succeeds_WhenWritablePattern()
    {
        var patterns = new Mock<IPatternRepository>();
        var pat = DomainPattern.Create("F", "N", false).Value;
        patterns.Setup(p => p.GetAsync("F", "N", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(pat));

        var details = new Mock<IPatternDetailRepository>();
        details.Setup(d => d.DeleteAsync("F", "N", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var sut = new DeletePatternDetailCommandHandler(patterns.Object, details.Object);
        var res = await sut.Handle(new DeletePatternDetailCommand("F", "N"), default);

        res.IsSuccess.ShouldBeTrue();
    }
}

