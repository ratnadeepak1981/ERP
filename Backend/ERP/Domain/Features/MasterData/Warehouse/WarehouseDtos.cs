using System;
using System.Collections.Generic;

namespace ERP.Domain.Features.MasterData.Warehouse;

public class CreateWarehouseRequest
{
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

public class UpdateWarehouseRequest
{
    public string WarehouseName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

public class CreateWarehouseZoneRequest
{
    public string ZoneCode { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateWarehouseZoneRequest
{
    public string ZoneName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CreateWarehouseLocationRequest
{
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public Guid? LocationTypeId { get; set; }
    public Guid? ParentLocationId { get; set; }
}

public class UpdateWarehouseLocationRequest
{
    public string LocationName { get; set; } = string.Empty;
    public Guid? LocationTypeId { get; set; }
    public Guid? ParentLocationId { get; set; }
}

public class CreateLocationTypeRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool CanStoreInventory { get; set; } = true;
    public bool CanPick { get; set; } = true;
    public bool CanPutAway { get; set; } = true;
    public bool CanContainChildren { get; set; } = true;
}

public class WarehouseAddressLinkRequest
{
    public Guid AddressId { get; set; }
    public Guid AddressTypeId { get; set; }
    public bool IsDefault { get; set; }
}
