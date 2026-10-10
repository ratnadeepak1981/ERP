using Hangfire.Dashboard;

namespace API.Security.Authorization;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Enforce authenticated context with PlatformAdmin or PlatformBillingManager
        var user = httpContext.User;
        if (user == null || !user.Identity?.IsAuthenticated == true)
        {
            return false;
        }

        return user.IsInRole("PlatformAdmin") || user.IsInRole("SecurityManager");
    }
}
