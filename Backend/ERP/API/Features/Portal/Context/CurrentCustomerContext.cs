using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Domain.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace API.Features.Portal.Context;

/// <summary>
/// Encapsulates the resolved customer identity context from the authenticated Customer JWT.
/// Recognizes both dedicated Customer tokens (portal_type == "CUSTOMER") and unified login tokens
/// holding customer roles, resolving underlying Customer and Contact records transparently.
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
    private readonly DomainDbContext _domainContext;

    public CurrentCustomerContext(
        IHttpContextAccessor httpContextAccessor,
        DomainDbContext domainContext)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _domainContext = domainContext ?? throw new ArgumentNullException(nameof(domainContext));
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
            if (Guid.TryParse(value, out var id) && id != Guid.Empty)
            {
                return id;
            }

            // Fallback resolution for unified login sessions
            if (UserId != Guid.Empty && TenantId != Guid.Empty)
            {
                var customerId = _domainContext.CustomerContacts
                    .Where(cc => cc.TenantId == TenantId && cc.ContactId == UserId && cc.IsActive)
                    .Select(cc => cc.CustomerId)
                    .FirstOrDefault();

                if (customerId != Guid.Empty)
                {
                    return customerId;
                }
            }

            return Guid.Empty;
        }
    }

    public Guid ContactId
    {
        get
        {
            var value = User?.FindFirstValue("contact_id");
            if (Guid.TryParse(value, out var id) && id != Guid.Empty)
            {
                return id;
            }

            return UserId;
        }
    }

    public string PortalType
    {
        get
        {
            var portalType = User?.FindFirstValue("portal_type");
            if (!string.IsNullOrWhiteSpace(portalType))
            {
                return portalType;
            }

            if (Roles.Any(r => r.StartsWith("CUSTOMER_", StringComparison.OrdinalIgnoreCase) || r.Equals("Customer", StringComparison.OrdinalIgnoreCase)))
            {
                return "CUSTOMER";
            }

            return string.Empty;
        }
    }

    public bool IsCustomer =>
        (string.Equals(PortalType, "CUSTOMER", StringComparison.OrdinalIgnoreCase) ||
         Roles.Any(r => r.StartsWith("CUSTOMER_", StringComparison.OrdinalIgnoreCase) || r.Equals("Customer", StringComparison.OrdinalIgnoreCase))) &&
        CustomerId != Guid.Empty &&
        TenantId != Guid.Empty &&
        UserId != Guid.Empty;

    public IReadOnlyCollection<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public IReadOnlyCollection<string> Permissions =>
        User?.FindAll("permission").Select(c => c.Value).ToList() ?? [];
}
