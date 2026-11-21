using JellyFish.Api.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace JellyFish.Api.Api.Endpoints;

public static class HealthReadinessEndpoints
{
    public static IEndpointRouteBuilder MapHealthzEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/healthz/live", () => Results.Ok(new { status = "live" }))
           .WithName("Liveness");

        app.MapGet("/healthz/ready", async (IOptions<MongoSettings> opts) =>
        {
            try
            {
                var client = new MongoClient(opts.Value.ConnectionString);
                var db = client.GetDatabase(opts.Value.DatabaseName);
                var cmd = new BsonDocument("ping", 1);
                await db.RunCommandAsync<BsonDocument>(cmd);
                return Results.Ok(new { status = "ready" });
            }
            catch
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        })
        .WithName("Readiness");

        return app;
    }
}

