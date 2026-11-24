using RestaurantCms.Domain.Common;

namespace RestaurantCms.Api.Endpoints;

/// <summary>Maps use-case results to HTTP: 200/201/204 on success, RFC 7807 problem details on failure.</summary>
public static class ResultHttp
{
    public static IResult ToOk<T>(this Result<T> result) => result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error!);

    public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
        result.IsSuccess ? Results.Created(location(result.Value), result.Value) : Problem(result.Error!);

    public static IResult ToNoContent<T>(this Result<T> result) => result.IsSuccess ? Results.NoContent() : Problem(result.Error!);

    public static IResult Problem(Error error)
    {
        if (error.Fields is { Count: > 0 } fields)
            return Results.ValidationProblem(fields.ToDictionary(f => f.Key, f => f.Value), title: error.Message,
                extensions: new Dictionary<string, object?> { ["code"] = error.Code });

        var status = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        };
        return Results.Problem(title: error.Message, statusCode: status, extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
