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
        services.AddScoped<Domain.Features.Procurement.PurchaseOrder.IPurchaseOrderRepository, Domain.Features.Procurement.PurchaseOrder.PurchaseOrderRepository>();
        services.AddScoped<ERP.Domain.Features.MasterData.Category.ICategoryRepository, ERP.Domain.Features.MasterData.Category.CategoryRepository>();
        services.AddScoped<Domain.Features.MasterData.UnitOfMeasure.IUnitOfMeasureRepository, Domain.Features.MasterData.UnitOfMeasure.UnitOfMeasureRepository>();
        services.AddScoped<ERP.Domain.Features.MasterData.Customer.ICustomerRepository, ERP.Domain.Features.MasterData.Customer.CustomerRepository>();
        services.AddScoped<ERP.Domain.Features.MasterData.Supplier.ISupplierRepository, ERP.Domain.Features.MasterData.Supplier.SupplierRepository>();
        services.AddScoped<ERP.Domain.Features.MasterData.Warehouse.IWarehouseRepository, ERP.Domain.Features.MasterData.Warehouse.WarehouseRepository>();
        services.AddScoped<Domain.Features.MasterData.Address.IAddressRepository, Domain.Features.MasterData.Address.AddressRepository>();
        services.AddScoped<Domain.Features.MasterData.Supplier.ISupplierProductPriceRepository, Domain.Features.MasterData.Supplier.SupplierProductPriceRepository>();

        return services;
    }
}