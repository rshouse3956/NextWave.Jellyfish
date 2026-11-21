using Microsoft.AspNetCore.Http;

namespace JellyFish.Api.Common;

public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess) return Results.NoContent();

        return result.Error.Type switch
        {
            ErrorType.Validation => Results.BadRequest(new { error = result.Error.Description, code = result.Error.Code }),
            ErrorType.NotFound => Results.NotFound(new { error = result.Error.Description, code = result.Error.Code }),
            ErrorType.Conflict => Results.Conflict(new { error = result.Error.Description, code = result.Error.Code }),
            ErrorType.Unauthorized => Results.Unauthorized(),
            _ => Results.Problem(result.Error.Description)
        };
    }

    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ((Result)result).ToHttpResult();
    }
}

