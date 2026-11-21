namespace JellyFish.Api.Common;

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    Problem = 2,
    NotFound = 3,
    Conflict = 4,
    Unauthorized = 5
}

public record Error
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
    public static readonly Error NullValue = new(
        "General.Null",
        "Null value was provided",
        ErrorType.Failure);

    public Error(string code, string description, ErrorType type)
    {
        Code = code;
        Description = description;
        Type = type;
    }

    public string Code { get; }

    public string Description { get; }

    public ErrorType Type { get; }

    public static Error Failure(string code, string description) =>
        new(code, description, ErrorType.Failure);

    public static Error NotFound(string description) =>
        new($"{nameof(ErrorType.NotFound)}.100", description, ErrorType.NotFound);

    public static Error Problem(string description) =>
        new($"{nameof(ErrorType.Problem)}.100", description, ErrorType.Problem);

    public static Error Conflict(string description) =>
        new($"{nameof(ErrorType.Conflict)}.100", description, ErrorType.Conflict);

    public static Error Unauthorized(string description) =>
        new($"{nameof(ErrorType.Unauthorized)}.100", description, ErrorType.Unauthorized);

    public static Error Validation(string description) =>
        new($"{nameof(ErrorType.Validation)}.100", description, ErrorType.Validation);
}

