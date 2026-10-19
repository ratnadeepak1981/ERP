using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Contracts;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace API.Common;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        // Log unexpected exception with full stack trace and correlation trace identifier
        _logger.LogError(
            exception,
            "Unhandled exception caught by GlobalExceptionHandler. TraceId: {TraceId}, Path: {Path}, Method: {Method}",
            traceId,
            httpContext.Request.Path,
            httpContext.Request.Method);

        if (httpContext.Response.HasStarted)
        {
            _logger.LogWarning("Response has already started; unable to write standardized error envelope for TraceId: {TraceId}", traceId);
            return false;
        }

        int statusCode = StatusCodes.Status500InternalServerError;
        string errorCode = "INTERNAL_SERVER_ERROR";
        string safeMessage = "An unexpected error occurred while processing your request.";

        // Preserve 400 semantics for framework-generated bad request exceptions (e.g. malformed JSON / request body)
        if (exception is BadHttpRequestException badHttpEx)
        {
            statusCode = badHttpEx.StatusCode;
            errorCode = "BAD_REQUEST";
            safeMessage = "The request payload is invalid.";
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        var responsePayload = ApiResponse<object?>.Failure(
            message: safeMessage,
            errors: null,
            errorCode: errorCode,
            traceId: traceId);

        await httpContext.Response.WriteAsJsonAsync(responsePayload, _jsonOptions, cancellationToken);

        return true;
    }
}
