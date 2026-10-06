namespace Security.Interfaces;

public interface ITenantAdminBootstrapService
{
    Task<Guid> CreateTenantAdminAsync(
        Guid tenantId,
        string username,
        string email,
        string password);
}