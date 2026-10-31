using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using ERP.Domain.Features.MasterData.Category;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Category;

[ApiController]
[Route("api/categories")]
[RequirePermission("CATEGORY.VIEW")]
public class CategoryController : BaseApiController
{
    private readonly ICategoryService _categoryService;
    private readonly ICurrentUserContext _currentUserContext;

    private static readonly SortMapping<global::ERP.Domain.Features.MasterData.Category.Category> _sortMapping = new SortMapping<global::ERP.Domain.Features.MasterData.Category.Category>()
        .Add("categoryCode", c => c.CategoryCode, isDefault: true)
        .Add("categoryName", c => c.CategoryName)
        .SetSecondary(c => c.CategoryId);

    public CategoryController(
        ICategoryService categoryService,
        ICurrentUserContext currentUserContext)
    {
        _categoryService = categoryService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var categories = await _categoryService.GetCategoriesAsync(tenantId.Value);
            return PaginateAndSort(categories, page, pageSize, sortBy, sortDirection, _sortMapping);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCategory(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var category = await _categoryService.GetCategoryAsync(tenantId.Value, id);
            if (category == null)
                return NotFoundResponse("Category not found.");

            return SuccessResponse(category);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost]
    [RequirePermission("CATEGORY.CREATE")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var category = await _categoryService.CreateCategoryAsync(tenantId.Value, request);
            return CreatedResponse(nameof(GetCategory), new { id = category.CategoryId }, category);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("CATEGORY.EDIT")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var category = await _categoryService.UpdateCategoryAsync(tenantId.Value, id, request);
            return SuccessResponse(category);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("CATEGORY.DELETE")]
    public async Task<IActionResult> DeactivateCategory(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _categoryService.DeactivateCategoryAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/reactivate")]
    [RequirePermission("CATEGORY.EDIT")]
    public async Task<IActionResult> ReactivateCategory(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var category = await _categoryService.ReactivateCategoryAsync(tenantId.Value, id);
            return SuccessResponse(category, "Category reactivated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
