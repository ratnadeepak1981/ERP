using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.MasterData.Warehouse;

public class Warehouse : Auditable, ITenantScopedEntity
{
    public Guid WarehouseId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid? CompanyId { get; private set; }

    public Guid? BranchId { get; private set; }

    public string WarehouseCode { get; private set; } = string.Empty;

    public string WarehouseName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? AddressLine1 { get; private set; }

    public string? AddressLine2 { get; private set; }

    public string? City { get; private set; }

    public string? PostalCode { get; private set; }

    public string? Country { get; private set; }

    public bool IsActive { get; private set; }

    public static Warehouse Create(
        Guid tenantId,
        string warehouseCode,
        string warehouseName,
        string? description = null,
        Guid? companyId = null,
        Guid? branchId = null,
        string? addressLine1 = null,
        string? addressLine2 = null,
        string? city = null,
        string? postalCode = null,
        string? country = null,
        Guid? warehouseId = null)
    {
        return new Warehouse
        {
            WarehouseId = warehouseId ?? Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            WarehouseCode = warehouseCode,
            WarehouseName = warehouseName,
            Description = description,
            AddressLine1 = addressLine1,
            AddressLine2 = addressLine2,
            City = city,
            PostalCode = postalCode,
            Country = country,
            IsActive = true
        };
    }

    public void Update(
        string warehouseName,
        string? description,
        Guid? companyId,
        Guid? branchId,
        string? addressLine1,
        string? addressLine2,
        string? city,
        string? postalCode,
        string? country)
    {
        WarehouseName = warehouseName;
        Description = description;
        CompanyId = companyId;
        BranchId = branchId;
        AddressLine1 = addressLine1;
        AddressLine2 = addressLine2;
        City = city;
        PostalCode = postalCode;
        Country = country;
    }

    public void SyncDefaultAddress(string? addressLine1, string? addressLine2, string? city, string? postalCode, string? country)
    {
        AddressLine1 = addressLine1;
        AddressLine2 = addressLine2;
        City = city;
        PostalCode = postalCode;
        Country = country;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private Warehouse()
    {
    }
}