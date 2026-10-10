using System;
using System.Collections.Generic;

namespace ERP.Domain.Features.MasterData.Supplier;

public class CreateSupplierRequest
{
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? TaxNumber { get; set; }
    public string? CreditTerms { get; set; }
    public string? PaymentTerms { get; set; }
}

public class UpdateSupplierRequest
{
    public string SupplierName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? TaxNumber { get; set; }
    public string? CreditTerms { get; set; }
    public string? PaymentTerms { get; set; }
}

public class SupplierAddressLinkRequest
{
    public Guid AddressId { get; set; }
    public Guid AddressTypeId { get; set; }
    public bool IsDefault { get; set; }
}

public class SupplierContactLinkRequest
{
    public Guid ContactId { get; set; }
    public Guid ContactTypeId { get; set; }
    public bool IsPrimary { get; set; }
}
