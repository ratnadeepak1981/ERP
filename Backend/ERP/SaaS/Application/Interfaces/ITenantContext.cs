using System;

namespace SaaS.Application.Interfaces;

public interface ITenantContext
{
    Guid? TenantId { get; }

    string? Subdomain { get; }

    string? TenantName { get; }

    string? IndustryType { get; }

    string? CompanyStructure { get; }

    bool ECommerceEnabled { get; }

    bool IsActive { get; }

    bool IsTenantRequest { get; }
}
