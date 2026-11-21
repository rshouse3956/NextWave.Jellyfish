using System.Net;
using System.Text.Json;
using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Features.Pattern.Mappers;
using JellyFish.Api.Features.PatternAssistant.Commands;
using JellyFish.Api.Infrastructure.Persistence;
using JellyFish.Api.Infrastructure.Repositories;
using JellyFish.Api.Infrastructure.Services.Auditing;
using JellyFish.Api.Infrastructure.Services.PatternAssistant;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using static JellyFish.Api.Tests.Features.PatternAssistant.PatternAssistantTestData;

namespace JellyFish.Api.Tests.Features.PatternAssistant;

public class GeneratePatternCommandHandlerTests
{
    private readonly Mock<IPatternDetailRepository> _details = new();
    private readonly Mock<IAuditLogService> _audit = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly List<AuditCall> _audits = [];

    /// <summary>One LogActionAsync call, with the payloads serialized as the real service stores them.</summary>
    private sealed record AuditCall(string ActionType, string PatternName, bool Success, string Error, string Request, string Response);

    public GeneratePatternCommandHandlerTests()
    {
        _audit.Setup(a => a.LogActionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>?>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<string>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Callback((string action, string _, string _, string _, string pattern, IReadOnlyList<string>? _, int? _,
                    bool success, string error, object? req, object? res, long _, CancellationToken _) =>
                _audits.Add(new AuditCall(action, pattern, success, error, JsonSerializer.Serialize(req), JsonSerializer.Serialize(res))))
            .ReturnsAsync(Result.Success());
    }

    private GeneratePatternCommandHandler Sut(IChatClient chat, bool enabled = true, int maxExamples = 4) => new(
        chat,
        _details.Object,
        _audit.Object,
        _uow.Object,
        Options.Create(new PatternAssistantOptions { Enabled = enabled, Model = "test-model", MaxExamples = maxExamples, TimeoutSeconds = 30 }),
        NullLogger<GeneratePatternCommandHandler>.Instance);

    private static Task<Result<JellyFish.Api.Features.PatternAssistant.GeneratedPatternDto>> Run(GeneratePatternCommandHandler sut, string prompt = "candy cane") =>
        sut.Handle(new GeneratePatternCommand(prompt), default);

    [Fact]
    public async Task Disabled_ReturnsNotFound_WithoutCallingModel()
    {
        var chat = new FakeChatClient();
        var res = await Run(Sut(chat, enabled: false));

        res.IsFailure.ShouldBeTrue();
        res.Error.Type.ShouldBe(ErrorType.NotFound);
        chat.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidReply_ReturnsSanitizedPattern()
    {
        var chat = new FakeChatClient().Reply(ValidReply);
        var res = await Run(Sut(chat));

        res.IsSuccess.ShouldBeTrue();
        res.Value.SuggestedName.ShouldBe("Candy Cane Chase");
        res.Value.Details.Colors.ShouldBe(new[] { 255, 0, 0, 255, 255, 255 });
        res.Value.Details.Direction.ShouldBe("Right");
        res.Value.Details.RunData.Speed.ShouldBe(50); // speedLevel 3
    }

    [Fact]
    public async Task SendsJsonSchemaAndModelOptions()
    {
        var chat = new FakeChatClient().Reply(ValidReply);
        await Run(Sut(chat));

        var options = chat.Calls.Single().Options!;
        options.ModelId.ShouldBe("test-model");
        var format = options.ResponseFormat.ShouldBeOfType<ChatResponseFormatJson>();
        format.Schema.ShouldNotBeNull();
        format.Schema!.Value.GetProperty("properties").TryGetProperty("speedLevel", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task RequestRules_AreApplied()
    {
        var fadeReply = ValidReply.Replace("\"No Color Transform\"", "\"Fade\"");
        (await Run(Sut(new FakeChatClient().Reply(fadeReply)), "a block of 3 red and 3 white"))
            .Value.Details.EffectBetweenPixels.ShouldBe("Repeat");
        (await Run(Sut(new FakeChatClient().Reply(fadeReply)), "red and white fading"))
            .Value.Details.EffectBetweenPixels.ShouldBe("Fade");
        var sequenceReply = ValidReply.Replace("\"Chase\"", "\"Sequence\"");
        (await Run(Sut(new FakeChatClient().Reply(sequenceReply)), "rainbow across the roofline"))
            .Value.Details.Type.ShouldBe("Color");
    }

    [Fact]
    public async Task FencedReply_IsAccepted()
    {
        var chat = new FakeChatClient().Reply("```json\n" + ValidReply + "\n```");
        (await Run(Sut(chat))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task InvalidThenValid_RetriesWithError()
    {
        var chat = new FakeChatClient().Reply("not json").Reply(ValidReply);
        var res = await Run(Sut(chat));

        res.IsSuccess.ShouldBeTrue();
        chat.Calls.Count.ShouldBe(2);
        var retry = chat.Calls[1].Messages;
        retry[^2].Role.ShouldBe(ChatRole.Assistant);
        retry[^2].Text.ShouldBe("not json");
        retry[^1].Text.ShouldContain("could not be used");
    }

    [Fact]
    public async Task InvalidTwice_ReturnsInvalidResponse()
    {
        var chat = new FakeChatClient().Reply("{}").Reply("""{"colors":[]}""");
        var res = await Run(Sut(chat));

        res.IsFailure.ShouldBeTrue();
        res.Error.Code.ShouldBe("PatternAssistant.InvalidResponse");
        chat.Calls.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ConnectionFailure_ReturnsUnavailable()
    {
        var chat = new FakeChatClient().Throw(new HttpRequestException("connection refused"));
        var res = await Run(Sut(chat));
        res.Error.Code.ShouldBe("PatternAssistant.Unavailable");
    }

    [Fact]
    public async Task HttpTimeout_ReturnsTimeout()
    {
        var chat = new FakeChatClient().Throw(new TaskCanceledException("timeout", new TimeoutException()));
        var res = await Run(Sut(chat));
        res.Error.Code.ShouldBe("PatternAssistant.Timeout");
    }

    [Fact]
    public async Task ModelNotFound_ReturnsModelError()
    {
        var chat = new FakeChatClient().Throw(new HttpRequestException("model 'x' not found", null, HttpStatusCode.NotFound));
        var res = await Run(Sut(chat));
        res.Error.Code.ShouldBe("PatternAssistant.ModelError");
        res.Error.Description.ShouldContain("not found");
    }

    [Fact]
    public async Task CallerCancellation_Propagates()
    {
        var chat = new FakeChatClient().Throw(new OperationCanceledException());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() =>
            Sut(chat).Handle(new GeneratePatternCommand("x"), cts.Token));
    }

    [Fact]
    public async Task StoredPatterns_AreSentAsExamples()
    {
        var detail = PatternDetail.Create("Christmas", "Candy Cane", PatternMapper.FromDetailsDto(Details())).Value;
        _details.Setup(d => d.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { detail });
        var chat = new FakeChatClient().Reply(ValidReply);

        await Run(Sut(chat), "christmas candy cane");

        chat.Calls.Single().Messages[0].Text.ShouldContain("Christmas / Candy Cane");
    }

    [Fact]
    public async Task SyncedPatterns_AreChosenBeforeAppMadeOnes()
    {
        var appMade = PatternDetail.Create("Christmas", "Candy Cane Mine", PatternMapper.FromDetailsDto(Details())).Value;
        appMade.MarkChanged();
        var synced = PatternDetail.Create("Christmas", "Christmas Tree", PatternMapper.FromDetailsDto(Details())).Value;
        synced.MarkSynced();
        _details.Setup(d => d.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { appMade, synced });
        var chat = new FakeChatClient().Reply(ValidReply);

        await Run(Sut(chat, maxExamples: 1), "candy cane");

        var system = chat.Calls.Single().Messages[0].Text;
        system.ShouldContain("Christmas / Christmas Tree");
        system.ShouldNotContain("Candy Cane Mine");
    }

    [Fact]
    public async Task ExampleLoadFailure_StillGenerates()
    {
        _details.Setup(d => d.ListAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));
        var chat = new FakeChatClient().Reply(ValidReply);

        (await Run(Sut(chat))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task MaxExamplesZero_DoesNotQueryRepository()
    {
        var chat = new FakeChatClient().Reply(ValidReply);
        await Run(Sut(chat, maxExamples: 0));
        _details.Verify(d => d.ListAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Success_IsAudited_WithPromptModelAndPattern()
    {
        var chat = new FakeChatClient().Reply(ValidReply);
        await Run(Sut(chat), "slow candy cane");

        var a = _audits.ShouldHaveSingleItem();
        a.ActionType.ShouldBe(GeneratePatternCommandHandler.AuditActionType);
        a.Success.ShouldBeTrue();
        a.PatternName.ShouldBe("Candy Cane Chase");
        a.Request.ShouldContain("\"Prompt\":\"slow candy cane\"");
        a.Request.ShouldContain("\"Model\":\"test-model\"");
        a.Request.ShouldContain("\"Refine\":false");
        a.Response.ShouldContain("\"SuggestedName\":\"Candy Cane Chase\"");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refine_IsAudited_WithCurrentName()
    {
        var chat = new FakeChatClient().Reply(ValidReply);
        var current = new FakeChatClient().Reply(ValidReply);
        var first = await Run(Sut(current));
        _audits.Clear();

        await Sut(chat).Handle(new GeneratePatternCommand("faster", first.Value.Details, "Candy Cane Chase"), default);

        var a = _audits.ShouldHaveSingleItem();
        a.Request.ShouldContain("\"Refine\":true");
        a.Request.ShouldContain("\"CurrentName\":\"Candy Cane Chase\"");
    }

    [Fact]
    public async Task UnusableReplies_AreAudited_WithTheLastReply()
    {
        var chat = new FakeChatClient().Reply("{}").Reply("""{"colors":[]}""");
        await Run(Sut(chat));

        var a = _audits.ShouldHaveSingleItem();
        a.Success.ShouldBeFalse();
        a.Error.ShouldNotBeNullOrWhiteSpace();
        a.Response.ShouldContain("\"Attempts\":2");
        a.Response.ShouldContain("colors");
    }

    [Fact]
    public async Task LongUnusableReply_IsTruncatedInTheAudit()
    {
        var junk = new string('x', GeneratePatternCommandHandler.MaxLoggedReplyLength + 500);
        var chat = new FakeChatClient().Reply(junk).Reply(junk);
        await Run(Sut(chat));

        var reply = JsonDocument.Parse(_audits.Single().Response).RootElement.GetProperty("LastReply").GetString()!;
        reply.Length.ShouldBe(GeneratePatternCommandHandler.MaxLoggedReplyLength + 1);
    }

    [Fact]
    public async Task ConnectionFailure_IsAudited()
    {
        var chat = new FakeChatClient().Throw(new HttpRequestException("connection refused"));
        await Run(Sut(chat));

        var a = _audits.ShouldHaveSingleItem();
        a.Success.ShouldBeFalse();
        a.Response.ShouldContain("\"Attempts\":1");
    }

    [Fact]
    public async Task Disabled_IsNotAudited()
    {
        await Run(Sut(new FakeChatClient(), enabled: false));
        _audits.ShouldBeEmpty();
    }

    [Fact]
    public async Task AuditFailure_DoesNotFailTheRequest()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));
        var chat = new FakeChatClient().Reply(ValidReply);

        var res = await Run(Sut(chat));

        res.IsSuccess.ShouldBeTrue();
    }
}
