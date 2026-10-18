namespace SaaS.Core.Models;

public enum SubscriptionStatus
{
    PendingPayment = 1,
    Active = 2,
    PastDue = 3,
    Suspended = 4,
    Expired = 5,
    Cancelled = 6
}
