using System;
using SaaS.Core.Models;

namespace SaaS.Application.DTOs;

public class TenantResolutionResult
{
    public bool IsResolved { get; set; }
    public Guid? TenantId { get; set; }
    public string? Hostname { get; set; }
    public string? Subdomain { get; set; }
    public TenantDomainType? DomainType { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public bool IsTenantActive { get; set; }
    public string? FailureReason { get; set; }

    public static TenantResolutionResult Success(
        Guid tenantId,
        string hostname,
        string? subdomain,
        TenantDomainType domainType,
        bool isPrimary)
    {
        return new TenantResolutionResult
        {
            IsResolved = true,
            TenantId = tenantId,
            Hostname = hostname,
            Subdomain = subdomain,
            DomainType = domainType,
            IsPrimary = isPrimary,
            IsActive = true,
            IsVerified = true,
            IsTenantActive = true
        };
    }

    public static TenantResolutionResult NotFound(string reason = "Tenant domain not found.")
    {
        return new TenantResolutionResult
        {
            IsResolved = false,
            FailureReason = reason
        };
    }

    public static TenantResolutionResult InactiveDomain(
        Guid tenantId,
        string hostname,
        string? subdomain,
        TenantDomainType domainType)
    {
        return new TenantResolutionResult
        {
            IsResolved = false,
            TenantId = tenantId,
            Hostname = hostname,
            Subdomain = subdomain,
            DomainType = domainType,
            IsActive = false,
            IsVerified = true,
            IsTenantActive = true,
            FailureReason = "Tenant domain is inactive."
        };
    }

    public static TenantResolutionResult UnverifiedDomain(
        Guid tenantId,
        string hostname,
        string? subdomain,
        TenantDomainType domainType)
    {
        return new TenantResolutionResult
        {
            IsResolved = false,
            TenantId = tenantId,
            Hostname = hostname,
            Subdomain = subdomain,
            DomainType = domainType,
            IsActive = true,
            IsVerified = false,
            IsTenantActive = true,
            FailureReason = "Tenant domain is unverified."
        };
    }

    public static TenantResolutionResult InactiveTenant(
        Guid tenantId,
        string hostname,
        string? subdomain,
        TenantDomainType domainType)
    {
        return new TenantResolutionResult
        {
            IsResolved = false,
            TenantId = tenantId,
            Hostname = hostname,
            Subdomain = subdomain,
            DomainType = domainType,
            IsActive = true,
            IsVerified = true,
            IsTenantActive = false,
            FailureReason = "Associated tenant is inactive."
        };
    }

    public static TenantResolutionResult Malformed(string reason = "Malformed hostname.")
    {
        return new TenantResolutionResult
        {
            IsResolved = false,
            FailureReason = reason
        };
    }
}
