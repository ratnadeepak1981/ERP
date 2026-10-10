using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using API.Security.Authorization;
namespace API.Security.Authorization;

public class PermissionPolicyProvider
    : DefaultAuthorizationPolicyProvider
{
    public const string PolicyPrefix = "PERMISSION:";

    public PermissionPolicyProvider(
        IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override Task<AuthorizationPolicy?> GetPolicyAsync(
        string policyName)
    {
        if (policyName.StartsWith(
            PolicyPrefix,
            StringComparison.OrdinalIgnoreCase))
        {
            var permission =
                policyName[PolicyPrefix.Length..];

            var policy =
                new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new PermissionRequirement(permission))
                    .Build();

            return Task.FromResult<AuthorizationPolicy?>(
                policy);
        }

        return base.GetPolicyAsync(policyName);
    }
}