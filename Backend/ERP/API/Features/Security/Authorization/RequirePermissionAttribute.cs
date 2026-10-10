using Microsoft.AspNetCore.Authorization;

namespace API.Security.Authorization;

public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
    {
        Policy =
            $"{PermissionPolicyProvider.PolicyPrefix}{permission}";
    }
}