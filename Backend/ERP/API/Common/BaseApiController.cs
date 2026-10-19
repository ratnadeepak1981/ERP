using API.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Common;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected IActionResult Success<T>(
        T? data,
        string message = "Request completed successfully.")
    {
        return SuccessResponse(data, message);
    }

    protected IActionResult Success(
        string message = "Request completed successfully.")
    {
        return SuccessResponse(message);
    }

    protected IActionResult Failure(
        int statusCode,
        string message,
        IEnumerable<string>? errors = null,
        string? errorCode = null,
        string? traceId = null)
    {
        traceId ??= HttpContext?.TraceIdentifier;
        return StatusCode(
            statusCode,
            ApiResponse<object?>.Failure(message, errors, errorCode, traceId));
    }

    protected IActionResult SuccessResponse<T>(
        T? data,
        string message = "Request completed successfully.")
    {
        return Ok(ApiResponse<T>.Success(data, message));
    }

    protected IActionResult SuccessResponse(
        string message = "Request completed successfully.")
    {
        return Ok(ApiResponse<object?>.Success(null, message));
    }

    protected IActionResult CreatedResponse<T>(
        string? actionName,
        object? routeValues,
        T? data,
        string message = "Resource created successfully.")
    {
        if (!string.IsNullOrWhiteSpace(actionName))
        {
            return CreatedAtAction(
                actionName,
                routeValues,
                ApiResponse<T>.Success(data, message));
        }

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<T>.Success(data, message));
    }

    protected IActionResult CreatedResponse<T>(
        T? data,
        string message = "Resource created successfully.")
    {
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<T>.Success(data, message));
    }

    protected IActionResult BadRequestResponse(
        string message = "Bad request.",
        IEnumerable<string>? errors = null,
        string? errorCode = "BAD_REQUEST")
    {
        return BadRequest(
            ApiResponse<object?>.Failure(message, errors, errorCode, HttpContext?.TraceIdentifier));
    }

    protected IActionResult NotFoundResponse(
        string message = "Resource not found.",
        string? errorCode = "NOT_FOUND")
    {
        return NotFound(
            ApiResponse<object?>.Failure(message, null, errorCode, HttpContext?.TraceIdentifier));
    }

    protected IActionResult ConflictResponse(
        string message = "Conflict occurred.",
        IEnumerable<string>? errors = null,
        string? errorCode = "CONFLICT")
    {
        return Conflict(
            ApiResponse<object?>.Failure(message, errors, errorCode, HttpContext?.TraceIdentifier));
    }

    protected IActionResult UnauthorizedResponse(
        string message = "Unauthorized access.",
        string? errorCode = "UNAUTHORIZED")
    {
        return Unauthorized(
            ApiResponse<object?>.Failure(message, null, errorCode, HttpContext?.TraceIdentifier));
    }

    protected IActionResult ForbiddenResponse(
        string message = "Forbidden access.",
        string? errorCode = "FORBIDDEN")
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            ApiResponse<object?>.Failure(message, null, errorCode, HttpContext?.TraceIdentifier));
    }

    protected IActionResult InternalErrorResponse(
        string message = "An unexpected error occurred.",
        string? errorCode = "INTERNAL_SERVER_ERROR",
        Exception? ex = null)
    {
        var traceId = HttpContext?.TraceIdentifier;
        if (ex != null && HttpContext != null)
        {
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Microsoft.Extensions.Logging.ILogger<BaseApiController>>(HttpContext.RequestServices);
            logger?.LogError(ex, "Internal server error occurred. TraceId: {TraceId}", traceId);
        }

        return StatusCode(
            StatusCodes.Status500InternalServerError,
            ApiResponse<object?>.Failure(message, null, errorCode, traceId));
    }

    protected IActionResult FromResult<T>(
        ServiceResult<T> result,
        string? successMessage = null)
    {
        if (result.Success)
        {
            return SuccessResponse(
                result.Data,
                successMessage ?? result.Message ?? "Request completed successfully.");
        }

        return result.ErrorType switch
        {
            ServiceErrorType.NotFound =>
                NotFoundResponse(result.Message ?? "Resource not found.", result.ErrorCode),
            ServiceErrorType.Conflict =>
                ConflictResponse(result.Message ?? "Conflict occurred.", result.Errors, result.ErrorCode),
            ServiceErrorType.Validation =>
                BadRequestResponse(result.Message ?? "Validation failed.", result.Errors, result.ErrorCode ?? "VALIDATION_ERROR"),
            ServiceErrorType.Unauthorized =>
                UnauthorizedResponse(result.Message ?? "Unauthorized access.", result.ErrorCode),
            ServiceErrorType.Forbidden =>
                ForbiddenResponse(result.Message ?? "Forbidden access.", result.ErrorCode),
            _ =>
                BadRequestResponse(result.Message ?? "Operation failed.", result.Errors, result.ErrorCode)
        };
    }

    protected IActionResult PaginateAndSort<T>(
        IEnumerable<T> source,
        int? page,
        int? pageSize,
        string? sortBy,
        string? sortDirection,
        API.Common.Sorting.SortMapping<T>? sortMapping = null)
    {
        if (sortMapping != null && (!string.IsNullOrWhiteSpace(sortBy) || !string.IsNullOrWhiteSpace(sortDirection)))
        {
            if (!sortMapping.Validate(sortBy, sortDirection, out var sortError))
            {
                return BadRequestResponse(sortError!, null, "INVALID_SORT");
            }

            source = sortMapping.Apply(source, sortBy, sortDirection);
        }
        else if (sortMapping != null)
        {
            source = sortMapping.Apply(source, null, null);
        }

        if (page.HasValue || pageSize.HasValue)
        {
            var paginationReq = new API.Contracts.Pagination.PaginationRequest
            {
                Page = page ?? 1,
                PageSize = pageSize ?? API.Contracts.Pagination.PaginationRequest.DefaultPageSize,
                SortBy = sortBy,
                SortDirection = sortDirection ?? "asc"
            };

            var paged = API.Common.Pagination.PaginationExtensions.ToPagedResponse(source, paginationReq);
            return SuccessResponse(paged);
        }

        return SuccessResponse(source as IReadOnlyList<T> ?? source.ToList());
    }

    protected IActionResult HandleDomainException(Exception ex)
    {
        return ex switch
        {
            KeyNotFoundException knf =>
                NotFoundResponse(knf.Message),
            ArgumentException arg =>
                BadRequestResponse(arg.Message),
            InvalidOperationException inv =>
                ConflictResponse(inv.Message),
            UnauthorizedAccessException unauth =>
                UnauthorizedResponse(unauth.Message),
            _ =>
                InternalErrorResponse("An unexpected error occurred.", "INTERNAL_SERVER_ERROR", ex)
        };
    }
}