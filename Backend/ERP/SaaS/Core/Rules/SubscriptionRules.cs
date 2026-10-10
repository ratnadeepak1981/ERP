using SaaS.Core.Models;

namespace SaaS.Core.Rules;

public static class SubscriptionRules
{
    public const int DefaultGracePeriodDays = 7;

    public static string GetDefaultSubscriptionName()
    {
        return "Free Manufacturing";
    }

    public static string GetDefaultSubscriptionType()
    {
        return "Free";
    }

    public static bool IsValidSubscriptionName(
        string subscriptionName)
    {
        return !string.IsNullOrWhiteSpace(subscriptionName);
    }

    public static bool IsValidSubscriptionType(
        string subscriptionType)
    {
        if (string.IsNullOrWhiteSpace(subscriptionType))
        {
            return false;
        }

        return subscriptionType.Equals(
                   "Free",
                   StringComparison.OrdinalIgnoreCase)
               ||
               subscriptionType.Equals(
                   "Trial",
                   StringComparison.OrdinalIgnoreCase)
               ||
               subscriptionType.Equals(
                   "Paid",
                   StringComparison.OrdinalIgnoreCase)
               ||
               subscriptionType.Equals(
                   "Enterprise",
                   StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsActive(
        DateTime startDate,
        DateTime? endDate)
    {
        DateTime now = DateTime.UtcNow;

        if (startDate > now)
        {
            return false;
        }

        if (endDate.HasValue && endDate.Value < now)
        {
            return false;
        }

        return true;
    }

    // ==========================================
    // Lifecycle Status & Transition Rules
    // ==========================================

    public static bool CanTransition(
        SubscriptionStatus current,
        SubscriptionStatus target)
    {
        if (current == target)
            return false;

        return current switch
        {
            SubscriptionStatus.PendingPayment => target is SubscriptionStatus.Active 
                                                       or SubscriptionStatus.Cancelled,

            SubscriptionStatus.Active => target is SubscriptionStatus.PastDue 
                                               or SubscriptionStatus.Suspended 
                                               or SubscriptionStatus.Expired 
                                               or SubscriptionStatus.Cancelled,

            SubscriptionStatus.PastDue => target is SubscriptionStatus.Active 
                                                or SubscriptionStatus.Suspended 
                                                or SubscriptionStatus.Expired 
                                                or SubscriptionStatus.Cancelled,

            SubscriptionStatus.Suspended => target is SubscriptionStatus.Active 
                                                  or SubscriptionStatus.Expired 
                                                  or SubscriptionStatus.Cancelled,

            SubscriptionStatus.Expired => false, // Terminal state

            SubscriptionStatus.Cancelled => false, // Terminal state

            _ => false
        };
    }

    public static void ValidateTransition(
        SubscriptionStatus current,
        SubscriptionStatus target)
    {
        if (!CanTransition(current, target))
        {
            throw new InvalidOperationException(
                $"Invalid subscription status transition from '{current}' to '{target}'.");
        }
    }

    public static bool DeriveIsActive(SubscriptionStatus status)
    {
        return status switch
        {
            SubscriptionStatus.Active => true,
            SubscriptionStatus.PastDue => true, // Remains active during grace period to retain tenant slot
            SubscriptionStatus.PendingPayment => false,
            SubscriptionStatus.Suspended => false,
            SubscriptionStatus.Expired => false,
            SubscriptionStatus.Cancelled => false,
            _ => false
        };
    }

    public static bool IsWithinGracePeriod(
        DateTime? endDate,
        DateTime asOfUtc,
        int gracePeriodDays = DefaultGracePeriodDays)
    {
        if (!endDate.HasValue)
            return true;

        if (gracePeriodDays < 0)
            throw new ArgumentOutOfRangeException(nameof(gracePeriodDays), "Grace period cannot be negative.");

        DateTime gracePeriodEnd = endDate.Value.AddDays(gracePeriodDays);
        return asOfUtc <= gracePeriodEnd;
    }

    public static bool IsEntitledToService(
        SubscriptionStatus status,
        DateTime startDate,
        DateTime? endDate,
        DateTime asOfUtc,
        int gracePeriodDays = DefaultGracePeriodDays)
    {
        if (startDate > asOfUtc)
            return false;

        return status switch
        {
            SubscriptionStatus.Active => !endDate.HasValue || asOfUtc <= endDate.Value,
            SubscriptionStatus.PastDue => IsWithinGracePeriod(endDate, asOfUtc, gracePeriodDays),
            _ => false
        };
    }
}