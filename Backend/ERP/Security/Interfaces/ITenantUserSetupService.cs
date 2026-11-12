using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Security.Interfaces;

public class TenantUserSpec
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleTemplateKey { get; set; } = string.Empty;
    public string? RoleName { get; set; }
    public string? Password { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public string? Description { get; set; }
}

public class TenantUserSetupRequest
{
    public Guid? TargetTenantId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public string? TenantCode { get; set; }
    public string? TenantShortCode { get; set; }
    public string? EmailDomain { get; set; }
    public string? DefaultInitialPassword { get; set; }
    public List<TenantUserSpec>? CustomUsers { get; set; }
}

public class TenantUserResultItem
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleTemplateKey { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public bool UserCreated { get; set; }
    public bool RoleAssigned { get; set; }
    public bool ScopeAssigned { get; set; }
    public string? GeneratedPassword { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class TenantUserSetupResult
{
    public Guid TenantId { get; set; }
    public bool Success { get; set; }
    public List<TenantUserResultItem> Users { get; set; } = new();
    public List<string> Messages { get; set; } = new();
}

public interface ITenantUserSetupService
{
    Task<TenantUserSetupResult> SetupTenantUsersAsync(
        Guid tenantId,
        TenantUserSetupRequest? request = null);

    List<TenantUserSpec> BuildDefaultDemoUserSpecs(
        string tenantShortCode,
        string emailDomain,
        Guid companyId,
        Guid branchId,
        string? defaultPassword = null);
}
