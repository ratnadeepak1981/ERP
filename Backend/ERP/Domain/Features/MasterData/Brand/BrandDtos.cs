using System;

namespace Domain.Features.MasterData.Brand;

public class BrandDto
{
    public Guid BrandId { get; set; }
    public Guid TenantId { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateBrandRequest
{
    public string BrandCode { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateBrandRequest
{
    public string BrandName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
