using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.Currency;

public class Currency : Auditable, ITenantScopedEntity
{
    public Guid CurrencyId { get; private set; }
    public Guid TenantId { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public string CurrencyName { get; private set; } = string.Empty;
    public string Symbol { get; private set; } = string.Empty;
    public int DecimalPlaces { get; private set; } = 2;
    public bool IsActive { get; private set; } = true;

    public static Currency Create(
        Guid tenantId,
        string currencyCode,
        string currencyName,
        string symbol,
        int decimalPlaces = 2,
        Guid? currencyId = null)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ArgumentException("Currency code is required.", nameof(currencyCode));
        if (string.IsNullOrWhiteSpace(currencyName))
            throw new ArgumentException("Currency name is required.", nameof(currencyName));
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Currency symbol is required.", nameof(symbol));
        if (decimalPlaces < 0 || decimalPlaces > 4)
            throw new ArgumentException("Decimal places must be between 0 and 4.", nameof(decimalPlaces));

        return new Currency
        {
            CurrencyId = currencyId ?? Guid.NewGuid(),
            TenantId = tenantId,
            CurrencyCode = currencyCode.Trim().ToUpperInvariant(),
            CurrencyName = currencyName.Trim(),
            Symbol = symbol.Trim(),
            DecimalPlaces = decimalPlaces,
            IsActive = true
        };
    }

    public void Update(string currencyName, string symbol, int decimalPlaces)
    {
        if (string.IsNullOrWhiteSpace(currencyName))
            throw new ArgumentException("Currency name is required.", nameof(currencyName));
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Currency symbol is required.", nameof(symbol));
        if (decimalPlaces < 0 || decimalPlaces > 4)
            throw new ArgumentException("Decimal places must be between 0 and 4.", nameof(decimalPlaces));

        CurrencyName = currencyName.Trim();
        Symbol = symbol.Trim();
        DecimalPlaces = decimalPlaces;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    private Currency() { }
}
