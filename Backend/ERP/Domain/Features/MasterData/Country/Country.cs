using System;
using ERP.Domain.Common;

namespace Domain.Features.MasterData.Country;

public class Country : Auditable
{
    public Guid CountryId { get; private set; }

    public string Alpha2Code { get; private set; } = string.Empty;

    public string Alpha3Code { get; private set; } = string.Empty;

    public string? NumericCode { get; private set; }

    public string CountryName { get; private set; } = string.Empty;

    public string? OfficialName { get; private set; }

    public bool IsActive { get; private set; }

    public static Country Create(
        string alpha2Code,
        string alpha3Code,
        string countryName,
        string? numericCode = null,
        string? officialName = null,
        Guid? countryId = null)
    {
        return new Country
        {
            CountryId = countryId ?? Guid.NewGuid(),
            Alpha2Code = alpha2Code.Trim().ToUpperInvariant(),
            Alpha3Code = alpha3Code.Trim().ToUpperInvariant(),
            CountryName = countryName.Trim(),
            NumericCode = numericCode?.Trim(),
            OfficialName = officialName?.Trim(),
            IsActive = true
        };
    }

    private Country()
    {
    }
}
