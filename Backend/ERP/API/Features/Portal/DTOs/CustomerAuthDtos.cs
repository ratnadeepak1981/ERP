using System;
using System.Collections.Generic;

namespace API.Features.Portal.DTOs;

public class CustomerLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class CustomerLoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string Token => AccessToken;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public string Status { get; set; } = "PASS";
    public string Message { get; set; } = "Customer authentication successful.";
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ContactId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PortalType { get; set; } = "CUSTOMER";
    public IEnumerable<string> Roles { get; set; } = [];
    public IEnumerable<string> Permissions { get; set; } = [];
}

public class CustomerProfileDto
{
    public CustomerCompanyDetailsDto Customer { get; set; } = new();
    public CustomerContactDetailsDto Contact { get; set; } = new();
    public CustomerMembershipDetailsDto Membership { get; set; } = new();
    public IReadOnlyList<SellingCompanyDto> AvailableSellingCompanies { get; set; } = [];
}

public class CustomerCompanyDetailsDto
{
    public Guid CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

public class CustomerContactDetailsDto
{
    public Guid ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? MobileNumber { get; set; }
}

public class CustomerMembershipDetailsDto
{
    public Guid TenantId { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
    public bool RequiresAuthorization { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = [];
    public IReadOnlyList<string> Permissions { get; set; } = [];
}

public class SellingCompanyDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? BaseCurrencyCode { get; set; }
}

public class SelectSellingCompanyRequest
{
    public Guid SellingCompanyId { get; set; }
}
