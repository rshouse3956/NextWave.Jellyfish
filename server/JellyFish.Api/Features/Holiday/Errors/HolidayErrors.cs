using JellyFish.Api.Common;

namespace JellyFish.Api.Features.Holiday.Errors;

public static class HolidayErrors
{
    public static Error NameRequired() => Error.Validation("Holiday name is required");
    public static Error CategoryInvalid(string category) => Error.Validation($"Invalid category '{category}'");
    public static Error CronInvalid(string expr) => Error.Validation($"Invalid cron expression '{expr}'");
    public static Error NotFound(string name) => Error.NotFound($"Holiday '{name}' not found");
    public static Error AlreadyExists(string name) => Error.Conflict($"Holiday '{name}' already exists");
}

