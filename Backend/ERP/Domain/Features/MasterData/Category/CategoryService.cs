using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.MasterData.Category
{
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

            // Optional parent validation and assignment
            if (request.ParentCategoryId.HasValue)
            {
                var parentMap = await _repository.GetParentMapAsync(tenantId);
                Guid parentId = request.ParentCategoryId.Value;

                if (!parentMap.ContainsKey(parentId))
                    throw new InvalidOperationException($"Parent category '{parentId}' does not exist for this tenant.");

                if (parentId == category.CategoryId)
                    throw new InvalidOperationException("A category cannot be its own parent.");

                // No cycle check needed on create because the new entity is not yet in the map.
                category.SetParent(parentId);
            }
            else
            {
                category.SetParent(null);
            }

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

            // Validate parent if supplied
            if (request.ParentCategoryId.HasValue)
            {
                var parentMap = await _repository.GetParentMapAsync(tenantId);
                Guid newParentId = request.ParentCategoryId.Value;

                if (!parentMap.ContainsKey(newParentId))
                    throw new InvalidOperationException($"Parent category '{newParentId}' does not exist for this tenant.");

                if (newParentId == categoryId)
                    throw new InvalidOperationException("A category cannot be its own parent.");

                // Detect cycles (including inactive categories)
                var visited = new HashSet<Guid> { categoryId };
                Guid? cursor = newParentId;
                while (cursor.HasValue)
                {
                    if (visited.Contains(cursor.Value))
                        throw new InvalidOperationException("Circular parent relationship detected.");
                    visited.Add(cursor.Value);
                    cursor = parentMap.ContainsKey(cursor.Value) ? parentMap[cursor.Value] : null;
                }

                category.SetParent(newParentId);
            }
            else
            {
                // Explicitly clearing parent (moving to root)
                category.SetParent(null);
            }

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

        public async Task<Category> ReactivateCategoryAsync(Guid tenantId, Guid categoryId)
        {
            var category = await _repository.GetByIdAsync(tenantId, categoryId)
                ?? throw new KeyNotFoundException("Category not found.");

            if (category.IsActive)
                return category;

            category.Activate();
            await _repository.SaveChangesAsync();
            return category;
        }
    }
}
