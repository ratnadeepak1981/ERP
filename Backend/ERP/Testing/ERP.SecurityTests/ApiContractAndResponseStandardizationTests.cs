using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using API.Common;
using API.Common.Pagination;
using API.Common.Sorting;
using API.Contracts;
using API.Contracts.Pagination;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ERP.SecurityTests;

public class ApiContractAndResponseStandardizationTests
{
    private class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    private class TestController : BaseApiController
    {
        public IActionResult TestSuccess<T>(T data, string message = "Request completed successfully.") => SuccessResponse(data, message);
        public IActionResult TestCreated<T>(string routeName, object routeValues, T data, string message = "Resource created successfully.") =>
            CreatedResponse(routeName, routeValues, data, message);
        public IActionResult TestFromServiceResult<T>(ServiceResult<T> result) => FromResult(result);
        public IActionResult TestHandleDomainException(Exception ex) => HandleDomainException(ex);
        public IActionResult TestPaginateAndSort<T>(IEnumerable<T> source, int? page, int? pageSize, string? sortBy, string? sortDirection, SortMapping<T> sortMapping) =>
            PaginateAndSort(source, page, pageSize, sortBy, sortDirection, sortMapping);
    }

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    #region 1. ApiResponse<T> Contract & JSON Serialization

    [Fact]
    public void ApiResponse_Success_HasCorrectContractAndSerialization()
    {
        var response = ApiResponse.Success("Test payload", "Operation completed successfully");

        Assert.True(response.Succeeded);
        Assert.Equal("Test payload", response.Data);
        Assert.Equal("Operation completed successfully", response.Message);
        Assert.Null(response.ErrorCode);
        Assert.Empty(response.Errors);

        string json = JsonSerializer.Serialize(response, _jsonOptions);
        Assert.Contains("\"succeeded\":true", json);
        Assert.Contains("\"data\":\"Test payload\"", json);
        Assert.Contains("\"message\":\"Operation completed successfully\"", json);
        Assert.Contains("\"errors\":[]", json);
    }

    [Fact]
    public void ApiResponse_Failure_HasCorrectContractAndSerialization()
    {
        var errors = new List<string>
        {
            "Field1 is required.",
            "Field2 must be positive."
        };
        var response = ApiResponse<string>.Failure("Validation failed", errors, "VALIDATION_FAILED");

        Assert.False(response.Succeeded);
        Assert.Null(response.Data);
        Assert.Equal("Validation failed", response.Message);
        Assert.Equal("VALIDATION_FAILED", response.ErrorCode);
        Assert.NotNull(response.Errors);
        Assert.Equal(2, response.Errors.Count);

        string json = JsonSerializer.Serialize(response, _jsonOptions);
        Assert.Contains("\"succeeded\":false", json);
        Assert.Contains("\"errorCode\":\"VALIDATION_FAILED\"", json);
        Assert.Contains("\"errors\":[", json);
    }

    #endregion

    #region 2. ServiceResult<T> Semantics & No Double Wrapping

    [Fact]
    public void ServiceResult_SuccessAndFailureConstructors_SetCorrectProperties()
    {
        var okResult = ServiceResult<int>.Ok(42, "Success message");
        Assert.True(okResult.Success);
        Assert.Equal(42, okResult.Data);
        Assert.Equal("Success message", okResult.Message);

        var notFoundResult = ServiceResult<int>.NotFound("Entity not found");
        Assert.False(notFoundResult.Success);
        Assert.Equal(ServiceErrorType.NotFound, notFoundResult.ErrorType);
        Assert.Equal("NOT_FOUND", notFoundResult.ErrorCode);

        var conflictResult = ServiceResult<int>.Conflict("Duplicate code");
        Assert.False(conflictResult.Success);
        Assert.Equal(ServiceErrorType.Conflict, conflictResult.ErrorType);

        var valErrors = new List<string> { "Invalid code format" };
        var validationResult = ServiceResult<int>.Validation("Validation error", valErrors);
        Assert.False(validationResult.Success);
        Assert.Equal(ServiceErrorType.Validation, validationResult.ErrorType);
        Assert.Single(validationResult.Errors);
    }

