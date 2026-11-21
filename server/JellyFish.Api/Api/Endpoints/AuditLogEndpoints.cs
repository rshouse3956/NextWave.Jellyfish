using JellyFish.Api.Common;
using JellyFish.Api.Features.AuditLog.Queries;
using MediatR;

namespace JellyFish.Api.Api.Endpoints;

public static class AuditLogEndpoints
{
    public static IEndpointRouteBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit-logs").WithTags("AuditLogs");

        group.MapGet("/", async (
            string? actionType,
            string? sourceType,
            DateTime? startDateUtc,
            DateTime? endDateUtc,
            int? skip,
            int? take,
            IMediator mediator) =>
        {
            var query = new ListAuditLogsQuery(
                ActionType: actionType,
                SourceType: sourceType,
                StartDateUtc: startDateUtc,
                EndDateUtc: endDateUtc,
                Skip: skip ?? 0,
                Take: take ?? 50
            );
            var res = await mediator.Send(query);
            return res.ToHttpResult();
        });

        group.MapGet("/stats", async (int? tzOffsetMinutes, IMediator mediator) =>
        {
            var res = await mediator.Send(new GetAuditLogStatsQuery(tzOffsetMinutes));
            return res.ToHttpResult();
        });

        group.MapGet("/{id}", async (string id, IMediator mediator) =>
        {
            var res = await mediator.Send(new GetAuditLogQuery(id));
            return res.ToHttpResult();
        });

        return app;
    }
}

