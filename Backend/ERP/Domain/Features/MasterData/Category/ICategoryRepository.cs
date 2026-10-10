using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.MasterData.Category;

public interface ICategoryRepository
{
    Task<List<Category>> GetByTenantAsync(Guid tenantId);
    Task<Category?> GetByIdAsync(Guid tenantId, Guid categoryId);
    Task<bool> ExistsByCodeAsync(Guid tenantId, string categoryCode);
    Task<bool> ExistsByNameAsync(Guid tenantId, string categoryName);
    Task AddAsync(Category category);
    Task SaveChangesAsync();
}
