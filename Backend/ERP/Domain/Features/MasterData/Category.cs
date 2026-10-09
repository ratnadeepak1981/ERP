using ERP.Domain.Common;

namespace ERP.Domain.Features.MasterData.Category;

public class Category : Auditable
{
    public Guid CategoryId { get; private set; }

    public Guid TenantId { get; private set; }

    public string CategoryCode { get; private set; } = string.Empty;

    public string CategoryName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }


    public static Category Create(
        Guid tenantId,
        string categoryCode,
        string categoryName,
        string? description = null,
        Guid? categoryId = null)
    {
        return new Category
        {
            CategoryId = categoryId ?? Guid.NewGuid(),
            TenantId = tenantId,
            CategoryCode = categoryCode,
            CategoryName = categoryName,
            Description = description,
            IsActive = true
        };
    }

    private Category()
    {
    }
}