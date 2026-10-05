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


    private Category()
    {
    }
}