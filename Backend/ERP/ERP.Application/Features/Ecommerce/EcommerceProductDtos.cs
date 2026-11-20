using System;

namespace ERP.Application.Features.Ecommerce;

public class CreateEcommerceProductRequest
{
    public Guid CompanyId { get; set; }
    public Guid ProductId { get; set; }
    public string? OnlineDescription { get; set; }
    public int DisplayOrder { get; set; } = 0;
    public bool IsPublished { get; set; } = false;
}

public class UpdateEcommerceProductRequest
{
    public string? OnlineDescription { get; set; }
    public int DisplayOrder { get; set; } = 0;
}

public class EcommerceProductPublicationDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public string? OnlineDescription { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsSellable { get; set; }
    public bool IsProductActive { get; set; }
    public decimal SellingPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Guid? BrandId { get; set; }
    public string? BrandName { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public class PublicEcommerceProductDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? OnlineDescription { get; set; }
    public int DisplayOrder { get; set; }
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Guid? BrandId { get; set; }
    public string? BrandName { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
    public string Currency { get; set; } = "USD";
}

public class PublicCatalogueFilter
{
    public string? SearchTerm { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }
}
