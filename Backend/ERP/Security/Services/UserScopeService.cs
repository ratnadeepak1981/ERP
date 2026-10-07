using Microsoft.EntityFrameworkCore;
using Security.Interfaces;
using Security.Infrastructure.Persistence;

namespace Security.Infrastructure.Services;

public class UserScopeService : IUserScopeService
{
    private readonly SecurityDbContext _context;

    public UserScopeService(SecurityDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<Guid>> GetCompanyIdsAsync(
        Guid userId)
    {
        return await _context.UserRoleScopes
            .Where(x =>
                x.IsActive &&
                x.UserRole.UserId == userId &&
                x.CompanyId.HasValue)
            .Select(x => x.CompanyId!.Value)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IReadOnlyCollection<Guid>> GetBranchIdsAsync(
        Guid userId)
    {
        return await _context.UserRoleScopes
            .Where(x =>
                x.IsActive &&
                x.UserRole.UserId == userId &&
                x.BranchId.HasValue)
            .Select(x => x.BranchId!.Value)
            .Distinct()
            .ToListAsync();
    }
}