using System.Net;
using JellyFish.Api.Features.Pattern;
using JellyFish.Api.Features.PatternAssistant;
using Microsoft.Extensions.AI;

namespace JellyFish.Api.Tests.Features.PatternAssistant;

internal static class PatternAssistantTestData
{
    public static PatternDetailsDto Details(string type = "Chase", int speed = 10, int[]? colors = null) => new(
        Colors: colors ?? new[] { 255, 0, 0, 255, 255, 255 },
        SpaceBetweenPixels: 4,
        EffectBetweenPixels: "No Color Transform",
        ColorPos: null,
        SoffitZone: null,
        Cursor: null,
        Type: type,
        Skip: 1,
        NumOfLeds: 1,
        RunData: new PatternRunDataDto(speed, 100, "No Effect", 0, new[] { 100, 100, 100 }),
        Direction: "Left");

    public const string ValidReply = """
        {"suggestedName":"Candy Cane Chase","explanation":"Red and white blocks moving slowly.",
         "colors":[{"r":255,"g":0,"b":0},{"r":255,"g":255,"b":255}],
         "type":"Chase","effectBetweenPixels":"No Color Transform","spaceBetweenPixels":4,
         "skip":1,"numOfLeds":1,"speedLevel":3,"brightness":100,"direction":"Right"}
        """;
}

/// <summary>Returns queued replies (or throws queued exceptions) and records every call.</summary>
internal sealed class FakeChatClient : IChatClient
{
    private readonly Queue<Func<ChatResponse>> _replies = new();
    public List<(List<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];

    public FakeChatClient Reply(string text)
    {
        _replies.Enqueue(() => new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
        return this;
    }

    public FakeChatClient Throw(Exception ex)
    {
        _replies.Enqueue(() => throw ex);
        return this;
    }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Calls.Add((messages.ToList(), options));
        return Task.FromResult(_replies.Dequeue()());
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

/// <summary>An HttpMessageHandler that records requests and answers with a fixed response.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
    public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

    public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public static StubHttpHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, body));
        return _respond(request);
    }
}
