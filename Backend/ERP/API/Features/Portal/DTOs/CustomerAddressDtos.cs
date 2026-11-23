using System;

namespace API.Features.Portal.DTOs;

public class CustomerAddressPortalDto
{
    public Guid CustomerAddressId { get; set; }
    public Guid AddressId { get; set; }
    public string AddressName { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public Guid? CountryId { get; set; }
    public Guid AddressTypeId { get; set; }
    public string AddressTypeCode { get; set; } = string.Empty;
    public string AddressTypeName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCustomerAddressPortalRequest
{
    public string AddressName { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public Guid? CountryId { get; set; }
    public Guid? AddressTypeId { get; set; }
    public string? AddressTypeCode { get; set; }
    public bool IsDefault { get; set; }
}

public class UpdateCustomerAddressPortalRequest
{
    public string AddressName { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public Guid? CountryId { get; set; }
    public Guid? AddressTypeId { get; set; }
    public string? AddressTypeCode { get; set; }
    public bool IsDefault { get; set; }
}

public class PortalAddressTypeDto
{
    public Guid AddressTypeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
