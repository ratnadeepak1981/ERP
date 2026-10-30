using System;

namespace Domain.Features.MasterData.Product;

public class CreateProductPackagingRequest
{
    public Guid ProductId { get; set; }
    public Guid? CompanyId { get; set; }
    public string PackagingCode { get; set; } = string.Empty;
    public string PackagingName { get; set; } = string.Empty;
    public Guid PackagingUomId { get; set; }
    public decimal BaseQuantity { get; set; }
    public bool PurchaseAllowed { get; set; } = true;
    public bool SalesAllowed { get; set; } = true;
}

public class UpdateProductPackagingRequest
{
    public string PackagingName { get; set; } = string.Empty;
    public decimal BaseQuantity { get; set; }
    public bool PurchaseAllowed { get; set; } = true;
    public bool SalesAllowed { get; set; } = true;
    public Guid? CompanyId { get; set; }
}

public class ProductPackagingDto
{
    public Guid ProductPackagingId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string PackagingCode { get; set; } = string.Empty;
    public string PackagingName { get; set; } = string.Empty;
    public Guid PackagingUomId { get; set; }
    public string PackagingUomCode { get; set; } = string.Empty;
    public Guid ProductBaseUomId { get; set; }
    public string ProductBaseUomCode { get; set; } = string.Empty;
    public decimal BaseQuantity { get; set; }
    public string DisplayExpression { get; set; } = string.Empty;
    public bool PurchaseAllowed { get; set; }
    public bool SalesAllowed { get; set; }
    public bool IsActive { get; set; }
}
