using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Common;
using API.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ERP.SecurityTests;

public class ErrorTraceabilityAndCentralHandlingTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public ErrorTraceabilityAndCentralHandlingTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
    }

    private class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception, string Message)> Logs { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Logs.Add((logLevel, exception, formatter(state, exception)));
        }
    }

    [Fact]
    public async Task GlobalExceptionHandler_TryHandleAsync_LogsErrorAndReturnsStandardized500WithTraceId()
    {
        // Arrange
        var testLogger = new TestLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(testLogger);

        var context = new DefaultHttpContext();
        context.TraceIdentifier = "trace-test-corr-12345";
        context.Request.Path = "/api/test/fail";
        context.Request.Method = "POST";

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var exception = new InvalidOperationException("Internal database timeout or deadlock details");

        // Act
        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);

        responseStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseStream);
        string responseJson = await reader.ReadToEndAsync();

        // Must NOT leak internal exception message or stack trace
        Assert.DoesNotContain("database timeout", responseJson);
        Assert.DoesNotContain("deadlock", responseJson);
        Assert.DoesNotContain("InvalidOperationException", responseJson);
        Assert.DoesNotContain("stack", responseJson, StringComparison.OrdinalIgnoreCase);

        // Must deserialize into standard ApiResponse envelope
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(responseJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(apiResponse);
        Assert.False(apiResponse.Succeeded);
        Assert.Equal("INTERNAL_SERVER_ERROR", apiResponse.ErrorCode);
        Assert.Equal("An unexpected error occurred while processing your request.", apiResponse.Message);
        Assert.Equal("trace-test-corr-12345", apiResponse.TraceId);

        // Verify ILogger captured the exception and correlation trace ID
        Assert.Single(testLogger.Logs);
        var log = testLogger.Logs[0];
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.Same(exception, log.Exception);
        Assert.Contains("trace-test-corr-12345", log.Message);
        Assert.Contains("/api/test/fail", log.Message);
    }

    [Fact]
    public async Task GlobalExceptionHandler_TryHandleAsync_BadHttpRequestException_Returns400WithTraceId()
    {
        // Arrange
        var testLogger = new TestLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(testLogger);

        var context = new DefaultHttpContext();
        context.TraceIdentifier = "trace-bad-request-888";
        context.Request.Path = "/api/test/malformed";
        context.Request.Method = "POST";

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var badHttpException = new BadHttpRequestException("Malformed JSON syntax", 400);

        // Act
        bool handled = await handler.TryHandleAsync(context, badHttpException, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        responseStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseStream);
        string responseJson = await reader.ReadToEndAsync();

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(responseJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(apiResponse);
        Assert.False(apiResponse.Succeeded);
        Assert.Equal("BAD_REQUEST", apiResponse.ErrorCode);
        Assert.Equal("The request payload is invalid.", apiResponse.Message);
        Assert.Equal("trace-bad-request-888", apiResponse.TraceId);
    }

    [Fact]
    public async Task HttpPipeline_UnexpectedException_ReturnsStandardized500AndTraceId_WithoutInformationLeak()
    {
        _fixture.EnsureDatabasesInitialized();
        var client = _fixture.CreateClient();

        // Call the simulated error endpoint that throws an unhandled InvalidOperationException
        var response = await client.GetAsync("/api/security/error-simulation-test");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        string rawContent = await response.Content.ReadAsStringAsync();

        // 1. Must NOT leak exception details
        Assert.DoesNotContain("Simulated unexpected", rawContent);
        Assert.DoesNotContain("InvalidOperationException", rawContent);
        Assert.DoesNotContain("stack", rawContent, StringComparison.OrdinalIgnoreCase);

        // 2. Must adhere to ApiResponse<T> contract
        var apiResponse = await response.ReadApiResponseAsync<object>();
        Assert.NotNull(apiResponse);
        Assert.False(apiResponse.Succeeded);
        Assert.Equal("INTERNAL_SERVER_ERROR", apiResponse.ErrorCode);
        Assert.Equal("An unexpected error occurred while processing your request.", apiResponse.Message);

        // 3. Must have a populated TraceId for log correlation
        Assert.False(string.IsNullOrWhiteSpace(apiResponse.TraceId));
    }

    [Fact]
    public void BaseApiController_InternalErrorResponse_PopulatesTraceIdFromHttpContext()
    {
        var controller = new SampleController();
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "ctrl-trace-corr-777";

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = context
        };

        var result = controller.TriggerInternalError("Test internal error");

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);

        var apiResponse = Assert.IsType<ApiResponse<object?>>(objectResult.Value);
        Assert.False(apiResponse.Succeeded);
        Assert.Equal("INTERNAL_SERVER_ERROR", apiResponse.ErrorCode);
        Assert.Equal("Test internal error", apiResponse.Message);
        Assert.Equal("ctrl-trace-corr-777", apiResponse.TraceId);
    }

    [Fact]
    public async Task AuthenticationAndAuthorization_StatusSemanticsPreserved()
    {
        _fixture.EnsureDatabasesInitialized();
        var client = _fixture.CreateClient();

        // 401 Unauthorized for unauthenticated access to protected endpoint
        var unauthResponse = await client.GetAsync("/api/security/protected-test");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResponse.StatusCode);

        // 403 Forbidden for authenticated user lacking required role
        var forbiddenClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantUserAlphaId,
            "tenant-user-alpha",
            TestConstants.TenantAlphaId,
            new[] { "Tenant User" },
            new[] { "PRODUCT.VIEW" });

        var forbiddenResponse = await forbiddenClient.GetAsync("/api/security/platform-admin-test");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    private class SampleController : BaseApiController
    {
        public IActionResult TriggerInternalError(string message)
        {
            return InternalErrorResponse(message);
        }
    }
}
