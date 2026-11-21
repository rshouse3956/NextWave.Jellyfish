using FluentValidation.Results;

namespace JellyFish.Api.Common;

public sealed record ValidationError : Error
{
    public ValidationError(Error[] errors)
        : base(
            "Validation.General",
            "One or more validation errors occurred",
            ErrorType.Validation)
    {
        Errors = errors;
        ValidationFailures = [];
    }

    public ValidationError(ValidationFailure[] validationFailures)
        : base(
            "Validation.General",
            "One or more validation errors occurred",
            ErrorType.Validation)
    {
        ValidationFailures = validationFailures;
        Errors = validationFailures.Select(f => Error.Problem(f.ErrorMessage)).ToArray();
    }

    public Error[] Errors { get; }
    public ValidationFailure[] ValidationFailures { get; }

    public static ValidationError FromResults(IEnumerable<Result> results) =>
        new(results.Where(r => r.IsFailure).Select(r => r.Error).ToArray());
}

