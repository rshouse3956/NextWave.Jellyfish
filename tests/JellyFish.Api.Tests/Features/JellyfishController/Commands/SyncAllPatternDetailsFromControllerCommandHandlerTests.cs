using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Features.JellyfishController.Commands;
using JellyFish.Api.Infrastructure.Repositories;
using JellyFish.Api.Infrastructure.Services.Jellyfish;
using JellyFish.Api.Infrastructure.Services.Jellyfish.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace JellyFish.Api.Tests.Features.JellyfishController.Commands;

public class SyncAllPatternDetailsFromControllerCommandHandlerTests
{
    private readonly Mock<IJellyfishControllerClient> _client = new();
    private readonly Mock<IFolderRepository> _folders = new();
    private readonly Mock<IPatternRepository> _patterns = new();
    private readonly Mock<IPatternDetailRepository> _details = new();
    private readonly List<PatternDetail> _upserted = [];

    public SyncAllPatternDetailsFromControllerCommandHandlerTests()
    {
        _folders.Setup(f => f.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _patterns.Setup(p => p.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _details.Setup(d => d.UpsertAsync(It.IsAny<PatternDetail>(), It.IsAny<CancellationToken>()))
            .Callback<PatternDetail, CancellationToken>((d, _) => _upserted.Add(d))
            .ReturnsAsync(Result.Success());
    }

    private SyncAllPatternDetailsFromControllerCommandHandler Handler() =>
        new(_client.Object, _folders.Object, _patterns.Object, _details.Object,
            NullLogger<SyncAllPatternDetailsFromControllerCommandHandler>.Instance);

    private void ControllerHas(params ControllerPatternListItemDto[] patterns) =>
        _client.Setup(c => c.GetPatternsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ControllerPatternListItemDto>>(patterns));

    private static ControllerPatternListItemDto Item(string folder, string controllerName) => new()
    {
        FolderName = folder.Trim(),
        Name = controllerName.Trim(),
        ControllerFolderName = folder,
        ControllerName = controllerName,
        IsReadOnly = true
    };

    private static PatternDetails Details(string type = "Chase") => new()
    {
        Colors = [255, 0, 0],
        Type = type,
        RunData = new PatternRunData { Speed = 1, Brightness = 100, Effect = "No Effect", RgbAdj = [100, 100, 100] }
    };

    [Fact]
    public async Task Requests_details_with_the_exact_controller_name_and_stores_the_trimmed_name()
    {
        ControllerHas(Item("Special Effects", "Paint By Numbers "));
        _client.Setup(c => c.GetPatternDetailAsync("Special Effects", "Paint By Numbers ", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Details("Paint")));

        var res = await Handler().Handle(new SyncAllPatternDetailsFromControllerCommand(), CancellationToken.None);

        res.IsSuccess.ShouldBeTrue();
        res.Value.Synced.ShouldBe(1);
        _upserted.ShouldHaveSingleItem().Name.ShouldBe("Paint By Numbers");
        _client.Verify(c => c.GetPatternDetailAsync("Special Effects", "Paint By Numbers", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task One_failure_does_not_discard_the_others()
    {
        ControllerHas(Item("A", "Good"), Item("A", "Broken"), Item("A", "Empty"));
        _client.Setup(c => c.GetPatternDetailAsync("A", "Good", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(Details()));
        _client.Setup(c => c.GetPatternDetailAsync("A", "Broken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PatternDetails>(Error.Problem("Request timed out communicating with Jellyfish controller")));
        _client.Setup(c => c.GetPatternDetailAsync("A", "Empty", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PatternDetails>(Error.NotFound("no details")));

        var res = await Handler().Handle(new SyncAllPatternDetailsFromControllerCommand(), CancellationToken.None);

        // Success, so the unit of work commits the pattern that did sync.
        res.IsSuccess.ShouldBeTrue();
        res.Value.Total.ShouldBe(3);
        res.Value.Synced.ShouldBe(1);
        res.Value.NoDetails.ShouldBe(["A/Empty"]);
        var failure = res.Value.Failed.ShouldHaveSingleItem();
        failure.Name.ShouldBe("Broken");
        failure.Error.ShouldContain("timed out");
        _upserted.ShouldHaveSingleItem().Name.ShouldBe("Good");
    }

    [Fact]
    public async Task Retries_a_failed_controller_request_once()
    {
        ControllerHas(Item("A", "Flaky"));
        _client.SetupSequence(c => c.GetPatternDetailAsync("A", "Flaky", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PatternDetails>(Error.Problem("Request timed out communicating with Jellyfish controller")))
            .ReturnsAsync(Result.Success(Details()));

        var res = await Handler().Handle(new SyncAllPatternDetailsFromControllerCommand(), CancellationToken.None);

        res.Value.Synced.ShouldBe(1);
        res.Value.Failed.ShouldBeEmpty();
    }

    [Fact]
    public async Task Names_that_only_differ_by_trailing_spaces_sync_once()
    {
        ControllerHas(Item("Colors", "Turquoise"), Item("Colors", "Turquoise "));
        _client.Setup(c => c.GetPatternDetailAsync("Colors", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Details("Color")));

        var res = await Handler().Handle(new SyncAllPatternDetailsFromControllerCommand(), CancellationToken.None);

        res.Value.Total.ShouldBe(1);
        _upserted.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Fails_when_the_pattern_list_cannot_be_read()
    {
        _client.Setup(c => c.GetPatternsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<IReadOnlyList<ControllerPatternListItemDto>>(Error.Problem("Failed to communicate with Jellyfish controller")));

        var res = await Handler().Handle(new SyncAllPatternDetailsFromControllerCommand(), CancellationToken.None);

        res.IsFailure.ShouldBeTrue();
    }
}
