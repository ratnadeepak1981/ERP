using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Security.Core.Models;
using Security.Infrastructure.Persistence;
using Security.Interfaces;

namespace Security.Services;

public class TenantUserSetupService : ITenantUserSetupService
{
    private readonly SecurityDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly SecurityBootstrapService? _bootstrapService;

    public TenantUserSetupService(
        SecurityDbContext context,
        PasswordService passwordService,
        SecurityBootstrapService? bootstrapService = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        _bootstrapService = bootstrapService;
    }

    public static string NormalizeTenantCode(string? tenantCode, string? tenantName = null)
    {
        if (!string.IsNullOrWhiteSpace(tenantCode))
        {
            string cleaned = Regex.Replace(tenantCode, @"[^A-Za-z0-9]", "").ToUpperInvariant();

            // If the code is just the auto-generated SaaS prefix "TEN-XXXX" or "TEN",
            // prefer deriving from the human-readable tenant name if available
            if ((cleaned.StartsWith("TEN") || cleaned.Length <= 3) && !string.IsNullOrWhiteSpace(tenantName))
            {
                string nameCode = Regex.Replace(tenantName, @"[^A-Za-z0-9]", "").ToUpperInvariant();
                if (nameCode.Length >= 2 && !nameCode.StartsWith("TEN"))
                {
                    return nameCode.Length <= 8 ? nameCode : nameCode[..8];
                }
            }

            if (cleaned.Length >= 2 && !string.Equals(cleaned, "TEN", StringComparison.OrdinalIgnoreCase))
            {
                return cleaned.Length <= 8 ? cleaned : cleaned[..8];
            }
        }

        if (!string.IsNullOrWhiteSpace(tenantName))
        {
            string nameCode = Regex.Replace(tenantName, @"[^A-Za-z0-9]", "").ToUpperInvariant();
            if (nameCode.Length >= 2)
            {
                return nameCode.Length <= 8 ? nameCode : nameCode[..8];
            }
        }

        return "APX";
    }

    public List<TenantUserSpec> BuildDefaultDemoUserSpecs(
        string tenantShortCode,
        string emailDomain,
        Guid companyId,
        Guid branchId,
        string? defaultPassword = null)
    {
        string cleanCode = NormalizeTenantCode(tenantShortCode);

        if (string.IsNullOrWhiteSpace(emailDomain))
            emailDomain = $"{cleanCode.ToLowerInvariant()}.local";

        emailDomain = emailDomain.Trim().ToLowerInvariant();

        // Standard Account naming convention:
        // {TENANT_CODE}{MODULE_CODE}{ROLE_CODE} (all uppercase, no spaces, hyphens, or separators)
        return new List<TenantUserSpec>
        {
            new()
            {
                Username = $"{cleanCode}ADM",
                Email = $"{cleanCode.ToLowerInvariant()}adm@{emailDomain}",
                RoleTemplateKey = "TENANT_ADMIN",
                RoleName = "Tenant Admin",
                CompanyId = companyId,
                BranchId = branchId,
                Password = defaultPassword,
                Description = "Tenant Administrator"
            },
            new()
            {
                Username = $"{cleanCode}PURMGR",
                Email = $"{cleanCode.ToLowerInvariant()}purmgr@{emailDomain}",
                RoleTemplateKey = "PROCUREMENT_MANAGER",
                RoleName = "Procurement Manager",
                CompanyId = companyId,
                BranchId = branchId,
                Password = defaultPassword,
                Description = "Procurement Manager (Can submit and approve PR and PO)"
            },
            new()
            {
                Username = $"{cleanCode}PURSTF",
                Email = $"{cleanCode.ToLowerInvariant()}purstf@{emailDomain}",
                RoleTemplateKey = "PROCUREMENT_OFFICER",
                RoleName = "Procurement Officer",
                CompanyId = companyId,
                BranchId = branchId,
                Password = defaultPassword,
                Description = "Procurement Officer (Can create PR and PO, cannot approve)"
            },
            new()
            {
                Username = $"{cleanCode}INVMGR",
                Email = $"{cleanCode.ToLowerInvariant()}invmgr@{emailDomain}",
                RoleTemplateKey = "INVENTORY_MANAGER",
                RoleName = "Inventory Manager",
                CompanyId = companyId,
                BranchId = branchId,
                Password = defaultPassword,
                Description = "Inventory Manager (Can post receipts, issues, approve stock counts)"
            },
            new()
            {
                Username = $"{cleanCode}INVSTF",
                Email = $"{cleanCode.ToLowerInvariant()}invstf@{emailDomain}",
                RoleTemplateKey = "INVENTORY_OFFICER",
                RoleName = "Inventory Officer",
                CompanyId = companyId,
                BranchId = branchId,
                Password = defaultPassword,
                Description = "Inventory Officer (Can record receipts, putaway, issues, cannot approve counts)"
            },
            new()
            {
                Username = $"{cleanCode}SALMGR",
                Email = $"{cleanCode.ToLowerInvariant()}salmgr@{emailDomain}",
                RoleTemplateKey = "SALES_MANAGER",
                RoleName = "Sales Manager",
                CompanyId = companyId,
                BranchId = branchId,
                Password = defaultPassword,
                Description = "Sales Manager (Can approve sales orders, dispatch, price overrides)"
            },
            new()
            {
                Username = $"{cleanCode}SALSTF",
                Email = $"{cleanCode.ToLowerInvariant()}salstf@{emailDomain}",
                RoleTemplateKey = "SALES_OFFICER",
                RoleName = "Sales Officer",
                CompanyId = companyId,
                BranchId = branchId,
                Password = defaultPassword,
                Description = "Sales Officer (Can create sales orders and record picking)"
            },
            new()
            {
                Username = $"{cleanCode}WHMGR",
                Email = $"{cleanCode.ToLowerInvariant()}whmgr@{emailDomain}",
                RoleTemplateKey = "WAREHOUSE_CONFIGURATION_OFFICER",
                RoleName = "Warehouse Configuration Officer",
                CompanyId = companyId,
                BranchId = branchId,
                Password = defaultPassword,
                Description = "Warehouse Configuration Officer (Zones, racks, bins, locations)"
            },
            new()
            {
                Username = $"{cleanCode}FINMGR",
                Email = $"{cleanCode.ToLowerInvariant()}finmgr@{emailDomain}",
                RoleTemplateKey = "FINANCE_CONFIGURATION_OFFICER",
                RoleName = "Finance Configuration Officer",
                CompanyId = companyId,
                BranchId = branchId,
                Password = defaultPassword,
                Description = "Finance Configuration Officer (Payment terms, tax codes, currencies)"
            },
            new()
            {
                Username = $"{cleanCode}MDMMGR",
                Email = $"{cleanCode.ToLowerInvariant()}mdmmgr@{emailDomain}",
                RoleTemplateKey = "MASTER_DATA_MANAGER",
                RoleName = "Master Data Manager",
                CompanyId = companyId,
                BranchId = branchId,
                Password = defaultPassword,
                Description = "Master Data Manager (Cross-module master data governance)"
            },
            new()
            {
                Username = $"{cleanCode}AUDVIEW",
                Email = $"{cleanCode.ToLowerInvariant()}audview@{emailDomain}",
                RoleTemplateKey = "TENANT_AUDITOR",
                RoleName = "Tenant Read-only Auditor",
                CompanyId = companyId,
                BranchId = branchId,
                Password = defaultPassword,
                Description = "Tenant Read-only Auditor (Read-only view across reports and master data)"
            }
        };
    }

