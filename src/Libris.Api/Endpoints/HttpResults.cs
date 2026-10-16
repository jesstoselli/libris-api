using Libris.Application.Common;

namespace Libris.Api.Endpoints;

public static class HttpResults
{
    public static IResult ToHttpResult<T>(OperationResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess ? Results.Ok(result.Value) : ToError(result);
    }

    public static IResult ToCreatedResult<T>(OperationResult<T> result, Func<T, string> location)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(location);

        return result.IsSuccess ? Results.Created(location(result.Value!), result.Value) : ToError(result);
    }

    public static IResult ToNoContentResult<T>(OperationResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess ? Results.NoContent() : ToError(result);
    }

    private static IResult ToError<T>(OperationResult<T> result)
    {
        var status = result.ErrorCode switch
        {
            "email_already_registered" => StatusCodes.Status409Conflict,
            "invalid_credentials" => StatusCodes.Status401Unauthorized,
            "invalid_refresh_token" => StatusCodes.Status401Unauthorized,
            "signup_disabled" => StatusCodes.Status403Forbidden,
            "not_found" => StatusCodes.Status404NotFound,
            "genre_already_exists" => StatusCodes.Status409Conflict,
            "isbn_already_registered" => StatusCodes.Status409Conflict,
            "already_owned" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return Results.Json(new { error = result.ErrorCode, message = result.ErrorMessage }, statusCode: status);
    }
}
