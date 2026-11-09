using Microsoft.EntityFrameworkCore;
using Security.Bootstrap.Roles;
using Security.Interfaces;
using Security.Infrastructure.Persistence;

namespace Security.Services;

public class UserAccessService : IUserAccessService
{
    private readonly SecurityDbContext _context;

    public UserAccessService(
        SecurityDbContext context)
    {
        _context = context;
    }

    public async Task<bool> CanAccessCompanyAsync(
        Guid userId,
        Guid tenantId,
        Guid companyId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.Id == userId &&
                x.IsActive);

        if (user == null)
            return false;

        if (user.TenantId != tenantId)
            return false;

        var isTenantAdmin = await _context.UserRoles
            .AnyAsync(x =>
                x.UserId == userId &&
                x.Role.IsActive &&
                ((x.Role.TemplateKey == StandardRoleTemplates.TenantAdminKey &&
                  x.Role.TenantId == tenantId) ||
                 (x.Role.TenantId == tenantId &&
                  x.Role.Name == "Tenant Admin") ||
                 (x.Role.TenantId == null &&
                  x.Role.Name == "Tenant Admin")));

        if (isTenantAdmin)
            return true;

        return await _context.UserRoleScopes
            .AnyAsync(x =>
                x.IsActive &&
                x.UserRole.UserId == userId &&
                x.CompanyId == companyId);
    }

    public async Task<bool> CanAccessBranchAsync(
        Guid userId,
        Guid tenantId,
        Guid companyId,
        Guid branchId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.Id == userId &&
                x.IsActive);

        if (user == null)
            return false;

        if (user.TenantId != tenantId)
            return false;

        var isTenantAdmin = await _context.UserRoles
            .AnyAsync(x =>
                x.UserId == userId &&
                x.Role.IsActive &&
                ((x.Role.TemplateKey == StandardRoleTemplates.TenantAdminKey &&
                  x.Role.TenantId == tenantId) ||
                 (x.Role.TenantId == tenantId &&
                  x.Role.Name == "Tenant Admin") ||
                 (x.Role.TenantId == null &&
                  x.Role.Name == "Tenant Admin")));

        if (isTenantAdmin)
            return true;

        return await _context.UserRoleScopes
            .AnyAsync(x =>
                x.IsActive &&
                x.UserRole.UserId == userId &&
                x.CompanyId == companyId &&
                (x.BranchId == null || x.BranchId == branchId));
    }
}