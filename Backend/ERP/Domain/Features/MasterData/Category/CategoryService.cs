using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.MasterData.Category;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _repository;

    public CategoryService(ICategoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<Category>> GetCategoriesAsync(Guid tenantId)
    {
        return await _repository.GetByTenantAsync(tenantId);
    }

    public async Task<Category?> GetCategoryAsync(Guid tenantId, Guid categoryId)
    {
        return await _repository.GetByIdAsync(tenantId, categoryId);
    }

    public async Task<Category> CreateCategoryAsync(Guid tenantId, CreateCategoryRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.CategoryCode))
            throw new ArgumentException("Category code is required.", nameof(request.CategoryCode));

        if (string.IsNullOrWhiteSpace(request.CategoryName))
            throw new ArgumentException("Category name is required.", nameof(request.CategoryName));

        string code = request.CategoryCode.Trim();
        string name = request.CategoryName.Trim();

        if (await _repository.ExistsByCodeAsync(tenantId, code))
            throw new InvalidOperationException($"Category code '{code}' already exists for this tenant.");

        if (await _repository.ExistsByNameAsync(tenantId, name))
            throw new InvalidOperationException($"Category name '{name}' already exists for this tenant.");

        var category = Category.Create(tenantId, code, name, request.Description?.Trim());
        await _repository.AddAsync(category);
        await _repository.SaveChangesAsync();
        return category;
    }

    public async Task<Category> UpdateCategoryAsync(Guid tenantId, Guid categoryId, UpdateCategoryRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.CategoryName))
            throw new ArgumentException("Category name is required.", nameof(request.CategoryName));

        var category = await _repository.GetByIdAsync(tenantId, categoryId)
            ?? throw new KeyNotFoundException("Category not found.");

        string name = request.CategoryName.Trim();
        if (category.CategoryName != name && await _repository.ExistsByNameAsync(tenantId, name))
            throw new InvalidOperationException($"Category name '{name}' already exists for this tenant.");

        category.Update(name, request.Description?.Trim());
        await _repository.SaveChangesAsync();
        return category;
    }

    public async Task DeactivateCategoryAsync(Guid tenantId, Guid categoryId)
    {
        var category = await _repository.GetByIdAsync(tenantId, categoryId)
            ?? throw new KeyNotFoundException("Category not found.");

        category.Deactivate();
        await _repository.SaveChangesAsync();
    }
}
