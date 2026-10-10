using System;
using System.Collections.Generic;

namespace ERP.Domain.Features.MasterData.Customer;

public class CreateCustomerRequest
{
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? TaxNumber { get; set; }
    public decimal CreditLimit { get; set; }
    public string? PaymentTerms { get; set; }
}

public class UpdateCustomerRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? TaxNumber { get; set; }
    public decimal CreditLimit { get; set; }
    public string? PaymentTerms { get; set; }
}

public class CustomerAddressLinkRequest
{
    public Guid AddressId { get; set; }
    public Guid AddressTypeId { get; set; }
    public bool IsDefault { get; set; }
}

public class CustomerContactLinkRequest
{
    public Guid ContactId { get; set; }
    public Guid ContactTypeId { get; set; }
    public bool IsPrimary { get; set; }
}
