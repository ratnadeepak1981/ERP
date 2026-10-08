using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using SaaS.Application.Interfaces;
using SaaS.Application.Interfaces.Repositories;
using SaaS.Infrastructure.Persistence.Repositories;
using SaaS.Infrastructure.Repositories;
using Security.Infrastructure.Persistence.Repositories;
using Security.Interfaces;

namespace API.Configuration;

public static class RepositoryRegistration
{
    public static IServiceCollection AddApplicationRepositories(
        this IServiceCollection services)
    {
        // Security
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();

        // SaaS
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantDatabaseRepository, TenantDatabaseRepository>();
        services.AddScoped<ITenantConfigurationRepository, TenantConfigurationRepository>();

        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
        services.AddScoped<ISubscriptionParameterRepository, SubscriptionParameterRepository>();
        services.AddScoped<ISubscriptionPlanParameterRepository, SubscriptionPlanParameterRepository>();
        services.AddScoped<ISubscriptionLimitRepository, SubscriptionLimitRepository>();
        services.AddScoped<ISubscriptionUsageRepository, SubscriptionUsageRepository>();

        // Domain
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();

        return services;
    }
}