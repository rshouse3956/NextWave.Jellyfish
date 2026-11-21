using System.Runtime.CompilerServices;
using System.Threading.Channels;
using JellyFish.Api.Infrastructure.Notifications;
using Microsoft.AspNetCore.Http;

namespace JellyFish.Api.Api.Endpoints;

public static class EventsEndpoints
{
    public static IEndpointRouteBuilder MapEventsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events").WithTags("Events");

        group.MapGet("/state", async (HttpContext ctx, IStateChangeBroadcaster broadcaster) =>
        {
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.Connection = "keep-alive";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";
            ctx.Response.ContentType = "text/event-stream";

            var reader = broadcaster.Subscribe(ctx.RequestAborted);

            // Initial comment to open stream
            await ctx.Response.WriteAsync(": connected\n\n");
            await ctx.Response.Body.FlushAsync();

            await foreach (var msg in ReadAllAsync(reader, ctx.RequestAborted))
            {
                await ctx.Response.WriteAsync($"data: {msg}\n\n");
                await ctx.Response.Body.FlushAsync();
            }
        });

        return app;
    }

    private static async IAsyncEnumerable<string> ReadAllAsync(ChannelReader<string> reader, [EnumeratorCancellation] CancellationToken ct)
    {
        while (await reader.WaitToReadAsync(ct))
        {
            while (reader.TryRead(out var msg))
            {
                yield return msg;
            }
        }
    }
}

