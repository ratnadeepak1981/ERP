namespace SaaS.Application.Interfaces;

public interface ITenantDatabaseResolver
{
    string GetDatabaseConnectionString(Guid tenantId);
}