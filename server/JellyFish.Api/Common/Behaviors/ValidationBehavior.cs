using FluentValidation;
using FluentValidation.Results;
using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = validationResults.SelectMany(r => r.Errors).Where(f => f is not null).ToArray();

        if (failures.Length == 0)
            return await next();

        var error = new ValidationError(failures);

        // Try to construct a Result/Result<T> failure matching TResponse
        var tResp = typeof(TResponse);
        if (tResp == typeof(Result))
        {
            object boxed = Result.Failure(error);
            return (TResponse)boxed;
        }
        if (tResp.IsGenericType && tResp.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var method = typeof(Result).GetMethod(nameof(Result.Failure), new[] { typeof(Error) })!;
            var generic = method.MakeGenericMethod(tResp.GetGenericArguments()[0]);
            object boxed = generic.Invoke(null, new object[] { error })!;
            return (TResponse)boxed;
        }

        // If response type is not Result, just continue (or throw). We continue to avoid breaking endpoints silently.
        return await next();
    }
}

