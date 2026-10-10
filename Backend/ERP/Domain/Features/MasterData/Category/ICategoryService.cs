using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.MasterData.Category;

public interface ICategoryService
{
    Task<List<Category>> GetCategoriesAsync(Guid tenantId);
    Task<Category?> GetCategoryAsync(Guid tenantId, Guid categoryId);
    Task<Category> CreateCategoryAsync(Guid tenantId, CreateCategoryRequest request);
    Task<Category> UpdateCategoryAsync(Guid tenantId, Guid categoryId, UpdateCategoryRequest request);
    Task DeactivateCategoryAsync(Guid tenantId, Guid categoryId);
}
