using MediatR;
using Microsoft.Extensions.Logging;
using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Persistence;

namespace JellyFish.Api.Common.Behaviors;

public sealed class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>, ITransactionalRequest
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UnitOfWorkBehavior<TRequest, TResponse>> _logger;

    public UnitOfWorkBehavior(IUnitOfWork uow, ILogger<UnitOfWorkBehavior<TRequest, TResponse>> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();

        // Only commit if the response is a Result/Result<T> and is success
        if (response is Result r)
        {
            if (r.IsSuccess)
            {
                await _uow.SaveChangesAsync(cancellationToken);
            }
            return response;
        }

        var t = typeof(TResponse);
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var isSuccessProp = t.GetProperty(nameof(Result.IsSuccess));
            var isSuccess = (bool)(isSuccessProp?.GetValue(response) ?? false);
            if (isSuccess)
            {
                await _uow.SaveChangesAsync(cancellationToken);
            }
        }

        return response;
    }
}

