using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace API.Features.Portal.Context;

/// <summary>
/// Encapsulates the resolved customer identity context from the authenticated Customer JWT.
/// Strictly enforces that only tokens with portal_type == "CUSTOMER" and valid customer claims are recognized.
/// </summary>
public interface ICurrentCustomerContext
{
    Guid UserId { get; }
    Guid TenantId { get; }
    Guid CustomerId { get; }
    Guid ContactId { get; }
    string PortalType { get; }
    bool IsCustomer { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }
}

public class CurrentCustomerContext : ICurrentCustomerContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentCustomerContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid UserId
    {
        get
        {
            var value = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User?.FindFirstValue(JwtRegisteredClaimNames.Sub);

            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public Guid TenantId
    {
        get
        {
            var value = User?.FindFirstValue("tenant_id");
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public Guid CustomerId
    {
        get
        {
            var value = User?.FindFirstValue("customer_id");
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public Guid ContactId
    {
        get
        {
            var value = User?.FindFirstValue("contact_id");
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public string PortalType => User?.FindFirstValue("portal_type") ?? string.Empty;

    public bool IsCustomer =>
        string.Equals(PortalType, "CUSTOMER", StringComparison.OrdinalIgnoreCase) &&
        CustomerId != Guid.Empty &&
        TenantId != Guid.Empty &&
        UserId != Guid.Empty;

    public IReadOnlyCollection<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public IReadOnlyCollection<string> Permissions =>
        User?.FindAll("permission").Select(c => c.Value).ToList() ?? [];
}
