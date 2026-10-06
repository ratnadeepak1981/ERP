using Microsoft.EntityFrameworkCore.Storage;
using SaaS.Core.Models;

namespace SaaS.Application.Interfaces;

public interface ITenantRepository
{
    Task AddAsync(Tenant tenant);

    Task<Tenant?> GetByIdAsync(Guid tenantId);

    Task<bool> ExistsByCodeAsync(string code);

    Task SaveChangesAsync();

    Task<IDbContextTransaction> BeginTransactionAsync();

    Task CommitTransactionAsync(
        IDbContextTransaction transaction);

    Task RollbackTransactionAsync(
        IDbContextTransaction transaction);
}