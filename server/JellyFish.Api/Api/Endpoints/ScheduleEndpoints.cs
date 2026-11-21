using JellyFish.Api.Api.Endpoints;
using JellyFish.Api.Common;
using JellyFish.Api.Domain.Services;
using JellyFish.Api.Features.Schedule;
using JellyFish.Api.Features.Schedule.Commands;
using JellyFish.Api.Features.Schedule.Queries;
using MediatR;

namespace JellyFish.Api.Api.Endpoints;

public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/schedules").WithTags("Schedules");

        group.MapGet("/", async (IMediator mediator) =>
        {
            var res = await mediator.Send(new ListSchedulesQuery());
            return res.ToHttpResult();
        });


        group.MapGet("/{name}", async (string name, IMediator mediator) =>
        {
            var res = await mediator.Send(new GetScheduleQuery(name));
            return res.ToHttpResult();
        });

        group.MapPost("/", async (CreateScheduleCommand cmd, IMediator mediator) =>
        {
            var res = await mediator.Send(cmd);
            return res.ToHttpResult();
        });

        group.MapPut("/{name}", async (string name, UpdateScheduleCommand body, IMediator mediator) =>
        {
            var res = await mediator.Send(body with { Name = name });
            return res.ToHttpResult();
        });

        group.MapDelete("/{name}", async (string name, IMediator mediator) =>
        {
            var res = await mediator.Send(new DeleteScheduleCommand(name));
            return res.ToHttpResult();
        });

        group.MapPost("/{name}/deactivate-now", async (string name, IMediator mediator) =>
        {
            var res = await mediator.Send(new DeactivateScheduleNowCommand(name));
            return res.ToHttpResult();
        });

        group.MapGet("/active-windows", async (IMediator mediator) =>
        {
            var res = await mediator.Send(new ListActiveWindowsQuery());
            return res.ToHttpResult();
        });
        group.MapGet("/timeline-today", async (string? date, IMediator mediator) =>
        {
            DateOnly? d = null;
            if (!string.IsNullOrWhiteSpace(date))
            {
                if (DateOnly.TryParse(date, out var parsed)) d = parsed;
                else return Result.Failure<List<ScheduleTimelineItemDto>>(Error.Validation("Invalid date (expected yyyy-MM-dd).")).ToHttpResult();
            }

            var res = await mediator.Send(new GetTimelineTodayQuery(d));
            return res.ToHttpResult();
        });


        group.MapGet("/diagnostics", async (IMediator mediator, JellyFish.Api.Domain.Services.IScheduleNextEventService svc) =>
        {
            var listRes = await mediator.Send(new ListSchedulesQuery());
            if (listRes.IsFailure) return listRes.ToHttpResult();
            var nowUtc = DateTime.UtcNow;
            var items = new List<object>();
            foreach (var s in listRes.Value)
            {
                var diag = svc.GetDiagnostics(s.OnTime, s.OnDay, s.OffTime, s.OffDay, nowUtc);
                if (diag.IsFailure)
                {
                    items.Add(new { s.Name, s.FolderName, Error = diag.Error.Description });
                    continue;
                }
                items.Add(new {
                    s.Name,
                    s.FolderName,
                    s.PatternName,
                    s.Priority,
                    s.ZoneNames,
                    s.OnTime,
                    s.OnDay,
                    s.OffTime,
                    s.OffDay,
                    NowUtc = nowUtc,
                    diag.Value.PrevOnUtc,
                    diag.Value.PrevOffUtc,
                    diag.Value.NextOnUtc,
                    diag.Value.NextOffUtc,
                    diag.Value.IsActive,
                    diag.Value.NextOverallUtc,
                    diag.Value.NextKind,
                    diag.Value.TimeZoneIdResolved
                });
            }
            return Result.Success(items).ToHttpResult();
        });

        group.MapGet("/override", async (IMediator mediator) =>
        {
            var res = await mediator.Send(new GetManualOverrideQuery());
            return res.ToHttpResult();
        });

        group.MapPost("/override/clear", async (IMediator mediator) =>
        {
            var res = await mediator.Send(new ClearManualOverrideCommand());
            return res.ToHttpResult();
        });

        // Diagnostic endpoints for testing schedule logic
        group.MapPost("/debug/is-active", (IsActiveDebugRequest req, IScheduleNextEventService nextSvc) =>
        {
            var nowUtc = req.NowUtc ?? DateTime.UtcNow;
            var result = nextSvc.IsActiveAt(req.OnTime, req.OnDay, req.OffTime, req.OffDay, nowUtc);

            if (result.IsFailure)
            {
                return result.ToHttpResult();
            }

            var response = new
            {
                IsActive = result.Value,
                EvaluatedAt = nowUtc,
                OnTime = req.OnTime,
                OnDay = req.OnDay,
                OffTime = req.OffTime,
                OffDay = req.OffDay
            };

            return Result.Success(response).ToHttpResult();
        }).WithName("DebugIsActive");

        group.MapPost("/debug/multi-day-events", (MultiDayEventsDebugRequest req, IScheduleNextEventService nextSvc) =>
        {
            var fromUtc = req.FromUtc ?? DateTime.UtcNow;
            var toUtc = req.ToUtc ?? fromUtc.AddDays(7);

            var result = nextSvc.ComputeMultiDayEvents(req.OnTime, req.OnDay, req.OffTime, req.OffDay, fromUtc, toUtc);

            if (result.IsFailure)
            {
                return result.ToHttpResult();
            }

            var response = new
            {
                Events = result.Value.Select(e => new
                {
                    EventUtc = e.EventUtc,
                    Kind = e.Kind,
                    LocalDate = e.Date
                }).ToList(),
                FromUtc = fromUtc,
                ToUtc = toUtc,
                OnTime = req.OnTime,
                OnDay = req.OnDay,
                OffTime = req.OffTime,
                OffDay = req.OffDay,
                EventCount = result.Value.Count
            };

            return Result.Success(response).ToHttpResult();
        }).WithName("DebugMultiDayEvents");

        return app;
    }
}

public record IsActiveDebugRequest(
    string OnTime,
    string OnDay,
    string OffTime,
    string OffDay,
    DateTime? NowUtc = null
);

public record MultiDayEventsDebugRequest(
    string OnTime,
    string OnDay,
    string OffTime,
    string OffDay,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null
);

