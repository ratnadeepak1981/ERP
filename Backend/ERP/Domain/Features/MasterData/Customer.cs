using ERP.Domain.Common;
using System;

namespace ERP.Domain.Features.MasterData.Customer;

public class Customer : Auditable
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
    public string? City { get; private set; }
    public string? PostalCode { get; private set; }
    public string? Country { get; private set; }

    public string? TaxNumber { get; private set; }

    public decimal CreditLimit { get; private set; }

    public string? PaymentTerms { get; private set; }

    public bool IsActive { get; private set; }

  

    private Customer() { }
}