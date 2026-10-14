using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace API.Contracts;

public sealed class ApiResponse<T>
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public List<string> Errors { get; init; } = [];
    public string? ErrorCode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; init; }

    public ApiResponse() { }

    public static ApiResponse<T> Success(
        T? data,
        string message = "Request completed successfully.",
        string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Succeeded = true,
            Message = message,
            Data = data,
            Errors = [],
            TraceId = traceId
        };
    }

    public static ApiResponse<T> Failure(
        string message,
        IEnumerable<string>? errors = null,
        string? errorCode = null,
        string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Succeeded = false,
            Message = message,
            Data = default,
            Errors = errors?.ToList() ?? [],
            ErrorCode = errorCode,
            TraceId = traceId
        };
    }
}

public static class ApiResponse
{
    public static ApiResponse<T> Success<T>(
        T? data,
        string message = "Request completed successfully.",
        string? traceId = null)
    {
        return ApiResponse<T>.Success(data, message, traceId);
    }

    public static ApiResponse<object?> Success(
        string message = "Request completed successfully.")
    {
        return ApiResponse<object?>.Success(null, message, null);
    }

    public static ApiResponse<object?> Failure(
        string message,
        IEnumerable<string>? errors = null,
        string? errorCode = null,
        string? traceId = null)
    {
        return ApiResponse<object?>.Failure(message, errors, errorCode, traceId);
    }
}