    [Fact]
    public void BaseApiController_FromResult_UnwrapsWithoutDoubleWrapping()
    {
        var controller = new TestController();

        // 1. Success case: Returns OkObjectResult containing ApiResponse<T> where Data is T, not ServiceResult<T>
        var serviceResult = ServiceResult<string>.Ok("Hello World", "Fetched");
        var actionResult = controller.TestFromServiceResult(serviceResult);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var envelope = Assert.IsType<ApiResponse<string>>(okResult.Value);
        Assert.True(envelope.Succeeded);
        Assert.Equal("Hello World", envelope.Data); // Unwrapped T directly
        Assert.Equal("Fetched", envelope.Message);

        // 2. NotFound case: Maps to 404 with ApiResponse<object>
        var nfServiceResult = ServiceResult<string>.NotFound("Resource not found");
        var nfActionResult = controller.TestFromServiceResult(nfServiceResult);
        var notFoundObjResult = Assert.IsType<NotFoundObjectResult>(nfActionResult);
        var nfEnvelope = Assert.IsType<ApiResponse<object>>(notFoundObjResult.Value);
        Assert.False(nfEnvelope.Succeeded);
        Assert.Equal("Resource not found", nfEnvelope.Message);

        // 3. Conflict case: Maps to 409 with ApiResponse<object>
        var conflictServiceResult = ServiceResult<string>.Conflict("Item already exists");
        var conflictActionResult = controller.TestFromServiceResult(conflictServiceResult);
        var conflictObjResult = Assert.IsType<ConflictObjectResult>(conflictActionResult);
        var conflictEnvelope = Assert.IsType<ApiResponse<object>>(conflictObjResult.Value);
        Assert.False(conflictEnvelope.Succeeded);
        Assert.Equal("Item already exists", conflictEnvelope.Message);

        // 4. Validation case: Maps to 400 with ApiResponse<object>
        var valServiceResult = ServiceResult<string>.Validation("Invalid request", new[] { "Required" });
        var valActionResult = controller.TestFromServiceResult(valServiceResult);
        var badRequestObjResult = Assert.IsType<BadRequestObjectResult>(valActionResult);
        var valEnvelope = Assert.IsType<ApiResponse<object>>(badRequestObjResult.Value);
        Assert.False(valEnvelope.Succeeded);
        Assert.Single(valEnvelope.Errors);
    }

    #endregion

    #region 3. Domain Exception Mapping & Status Codes

