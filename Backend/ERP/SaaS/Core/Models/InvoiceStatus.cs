namespace SaaS.Core.Models;

public enum InvoiceStatus
{
    Draft = 1,
    Pending = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Overdue = 5,
    Failed = 6,
    Cancelled = 7
}
