using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.MasterData.Category;

public class Category : Auditable, ITenantScopedEntity
{
    public Guid CategoryId { get; private set; }

    public Guid TenantId { get; private set; }

    public string CategoryCode { get; private set; } = string.Empty;

    public string CategoryName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    // Nullable parent category for hierarchical tree (Option A)
    public Guid? ParentCategoryId { get; private set; }

    // Navigation properties for EF Core self‑reference
    public Category? Parent { get; private set; }
    public ICollection<Category> Children { get; private set; } = new List<Category>();

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

    public void Update(string categoryName, string? description)
    {
        CategoryName = categoryName;
        Description = description;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void SetParent(Guid? parentId)
    {
        ParentCategoryId = parentId;
    }

    private Category()
    {
    }
}