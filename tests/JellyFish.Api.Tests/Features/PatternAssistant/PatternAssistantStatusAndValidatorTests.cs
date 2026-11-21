using System.Net;
using System.Text.Json;
using FluentValidation.TestHelper;
using JellyFish.Api.Features.PatternAssistant;
using JellyFish.Api.Features.PatternAssistant.Commands;
using JellyFish.Api.Features.PatternAssistant.Queries;
using JellyFish.Api.Infrastructure.Services.PatternAssistant;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OllamaSharp;
using Shouldly;
using static JellyFish.Api.Tests.Features.PatternAssistant.PatternAssistantTestData;

namespace JellyFish.Api.Tests.Features.PatternAssistant;

public class PatternAssistantStatusTests
{
    private const string Tags = """{"models":[{"name":"qwen2.5:7b"},{"name":"gemma3:latest"}]}""";

    private static async Task<PatternAssistantStatusDto> Status(HttpMessageHandler handler, bool enabled = true, string model = "qwen2.5:7b")
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(PatternAssistantOptions.HttpClientName))
            .Returns(() => new HttpClient(handler) { BaseAddress = new Uri("http://ollama:11434/") });
        var sut = new GetPatternAssistantStatusQueryHandler(
            factory.Object,
            Options.Create(new PatternAssistantOptions { Enabled = enabled, Model = model, BaseUrl = "http://ollama:11434" }),
            NullLogger<GetPatternAssistantStatusQueryHandler>.Instance);
        var res = await sut.Handle(new GetPatternAssistantStatusQuery(), default);
        res.IsSuccess.ShouldBeTrue();
        return res.Value;
    }

    [Fact]
    public async Task Disabled_DoesNotProbe()
    {
        var handler = StubHttpHandler.Json(Tags);
        var s = await Status(handler, enabled: false);
        s.Enabled.ShouldBeFalse();
        s.Ready.ShouldBeFalse();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ModelInstalled_IsReady()
    {
        var handler = StubHttpHandler.Json(Tags);
        var s = await Status(handler);
        s.Ready.ShouldBeTrue();
        s.Message.ShouldBeNull();
        handler.Requests.Single().Request.RequestUri!.ToString().ShouldBe("http://ollama:11434/api/tags");
    }

    [Fact]
    public async Task UntaggedModel_MatchesLatest()
    {
        (await Status(StubHttpHandler.Json(Tags), model: "gemma3")).ModelInstalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ModelMissing_ExplainsPull()
    {
        var s = await Status(StubHttpHandler.Json(Tags), model: "llama3.1:8b");
        s.Reachable.ShouldBeTrue();
        s.ModelInstalled.ShouldBeFalse();
        s.Message!.ShouldContain("ollama pull llama3.1:8b");
    }

    [Fact]
    public async Task Unreachable_IsReportedNotThrown()
    {
        var s = await Status(new StubHttpHandler(_ => throw new HttpRequestException("refused")));
        s.Reachable.ShouldBeFalse();
        s.Message!.ShouldContain("Could not reach");
    }

    [Fact]
    public async Task ErrorStatus_IsReported()
    {
        var s = await Status(StubHttpHandler.Json("{}", HttpStatusCode.InternalServerError));
        s.Reachable.ShouldBeFalse();
        s.Message!.ShouldContain("500");
    }
}

public class GeneratePatternCommandValidatorTests
{
    private readonly GeneratePatternCommandValidator _v = new();

    [Fact]
    public void Valid_Passes() =>
        _v.TestValidate(new GeneratePatternCommand("candy cane", Details(), "Name", ["first"])).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyPrompt_Fails(string prompt) =>
        _v.TestValidate(new GeneratePatternCommand(prompt)).ShouldHaveValidationErrorFor(x => x.Prompt);

    [Fact]
    public void LongPrompt_Fails() =>
        _v.TestValidate(new GeneratePatternCommand(new string('x', 501))).ShouldHaveValidationErrorFor(x => x.Prompt);

    [Fact]
    public void TooMuchHistory_Fails() =>
        _v.TestValidate(new GeneratePatternCommand("x", History: Enumerable.Repeat("h", 11).ToList()))
            .ShouldHaveValidationErrorFor(x => x.History);

    [Fact]
    public void BadCurrentDetailsStructure_Fails() =>
        _v.TestValidate(new GeneratePatternCommand("x", Details(colors: new[] { 1, 2 })))
            .ShouldHaveValidationErrorFor("CurrentDetails.Colors");

    [Fact]
    public void UndocumentedCurrentType_IsAllowed() =>
        _v.TestValidate(new GeneratePatternCommand("x", Details(type: "Soffit"))).ShouldNotHaveAnyValidationErrors();
}

/// <summary>Checks the real OllamaSharp client sends the schema as Ollama's "format" (structured outputs).</summary>
public class OllamaRequestFormatTests
{
    [Fact]
    public async Task JsonSchemaResponseFormat_IsSentAsOllamaFormat()
    {
        const string reply = """
            {"model":"qwen2.5:7b","created_at":"2026-11-18T00:00:00Z","message":{"role":"assistant","content":"{}"},"done":true,"done_reason":"stop"}
            """;
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(reply + "\n", System.Text.Encoding.UTF8, "application/x-ndjson")
        });
        IChatClient client = new OllamaApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://ollama:11434/") }, "qwen2.5:7b");

        await client.GetResponseAsync("hi", new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(AssistantPatternSchema.Schema, "jellyfish_pattern")
        });

        var (request, body) = handler.Requests.Single();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/chat");
        using var doc = JsonDocument.Parse(body!);
        var format = doc.RootElement.GetProperty("format");
        format.ValueKind.ShouldBe(JsonValueKind.Object);
        format.GetProperty("properties").TryGetProperty("speedLevel", out _).ShouldBeTrue();
    }
}
