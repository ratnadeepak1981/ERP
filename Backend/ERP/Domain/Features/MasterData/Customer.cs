using ERP.Domain.Common;
using ERP.Domain.Common.Scope;
using System;
using Domain.Features.MasterData.Currency;

namespace ERP.Domain.Features.MasterData.Customer;

public class Customer : Auditable, ITenantScopedEntity
{
    public Guid CustomerId { get; private set; }
    public Guid TenantId { get; private set; }

    public string CustomerCode { get; private set; } = string.Empty;
    public string CustomerName { get; private set; } = string.Empty;

    public string? ContactPerson { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }

    public string? AddressLine1 { get; private set; }
    public string? AddressLine2 { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string? PostalCode { get; private set; }
    public string? Country { get; private set; }

    public string? TaxNumber { get; private set; }

    public decimal CreditLimit { get; private set; }

    public string? PaymentTerms { get; private set; }

    public Guid? CurrencyId { get; private set; }
    public Currency? Currency { get; private set; }

    public string? CurrencyCode => Currency?.CurrencyCode;

    public Guid? DefaultSalesPriceCardId { get; private set; }

    public bool IsActive { get; private set; }

    public static Customer Create(
        Guid tenantId,
        string customerCode,
        string customerName,
        string? contactPerson = null,
        string? email = null,
        string? phone = null,
        string? addressLine1 = null,
        string? addressLine2 = null,
        string? city = null,
        string? postalCode = null,
        string? country = null,
        string? taxNumber = null,
        decimal creditLimit = 0,
        string? paymentTerms = null,
        Guid? customerId = null,
        Guid? currencyId = null)
    {
        return new Customer
        {
            CustomerId = customerId ?? Guid.NewGuid(),
            TenantId = tenantId,
            CustomerCode = customerCode,
            CustomerName = customerName,
            ContactPerson = contactPerson,
            Email = email,
            Phone = phone,
            AddressLine1 = addressLine1,
            AddressLine2 = addressLine2,
            City = city ?? string.Empty,
            PostalCode = postalCode,
            Country = country,
            TaxNumber = taxNumber,
            CreditLimit = creditLimit,
            PaymentTerms = paymentTerms,
            CurrencyId = currencyId,
            IsActive = true
        };
    }

    public void Update(
        string customerName,
        string? contactPerson,
        string? email,
        string? phone,
        string? addressLine1,
        string? addressLine2,
        string city,
        string? postalCode,
        string? country,
        string? taxNumber,
        decimal creditLimit,
        string? paymentTerms,
        Guid? currencyId = null)
    {
        CustomerName = customerName;
        ContactPerson = contactPerson;
        Email = email;
        Phone = phone;
        AddressLine1 = addressLine1;
        AddressLine2 = addressLine2;
        City = city;
        PostalCode = postalCode;
        Country = country;
        TaxNumber = taxNumber;
        CreditLimit = creditLimit;
        PaymentTerms = paymentTerms;
        CurrencyId = currencyId;
    }

    public void SetDefaultCurrency(Guid? currencyId)
    {
        CurrencyId = currencyId;
    }

    public void SyncPrimaryContact(string? contactPerson, string? email, string? phone)
    {
        ContactPerson = contactPerson;
        Email = email;
        Phone = phone;
    }

    public void SyncDefaultAddress(string? addressLine1, string? addressLine2, string city, string? postalCode, string? country)
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

    private Customer() { }
}