    [Fact]
    public void BaseApiController_HandleDomainException_MapsAppropriateHttpCodes()
    {
        var controller = new TestController();

        // KeyNotFoundException -> 404
        var nfResult = controller.TestHandleDomainException(new KeyNotFoundException("Item 123 not found"));
        var nfObj = Assert.IsType<NotFoundObjectResult>(nfResult);
        var nfEnv = Assert.IsType<ApiResponse<object>>(nfObj.Value);
        Assert.False(nfEnv.Succeeded);
        Assert.Equal(404, nfObj.StatusCode);

        // ArgumentException -> 400
        var badReqResult = controller.TestHandleDomainException(new ArgumentException("Invalid argument value"));
        var badReqObj = Assert.IsType<BadRequestObjectResult>(badReqResult);
        var badReqEnv = Assert.IsType<ApiResponse<object>>(badReqObj.Value);
        Assert.False(badReqEnv.Succeeded);
        Assert.Equal(400, badReqObj.StatusCode);

        // InvalidOperationException -> 409
        var conflictResult = controller.TestHandleDomainException(new InvalidOperationException("State transition forbidden"));
        var conflictObj = Assert.IsType<ConflictObjectResult>(conflictResult);
        var conflictEnv = Assert.IsType<ApiResponse<object>>(conflictObj.Value);
        Assert.False(conflictEnv.Succeeded);
        Assert.Equal(409, conflictObj.StatusCode);

        // UnauthorizedAccessException -> 401
        var unauthResult = controller.TestHandleDomainException(new UnauthorizedAccessException("Access denied"));
        var unauthObj = Assert.IsType<UnauthorizedObjectResult>(unauthResult);
        var unauthEnv = Assert.IsType<ApiResponse<object>>(unauthObj.Value);
        Assert.False(unauthEnv.Succeeded);
        Assert.Equal(401, unauthObj.StatusCode);

        // Unexpected Exception -> 500 (Does not expose stack trace)
        var serverErrorResult = controller.TestHandleDomainException(new Exception("Internal database failure"));
        var serverErrorObj = Assert.IsType<ObjectResult>(serverErrorResult);
        Assert.Equal(500, serverErrorObj.StatusCode);
        var serverErrorEnv = Assert.IsType<ApiResponse<object>>(serverErrorObj.Value);
        Assert.False(serverErrorEnv.Succeeded);
        Assert.Equal("An unexpected error occurred.", serverErrorEnv.Message);
        Assert.DoesNotContain("stack", serverErrorEnv.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 4. Pagination Boundaries & Metadata

    [Fact]
    public void PaginationRequest_SafeProperties_ClampCorrectly()
    {
        // Default values
        var reqDefault = new PaginationRequest();
        Assert.Equal(1, reqDefault.SafePage);
        Assert.Equal(20, reqDefault.SafePageSize);
        Assert.Equal("asc", reqDefault.SafeSortDirection);

        // Upper clamp
        var reqLarge = new PaginationRequest { Page = 5, PageSize = 500, SortDirection = "desc" };
        Assert.Equal(5, reqLarge.SafePage);
        Assert.Equal(100, reqLarge.SafePageSize); // Clamped to MaxPageSize = 100
        Assert.Equal("desc", reqLarge.SafeSortDirection);

        // Lower clamp
        var reqNegative = new PaginationRequest { Page = -1, PageSize = -10, SortDirection = "invalid" };
        Assert.Equal(1, reqNegative.SafePage);
        Assert.Equal(1, reqNegative.SafePageSize); // Clamped to lower bound 1
        Assert.Equal("asc", reqNegative.SafeSortDirection);
    }

    [Fact]
    public void PaginateAndSort_ProducesCorrectPagedResponse()
    {
        var items = Enumerable.Range(1, 45)
            .Select(i => new TestItem { Id = i, Name = $"Item {i:D2}", Price = i * 10m })
            .ToList();

        var sortMapping = new SortMapping<TestItem>()
            .Add("name", x => x.Name, isDefault: true)
            .Add("price", x => x.Price)
            .SetSecondary(x => x.Id);

        var controller = new TestController();

        // Page 1, Size 10
        var resultP1 = controller.TestPaginateAndSort(items, page: 1, pageSize: 10, sortBy: "price", sortDirection: "desc", sortMapping);
        var okP1 = Assert.IsType<OkObjectResult>(resultP1);
        var envP1 = Assert.IsType<ApiResponse<PagedResponse<TestItem>>>(okP1.Value);

        Assert.True(envP1.Succeeded);
        var pagedDataP1 = envP1.Data!;
        Assert.Equal(1, pagedDataP1.Page);
        Assert.Equal(10, pagedDataP1.PageSize);
        Assert.Equal(45, pagedDataP1.TotalCount);
        Assert.Equal(5, pagedDataP1.TotalPages);
        Assert.False(pagedDataP1.HasPreviousPage);
        Assert.True(pagedDataP1.HasNextPage);
        Assert.Equal(10, pagedDataP1.Items.Count);
        Assert.Equal(45, pagedDataP1.Items[0].Id); // highest price first

        // Page 5, Size 10 (Last page with 5 items)
        var resultP5 = controller.TestPaginateAndSort(items, page: 5, pageSize: 10, sortBy: "price", sortDirection: "desc", sortMapping);
        var okP5 = Assert.IsType<OkObjectResult>(resultP5);
        var envP5 = Assert.IsType<ApiResponse<PagedResponse<TestItem>>>(okP5.Value);
        var pagedDataP5 = envP5.Data!;
        Assert.Equal(5, pagedDataP5.Page);
        Assert.Equal(5, pagedDataP5.Items.Count);
        Assert.True(pagedDataP5.HasPreviousPage);
        Assert.False(pagedDataP5.HasNextPage);

        // Empty collection
        var resultEmpty = controller.TestPaginateAndSort(new List<TestItem>(), page: 1, pageSize: 10, sortBy: null, sortDirection: null, sortMapping);
        var okEmpty = Assert.IsType<OkObjectResult>(resultEmpty);
        var envEmpty = Assert.IsType<ApiResponse<PagedResponse<TestItem>>>(okEmpty.Value);
        Assert.Equal(0, envEmpty.Data!.TotalCount);
        Assert.Equal(0, envEmpty.Data!.TotalPages);
        Assert.Empty(envEmpty.Data!.Items);
    }

    #endregion

    #region 5. Sorting Allowlist & SQL Injection Safety

    [Fact]
    public void SortMapping_ValidatesAllowlist_AndRejectsInvalidFields()
    {
        var items = new List<TestItem>
        {
            new() { Id = 1, Name = "B", Price = 20 },
            new() { Id = 2, Name = "A", Price = 10 }
        };

        var sortMapping = new SortMapping<TestItem>()
            .Add("name", x => x.Name, isDefault: true)
            .Add("price", x => x.Price)
            .SetSecondary(x => x.Id);

        var controller = new TestController();

        // Invalid sortBy returns 400 Bad Request
        var resultInvalidField = controller.TestPaginateAndSort(items, page: 1, pageSize: 10, sortBy: "malicious_sql_injection; DROP TABLE Users;--", sortDirection: "asc", sortMapping);
        var badReq = Assert.IsType<BadRequestObjectResult>(resultInvalidField);
        var env = Assert.IsType<ApiResponse<object>>(badReq.Value);
        Assert.False(env.Succeeded);
        Assert.Equal("INVALID_SORT", env.ErrorCode);

        // Invalid sortDirection returns 400 Bad Request
        var resultInvalidDir = controller.TestPaginateAndSort(items, page: 1, pageSize: 10, sortBy: "name", sortDirection: "random", sortMapping);
        var badReqDir = Assert.IsType<BadRequestObjectResult>(resultInvalidDir);
        var envDir = Assert.IsType<ApiResponse<object>>(badReqDir.Value);
        Assert.False(envDir.Succeeded);
        Assert.Equal("INVALID_SORT", envDir.ErrorCode);
    }

    [Fact]
    public void SortMapping_SecondarySort_ProvidesDeterministicOrder()
    {
        var items = new List<TestItem>
        {
            new() { Id = 3, Name = "SameName", Price = 10 },
            new() { Id = 1, Name = "SameName", Price = 20 },
            new() { Id = 2, Name = "SameName", Price = 30 }
        };

        var sortMapping = new SortMapping<TestItem>()
            .Add("name", x => x.Name, isDefault: true)
            .SetSecondary(x => x.Id);

        var sortedAsc = sortMapping.Apply(items, "name", "asc").ToList();
        Assert.Equal(1, sortedAsc[0].Id);
        Assert.Equal(2, sortedAsc[1].Id);
        Assert.Equal(3, sortedAsc[2].Id);
    }

    #endregion
}
