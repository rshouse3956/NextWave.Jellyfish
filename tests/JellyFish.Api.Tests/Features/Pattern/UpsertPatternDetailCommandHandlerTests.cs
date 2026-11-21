using JellyFish.Api.Common;
using JellyFish.Api.Features.Pattern;
using JellyFish.Api.Features.Pattern.Commands;
using JellyFish.Api.Infrastructure.Repositories;
using Moq;
using Shouldly;
using DomainPattern = JellyFish.Api.Domain.Pattern;
using DomainPatternDetail = JellyFish.Api.Domain.PatternDetail;

namespace JellyFish.Api.Tests.Features.Pattern;

public class UpsertPatternDetailCommandHandlerTests
{
    private static PatternDetailsDto MakeDetailsDto() => new(
        Colors: new[] { 1, 2, 3 },
        SpaceBetweenPixels: 2,
        EffectBetweenPixels: "No Color Transform",
        ColorPos: new[] { -1 },
        SoffitZone: null,
        Cursor: -1,
        Type: "Chase",
        Skip: 0,
        NumOfLeds: 1,
        RunData: new PatternRunDataDto(0, 100, "No Effect", 0, new[] { 100, 100, 100 }),
        Direction: "Left"
    );

    [Fact]
    public async Task PatternNotFound_ReturnsNotFound()
    {
        var patterns = new Mock<IPatternRepository>();
        patterns.Setup(p => p.GetAsync("F", "N", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<DomainPattern>(Error.NotFound("not found")));
        var details = new Mock<IPatternDetailRepository>();

        var sut = new UpsertPatternDetailCommandHandler(patterns.Object, details.Object);
        var res = await sut.Handle(new UpsertPatternDetailCommand("F", "N", MakeDetailsDto()), default);

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

        var sut = new UpsertPatternDetailCommandHandler(patterns.Object, details.Object);
        var res = await sut.Handle(new UpsertPatternDetailCommand("F", "N", MakeDetailsDto()), default);

        res.IsFailure.ShouldBeTrue();
        res.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task Upsert_Succeeds_AndReturnsEnrichedDto()
    {
        var patterns = new Mock<IPatternRepository>();
        var pat = DomainPattern.Create("F", "N", false).Value;
        patterns.Setup(p => p.GetAsync("F", "N", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(pat));

        var details = new Mock<IPatternDetailRepository>();
        details.Setup(d => d.UpsertAsync(It.IsAny<DomainPatternDetail>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        details.Setup(d => d.GetAsync("F", "N", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(DomainPatternDetail.Create("F", "N",
                new JellyFish.Api.Domain.PatternDetails
                {
                    Colors = new[] { 1, 2, 3 },
                    SpaceBetweenPixels = 2,
                    EffectBetweenPixels = "No Color Transform",
                    ColorPos = new[] { -1 },
                    SoffitZone = null,
                    Cursor = -1,
                    Type = "Chase",
                    Skip = 0,
                    NumOfLeds = 1,
                    RunData = new JellyFish.Api.Domain.PatternRunData
                    {
                        Speed = 0,
                        Brightness = 100,
                        Effect = "No Effect",
                        EffectValue = 0,
                        RgbAdj = new[] { 100, 100, 100 }
                    },
                    Direction = "Left"
                }).Value));

        var sut = new UpsertPatternDetailCommandHandler(patterns.Object, details.Object);
        var res = await sut.Handle(new UpsertPatternDetailCommand("F", "N", MakeDetailsDto()), default);

        res.IsSuccess.ShouldBeTrue();
        res.Value.Details.ShouldNotBeNull();
        res.Value.Details!.Colors.ShouldBe(new[] { 1, 2, 3 });
        res.Value.Name.ShouldBe("N");
        res.Value.FolderName.ShouldBe("F");
    }
}

