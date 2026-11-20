using System;

namespace API.Features.Portal.DTOs;

/// <summary>
/// Payload submitted for Company Customer self-service registration.
/// Follows Customer-Workspace-and-Registration-Workflow.md (v1.1.0) Section 4.
/// </summary>
public sealed class RegisterCompanyCustomerRequest
{
    /// <summary>
    /// Target tenant under which the customer company is registering.
    /// </summary>
    public Guid TenantId { get; set; }

    // --- Company Master Data ---
    public string CompanyName { get; set; } = string.Empty;
    public string? TradingName { get; set; }
    public string? CompanyRegistrationNumber { get; set; }
    public string? TaxNumber { get; set; }
    public string? CompanyEmail { get; set; }
    public string? CompanyPhone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    // --- Human Contact & Login Identifier ---
    public string ContactPersonName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? MobileNumber { get; set; }

    // --- Credentials ---
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// Result returned upon successful customer company registration.
/// </summary>
public sealed class CustomerRegistrationResult
{
    public Guid CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public Guid ContactId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsExistingCompanyMatched { get; set; }
    public bool RequiresAuthorization { get; set; }
    public bool CanLoginImmediately { get; set; }
    public string Message { get; set; } = string.Empty;
}
