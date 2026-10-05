using ERP.Domain.Common;
using System;

namespace ERP.Domain.Features.MasterData.Supplier;

public class Supplier : Auditable
{
    public Guid SupplierId { get; private set; }
    public Guid TenantId { get; private set; }

    public string SupplierCode { get; private set; } = string.Empty;
    public string SupplierName { get; private set; } = string.Empty;

    public string? ContactPerson { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }

    public string? AddressLine1 { get; private set; }
    public string? AddressLine2 { get; private set; }
    public string? City { get; private set; }
    public string? PostalCode { get; private set; }
    public string? Country { get; private set; }

    public string? TaxNumber { get; private set; }

    public string? CreditTerms { get; private set; }
    public string? PaymentTerms { get; private set; }

    public bool IsActive { get; private set; }


    private Supplier() { }
}