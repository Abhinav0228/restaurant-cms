namespace RestaurantCms.Domain.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden
}

public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Validation)
{
    /// <summary>Field-level messages for validation failures (field name → messages).</summary>
    public IReadOnlyDictionary<string, string[]>? Fields { get; init; }

    public static Error Invalid(IReadOnlyDictionary<string, string[]> fields) =>
        new("Validation", "Some fields need attention.", ErrorType.Validation) { Fields = fields };
    public static Error Forbidden(string message) => new("Auth.Forbidden", message, ErrorType.Forbidden);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string entity, object id) => new($"{entity}.NotFound", $"{entity} '{id}' was not found.", ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string message) => new("Auth.Unauthorized", message, ErrorType.Unauthorized);
}

public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    public static Result Success() => new(true, null);
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, bool isSuccess, Error? error) : base(isSuccess, error) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read Value of a failed result ({Error?.Code}).");

    public static Result<T> Success(T value) => new(value, true, null);
    public new static Result<T> Failure(Error error) => new(default, false, error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}
