using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.Address;

public class Address : Auditable, ITenantScopedEntity
{
    public Guid AddressId { get; private set; }

    public Guid TenantId { get; private set; }

    public string AddressName { get; private set; } = string.Empty;

    public string AddressLine1 { get; private set; } = string.Empty;

    public string? AddressLine2 { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string? StateProvince { get; private set; }

    public string? PostalCode { get; private set; }

    public Guid? CountryId { get; private set; }

    public string? UnmatchedCountryText { get; private set; }

    public bool IsActive { get; private set; }

    public static Address Create(
        Guid tenantId,
        string addressName,
        string addressLine1,
        string city,
        string? addressLine2 = null,
        string? stateProvince = null,
        string? postalCode = null,
        Guid? countryId = null,
        string? unmatchedCountryText = null,
        Guid? addressId = null)
    {
        return new Address
        {
            AddressId = addressId ?? Guid.NewGuid(),
            TenantId = tenantId,
            AddressName = addressName.Trim(),
            AddressLine1 = addressLine1.Trim(),
            AddressLine2 = addressLine2?.Trim(),
            City = city.Trim(),
            StateProvince = stateProvince?.Trim(),
            PostalCode = postalCode?.Trim(),
            CountryId = countryId,
            UnmatchedCountryText = countryId.HasValue ? null : unmatchedCountryText?.Trim(),
            IsActive = true
        };
    }

    public void Update(
        string addressName,
        string addressLine1,
        string city,
        string? addressLine2 = null,
        string? stateProvince = null,
        string? postalCode = null,
        Guid? countryId = null)
    {
        AddressName = addressName.Trim();
        AddressLine1 = addressLine1.Trim();
        AddressLine2 = addressLine2?.Trim();
        City = city.Trim();
        StateProvince = stateProvince?.Trim();
        PostalCode = postalCode?.Trim();

        if (countryId.HasValue)
        {
            CountryId = countryId.Value;
            UnmatchedCountryText = null; // Cleared only upon explicit resolution
        }
    }

    public void ResolveCountry(Guid countryId)
    {
        CountryId = countryId;
        UnmatchedCountryText = null;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private Address()
    {
    }
}
