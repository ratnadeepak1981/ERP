using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Security.Authorization;
using ERP.Domain.Features.MasterData.Category;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Category;

[ApiController]
[Route("api/categories")]
[RequirePermission("CATEGORY.VIEW")]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly ICurrentUserContext _currentUserContext;

    public CategoryController(
        ICategoryService categoryService,
        ICurrentUserContext currentUserContext)
    {
        _categoryService = categoryService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var categories = await _categoryService.GetCategoriesAsync(tenantId.Value);
        return Ok(categories);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCategory(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var category = await _categoryService.GetCategoryAsync(tenantId.Value, id);
        if (category == null)
            return NotFound(new { status = "FAIL", message = "Category not found." });

        return Ok(category);
    }

    [HttpPost]
    [RequirePermission("CATEGORY.CREATE")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var category = await _categoryService.CreateCategoryAsync(tenantId.Value, request);
            return CreatedAtAction(nameof(GetCategory), new { id = category.CategoryId }, category);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("CATEGORY.UPDATE")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var category = await _categoryService.UpdateCategoryAsync(tenantId.Value, id, request);
            return Ok(category);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Category not found." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("CATEGORY.DELETE")]
    public async Task<IActionResult> DeactivateCategory(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            await _categoryService.DeactivateCategoryAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Category not found." });
        }
    }
}
