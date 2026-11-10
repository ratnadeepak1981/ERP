using System;
using SaaS.Application.Interfaces;
using SaaS.Core.Models;

namespace SaaS.Application.Services;

public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }

    public string? Subdomain { get; set; }

    public string? TenantName { get; set; }

    public string? IndustryType { get; set; }

    public string? CompanyStructure { get; set; } = "Single";

    public bool ECommerceEnabled { get; set; }

    public bool IsActive { get; set; }

    public bool IsTenantRequest => TenantId.HasValue;

    public void SetTenant(Tenant tenant)
    {
        if (tenant == null)
        {
            Reset();
            return;
        }

        TenantId = tenant.Id;
        Subdomain = tenant.Subdomain;
        TenantName = tenant.Name;
        IndustryType = tenant.IndustryType;
        CompanyStructure = tenant.CompanyStructure;
        ECommerceEnabled = tenant.ECommerceEnabled;
        IsActive = tenant.IsActive;
    }

    public void Reset()
    {
        TenantId = null;
        Subdomain = null;
        TenantName = null;
        IndustryType = null;
        CompanyStructure = "Single";
        ECommerceEnabled = false;
        IsActive = false;
    }
}
