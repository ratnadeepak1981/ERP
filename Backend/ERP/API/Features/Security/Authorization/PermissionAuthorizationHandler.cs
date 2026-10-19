using Microsoft.AspNetCore.Authorization;

namespace API.Security.Authorization;

public class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var permissions = requirement.Permission.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var permission in permissions)
        {
            if (context.User.HasClaim("permission", permission))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Treat .EDIT and .UPDATE as equivalent permission action aliases
            if (permission.EndsWith(".EDIT", StringComparison.OrdinalIgnoreCase))
            {
                var updateAlias = permission[..^5] + ".UPDATE";
                if (context.User.HasClaim("permission", updateAlias))
                {
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }
            }
            else if (permission.EndsWith(".UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                var editAlias = permission[..^7] + ".EDIT";
                if (context.User.HasClaim("permission", editAlias))
                {
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }
            }
        }

        return Task.CompletedTask;
    }
}