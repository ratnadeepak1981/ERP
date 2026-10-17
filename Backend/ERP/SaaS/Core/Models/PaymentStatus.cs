namespace SaaS.Core.Models;

public enum PaymentStatus
{
    Pending = 1,
    Success = 2,
    Failed = 3,
    Refunded = 4,
    PartiallyRefunded = 5
}