    public async Task<TenantUserSetupResult> SetupTenantUsersAsync(
        Guid tenantId,
        TenantUserSetupRequest? request = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));

        var result = new TenantUserSetupResult
        {
            TenantId = tenantId,
            Success = true
        };

        // 1. Load tenant roles (strictly enforce tenant ownership)
        var tenantScopedRoles = await _context.Roles
            .Where(r => r.TenantId == tenantId && r.IsActive)
            .ToListAsync();

        if (tenantScopedRoles.Count == 0 && _bootstrapService != null)
        {
            await _bootstrapService.EnsureTenantStandardRolesAsync(tenantId);
            tenantScopedRoles = await _context.Roles
                .Where(r => r.TenantId == tenantId && r.IsActive)
                .ToListAsync();
        }

        var availableRoles = tenantScopedRoles.Count > 0
            ? tenantScopedRoles
            : await _context.Roles.Where(r => (r.TenantId == tenantId || r.TenantId == null) && r.IsActive).ToListAsync();

        if (availableRoles.Count == 0)
        {
            result.Success = false;
            result.Messages.Add($"No active roles found for tenant {tenantId}. Ensure role templates are seeded.");
            return result;
        }

        // 2. Identify specs to provision
        List<TenantUserSpec> specsToProvision;
        if (request?.CustomUsers != null && request.CustomUsers.Count > 0)
        {
            specsToProvision = request.CustomUsers;
        }
        else
        {
            Guid companyId = request?.CompanyId ?? Guid.Empty;
            Guid branchId = request?.BranchId ?? Guid.Empty;
            string rawCode = !string.IsNullOrWhiteSpace(request?.TenantCode)
                ? request.TenantCode
                : (!string.IsNullOrWhiteSpace(request?.TenantShortCode) ? request.TenantShortCode : "APX");
            string cleanCode = NormalizeTenantCode(rawCode);
            string emailDomain = request?.EmailDomain ?? $"{cleanCode.ToLowerInvariant()}.local";

            specsToProvision = BuildDefaultDemoUserSpecs(
                cleanCode,
                emailDomain,
                companyId,
                branchId,
                request?.DefaultInitialPassword);
        }

        // 3. Process each user specification
        foreach (var spec in specsToProvision)
        {
            string username = spec.Username.Trim().ToUpperInvariant();
            string email = spec.Email.Trim().ToLowerInvariant();

            // Locate Role by TemplateKey (prefer tenant-scoped role over system role)
            var role = availableRoles
                .Where(r => string.Equals(r.TemplateKey, spec.RoleTemplateKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.TenantId.HasValue) // Prefer tenant-specific role
                .FirstOrDefault()
                ?? (!string.IsNullOrWhiteSpace(spec.RoleName)
                    ? availableRoles
                        .Where(r => string.Equals(r.Name, spec.RoleName, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(r => r.TenantId.HasValue)
                        .FirstOrDefault()
                    : null);

            if (role == null)
            {
                result.Messages.Add($"Warning: Role for template key '{spec.RoleTemplateKey}' ({spec.RoleName}) was not found. Skipping user '{username}'.");
                continue;
            }

            // Find existing user strictly within this tenant
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && (u.Username == username || u.Email == email));

            User user;
            bool userCreated = false;
            string? generatedPassword = null;

            if (existingUser != null)
            {
                // PRESERVE existing user credentials and identity completely
                user = existingUser;
                result.Messages.Add($"Preserved existing user '{user.Username}' ({user.Email}). Credentials untouched.");
            }
            else
            {
                // Determine password
                string passwordToHash;
                if (!string.IsNullOrWhiteSpace(spec.Password))
                {
                    passwordToHash = spec.Password;
                }
                else if (!string.IsNullOrWhiteSpace(request?.DefaultInitialPassword))
                {
                    passwordToHash = request.DefaultInitialPassword;
                }
                else
                {
                    generatedPassword = GenerateSecurePassword();
                    passwordToHash = generatedPassword;
                }

                user = new User
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Username = username,
                    Email = email,
                    PasswordHash = _passwordService.HashPassword(passwordToHash),
                    IsActive = true
                };

                await _context.Users.AddAsync(user);
                await _context.SaveChangesAsync();
                userCreated = true;
                result.Messages.Add($"Created demo user '{user.Username}' ({user.Email}).");
            }

            // 4. Role Assignment
            var existingUserRole = await _context.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id);

            UserRole userRole;
            bool roleAssigned = false;

            if (existingUserRole != null)
            {
                userRole = existingUserRole;
            }
            else
            {
                userRole = new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = role.Id
                };

                await _context.UserRoles.AddAsync(userRole);
                await _context.SaveChangesAsync();
                roleAssigned = true;
                result.Messages.Add($"Assigned role '{role.Name}' to user '{user.Username}'.");
            }

            // 5. Scope Assignment (Company and Branch)
            bool scopeAssigned = false;
            Guid? effectiveCompanyId = spec.CompanyId ?? request?.CompanyId;
            Guid? effectiveBranchId = spec.BranchId ?? request?.BranchId;

            if (effectiveCompanyId.HasValue && effectiveCompanyId.Value != Guid.Empty)
            {
                var existingScope = await _context.UserRoleScopes
                    .FirstOrDefaultAsync(s =>
                        s.UserRoleId == userRole.Id &&
                        s.CompanyId == effectiveCompanyId.Value &&
                        s.BranchId == effectiveBranchId);

                if (existingScope == null)
                {
                    var newScope = new UserRoleScope
                    {
                        Id = Guid.NewGuid(),
                        UserRoleId = userRole.Id,
                        CompanyId = effectiveCompanyId.Value,
                        BranchId = (effectiveBranchId.HasValue && effectiveBranchId.Value != Guid.Empty) ? effectiveBranchId : null,
                        IsActive = true
                    };

                    await _context.UserRoleScopes.AddAsync(newScope);
                    await _context.SaveChangesAsync();
                    scopeAssigned = true;
                    result.Messages.Add($"Assigned Company ({effectiveCompanyId}) and Branch ({effectiveBranchId}) scope to user '{user.Username}'.");
                }
            }

            result.Users.Add(new TenantUserResultItem
            {
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                RoleId = role.Id,
                RoleTemplateKey = spec.RoleTemplateKey,
                RoleName = role.Name,
                CompanyId = effectiveCompanyId,
                BranchId = effectiveBranchId,
                UserCreated = userCreated,
                RoleAssigned = roleAssigned,
                ScopeAssigned = scopeAssigned,
                GeneratedPassword = generatedPassword,
                Status = userCreated ? "Created" : "Reused"
            });
        }

        // 6. Report preservation of primary tenant admin (e.g. retail.admin)
        var preExistingAdmin = await _context.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && EF.Functions.Like(u.Username, "%.admin"));

        if (preExistingAdmin != null && !result.Users.Any(u => u.UserId == preExistingAdmin.Id))
        {
            result.Messages.Add($"Verified primary administrator '{preExistingAdmin.Username}' is preserved with existing credentials.");
        }

        return result;
    }

    private static string GenerateSecurePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%^&*";

        var sb = new StringBuilder(16);
        sb.Append(upper[RandomNumberGenerator.GetInt32(upper.Length)]);
        sb.Append(lower[RandomNumberGenerator.GetInt32(lower.Length)]);
        sb.Append(digits[RandomNumberGenerator.GetInt32(digits.Length)]);
        sb.Append(special[RandomNumberGenerator.GetInt32(special.Length)]);

        const string allChars = upper + lower + digits + special;
        for (int i = 4; i < 16; i++)
        {
            sb.Append(allChars[RandomNumberGenerator.GetInt32(allChars.Length)]);
        }

        // Shuffle
        var chars = sb.ToString().ToCharArray();
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
