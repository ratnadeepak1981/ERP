namespace SaaS.Core.Rules;

public static class SubscriptionRules
{
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
}