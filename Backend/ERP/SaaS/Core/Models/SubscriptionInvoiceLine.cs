using SaaS.Common;

namespace SaaS.Core.Models;

public class SubscriptionInvoiceLine : Auditable
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid SubscriptionInvoiceId { get; set; }

    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }

    public decimal SubTotal { get; set; }

    public decimal TaxRate { get; set; } = 0.0m;

    public decimal TaxAmount { get; set; } = 0.0m;

    public decimal TotalAmount { get; set; }

    public SubscriptionInvoice SubscriptionInvoice { get; set; } = null!;

    public Tenant Tenant { get; set; } = null!;
}
