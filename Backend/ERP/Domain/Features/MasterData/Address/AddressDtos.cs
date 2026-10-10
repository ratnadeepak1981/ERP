using System;

namespace Domain.Features.MasterData.Address;

public class CreateAddressRequest
{
    public string AddressName { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public Guid? CountryId { get; set; }
    public string? UnmatchedCountryText { get; set; }
}

public class UpdateAddressRequest
{
    public string AddressName { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public Guid? CountryId { get; set; }
}

public class CreateContactRequest
{
    public string ContactName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? MobileNumber { get; set; }
    public string? PreferredContactMethod { get; set; }
}

public class UpdateContactRequest
{
    public string ContactName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? MobileNumber { get; set; }
    public string? PreferredContactMethod { get; set; }
}

public class CreateAddressTypeRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CreateContactTypeRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
