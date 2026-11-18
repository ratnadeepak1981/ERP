using System;
using System.Collections.Generic;

namespace SaaS.Application.DTOs;

public class TenantQueueItemDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }

    // Tenant Identity & Legal Profile
    public string? LegalName { get; set; }

    public string? Subdomain { get; set; }

    public string? TaxId { get; set; }

    public string? BillingEmail { get; set; }

    public string? City { get; set; }

    public string? CountryCode { get; set; }

    // Subscription & Plan
    public Guid? SubscriptionId { get; set; }

    public Guid? SubscriptionPlanId { get; set; }

    public string? SubscriptionName { get; set; }

    public string? PlanName { get; set; }

    public string? PlanCode { get; set; }

    public bool? PlanIsActive { get; set; }

    public string SubscriptionStatus { get; set; } = "None";

    public string StorageMode { get; set; } = "None";

    public DateTime? SubscriptionStartDate { get; set; }

    public DateTime? SubscriptionEndDate { get; set; }

    public DateTime? NextBillingDate { get; set; }

    public string? BillingCycle { get; set; }

    public decimal? BillingPlanPriceSnapshot { get; set; }

    public string? Currency { get; set; }

    public bool? AutoRenew { get; set; }

    // Database & Provisioning
    public string Status { get; set; } = "Pending"; // "Pending" | "Completed"

    public bool IsProvisioned { get; set; }

    public string? DatabaseName { get; set; }

    public string? DatabaseServer { get; set; }

    public bool? DatabaseIsActive { get; set; }

    public DateTime? ProvisioningDate { get; set; }

    public bool ConfigurationExists { get; set; }

    public bool CompanyEnabled { get; set; }

    public bool BranchEnabled { get; set; }

    public bool WarehouseZoneEnabled { get; set; }

    public bool WarehouseBinEnabled { get; set; }

    public bool ProcurementApprovalRequired { get; set; }

    public int ProcurementApprovalMode { get; set; } = 1;

    public bool SpecialUomConversionEnabled { get; set; }

    public string? IndustryType { get; set; }

    public string? CompanyStructure { get; set; }

    public bool ECommerceEnabled { get; set; }

    // Administrator Information
    public string? AdminUsername { get; set; }

    public string? AdminEmail { get; set; }

    public List<string> TenantAdminUsernames { get; set; } = new();

    public List<string> TenantAdminEmails { get; set; } = new();

    public int TenantAdminCount { get; set; }

    public string? TenantAdminAccountStatus { get; set; }

    // User Metrics
    public int TotalUserCount { get; set; }

    public int ActiveUserCount { get; set; }

    public int InactiveUserCount { get; set; }

    // Organization Scope (Company & Branch)
    public int CompanyCount { get; set; }

    public int BranchCount { get; set; }

    public string? PrimaryCompanyName { get; set; }

    public string? PrimaryCompanyStatus { get; set; }
}
