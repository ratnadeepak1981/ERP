using API.Security;
using API.Services;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Domain.Infrastructure.Persistence;
using Domain.Services;
using SaaS.Application.Interfaces;
using SaaS.Application.Services;
using SaaS.Services;
using Security.Infrastructure.Services;
using Security.Interfaces;
using Security.Services;

namespace API.Configuration;

public static class ServiceRegistration
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddScoped<IDomainDatabaseConfiguration, DomainDatabaseConfiguration>();

        services.AddScoped<SaaSService>();
        services.AddScoped<DomainService>();
        services.AddScoped<SecurityService>();

        services.AddSingleton<RsaPrivateKeyLoader>();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();

        services.AddScoped<IRolePermissionService, RolePermissionService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserRoleService, UserRoleService>();

        services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
        services.AddScoped<ISubscriptionParameterService, SubscriptionParameterService>();
        services.AddScoped<ISubscriptionPlanParameterService, SubscriptionPlanParameterService>();
        services.AddScoped<ISubscriptionLimitService, SubscriptionLimitService>();

        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<ITenantAdminProvisioningService, TenantAdminProvisioningService>();

        services.AddScoped<ISubscriptionUsageService, SubscriptionUsageService>();

        services.AddScoped<IRecurringBillingService, RecurringBillingService>();
        services.AddScoped<IBillingNotificationService, DummyBillingNotificationService>();

        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IUserScopeService, UserScopeService>();
        services.AddScoped<IUserAccessService, UserAccessService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<Domain.Features.Procurement.PurchaseOrder.IPurchaseOrderService, Domain.Features.Procurement.PurchaseOrder.PurchaseOrderService>();

        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUserContext, CurrentUserContext>();

        services.AddScoped<PasswordService>();

        return services;
    }
}