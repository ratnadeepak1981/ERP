namespace API.Contracts;

public class ServiceResult<T>
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public T? Data { get; init; }
    public List<string> Errors { get; init; } = [];
    public string? ErrorCode { get; init; }
    public ServiceErrorType ErrorType { get; init; } = ServiceErrorType.None;

    public static ServiceResult<T> Ok(
        T data,
        string? message = null)
    {
        return new ServiceResult<T>
        {
            Success = true,
            Message = message,
            Data = data,
            Errors = []
        };
    }

    public static ServiceResult<T> Fail(
        string message,
        IEnumerable<string>? errors = null,
        string? errorCode = null,
        ServiceErrorType errorType = ServiceErrorType.Failure)
    {
        return new ServiceResult<T>
        {
            Success = false,
            Message = message,
            Data = default,
            Errors = errors?.ToList() ?? [],
            ErrorCode = errorCode,
            ErrorType = errorType
        };
    }

    public static ServiceResult<T> NotFound(string message = "Resource not found.") =>
        Fail(message, null, "NOT_FOUND", ServiceErrorType.NotFound);

    public static ServiceResult<T> Conflict(string message) =>
        Fail(message, null, "CONFLICT", ServiceErrorType.Conflict);

    public static ServiceResult<T> Validation(string message, IEnumerable<string>? errors = null) =>
        Fail(message, errors, "VALIDATION_ERROR", ServiceErrorType.Validation);

    public static ServiceResult<T> Unauthorized(string message = "Unauthorized access.") =>
        Fail(message, null, "UNAUTHORIZED", ServiceErrorType.Unauthorized);

    public static ServiceResult<T> Forbidden(string message = "Forbidden access.") =>
        Fail(message, null, "FORBIDDEN", ServiceErrorType.Forbidden);
}

public static class ServiceResult
{
    public static ServiceResult<T> Ok<T>(T data, string? message = null) =>
        ServiceResult<T>.Ok(data, message);

    public static ServiceResult<object?> Ok(string? message = null) =>
        ServiceResult<object?>.Ok(null, message);

    public static ServiceResult<object?> Fail(
        string message,
        IEnumerable<string>? errors = null,
        string? errorCode = null,
        ServiceErrorType errorType = ServiceErrorType.Failure) =>
        ServiceResult<object?>.Fail(message, errors, errorCode, errorType);
}

public enum ServiceErrorType
{
    None,
    Failure,
    NotFound,
    Conflict,
    Validation,
    Unauthorized,
    Forbidden
}