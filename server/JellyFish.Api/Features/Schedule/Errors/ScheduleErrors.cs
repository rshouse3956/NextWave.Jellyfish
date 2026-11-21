using JellyFish.Api.Common;

namespace JellyFish.Api.Features.Schedule.Errors;

public static class ScheduleErrors
{
    public static Error NotFound(string name) => Error.NotFound($"Schedule '{name}' not found");
    public static Error AlreadyExists(string name) => Error.Conflict($"Schedule '{name}' already exists");
    public static Error PriorityInvalid(string p) => Error.Validation($"Invalid priority '{p}'");
    public static Error CronInvalid(string expr) => Error.Validation($"Invalid cron expression '{expr}'");
}

