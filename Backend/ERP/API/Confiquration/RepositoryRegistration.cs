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
        services.AddScoped<ITenantDomainRepository, TenantDomainRepository>();
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
        services.AddScoped<Domain.Features.MasterData.Product.IProductRepository, ERP.Infrastructure.Repositories.MasterData.ProductRepository>();
        services.AddScoped<Domain.Features.MasterData.Product.IProductPackagingRepository, ERP.Infrastructure.Repositories.MasterData.ProductPackagingRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<ERP.Application.Features.Procurement.IProcurementPersistenceCoordinator, ERP.Infrastructure.Persistence.ProcurementPersistenceCoordinator>();
        services.AddScoped<Domain.Features.Procurement.PurchaseOrder.IPurchaseOrderRepository, ERP.Infrastructure.Repositories.Procurement.PurchaseOrderRepository>();
        services.AddScoped<ERP.Domain.Features.MasterData.Category.ICategoryRepository, ERP.Infrastructure.Repositories.MasterData.CategoryRepository>();
        services.AddScoped<Domain.Features.MasterData.UnitOfMeasure.IUnitOfMeasureRepository, ERP.Infrastructure.Repositories.MasterData.UnitOfMeasureRepository>();
        services.AddScoped<Domain.Features.MasterData.Currency.ICurrencyRepository, ERP.Infrastructure.Repositories.MasterData.CurrencyRepository>();
        services.AddScoped<Domain.Features.MasterData.Currency.IExchangeRateRepository, ERP.Infrastructure.Repositories.MasterData.ExchangeRateRepository>();
        services.AddScoped<Domain.Features.MasterData.Brand.IBrandRepository, ERP.Infrastructure.Repositories.MasterData.BrandRepository>();
        services.AddScoped<Domain.Features.MasterData.PaymentTerm.IPaymentTermRepository, ERP.Infrastructure.Repositories.MasterData.PaymentTermRepository>();
        services.AddScoped<Domain.Features.MasterData.TaxCode.ITaxCodeRepository, ERP.Infrastructure.Repositories.MasterData.TaxCodeRepository>();
        services.AddScoped<ERP.Domain.Features.MasterData.Customer.ICustomerRepository, ERP.Infrastructure.Repositories.MasterData.CustomerRepository>();
        services.AddScoped<ERP.Domain.Features.MasterData.Supplier.ISupplierRepository, ERP.Infrastructure.Repositories.MasterData.SupplierRepository>();
        services.AddScoped<ERP.Application.Features.MasterData.Customer.ICustomerPersistenceCoordinator, ERP.Infrastructure.Persistence.CustomerPersistenceCoordinator>();
        services.AddScoped<ERP.Application.Features.MasterData.Supplier.ISupplierPersistenceCoordinator, ERP.Infrastructure.Persistence.SupplierPersistenceCoordinator>();
        services.AddScoped<Domain.Features.MasterData.PriceCard.IPriceCardRepository, ERP.Infrastructure.Repositories.MasterData.PriceCardRepository>();
        services.AddScoped<ERP.Domain.Features.MasterData.Warehouse.IWarehouseRepository, ERP.Domain.Features.MasterData.Warehouse.WarehouseRepository>();
        services.AddScoped<Domain.Features.MasterData.Address.IAddressRepository, Domain.Features.MasterData.Address.AddressRepository>();
        services.AddScoped<Domain.Features.MasterData.Supplier.ISupplierProductPriceRepository, ERP.Infrastructure.Repositories.MasterData.SupplierProductPriceRepository>();
        services.AddScoped<Domain.Features.Procurement.Approval.IApprovalAuditRepository, ERP.Infrastructure.Repositories.Procurement.ApprovalAuditRepository>();
        services.AddScoped<Domain.Features.Procurement.PurchaseRequisition.IPurchaseRequisitionRepository, ERP.Infrastructure.Repositories.Procurement.PurchaseRequisitionRepository>();
        services.AddScoped<ERP.Application.Features.Inventory.IInventoryPersistenceCoordinator, ERP.Infrastructure.Persistence.InventoryPersistenceCoordinator>();
        services.AddScoped<ERP.Domain.Features.Inventory.IInventoryRepository, ERP.Infrastructure.Repositories.Inventory.InventoryRepository>();
        services.AddScoped<Domain.Features.Inventory.Putaway.IPutawayRepository, ERP.Infrastructure.Repositories.Inventory.PutawayRepository>();
        services.AddScoped<Domain.Features.Procurement.GoodsReceiptNote.IGoodsReceiptNoteRepository, ERP.Infrastructure.Repositories.Procurement.GoodsReceiptNoteRepository>();

        // CRM
        services.AddScoped<ERP.Domain.Features.CRM.ILeadRepository, ERP.Domain.Features.CRM.LeadRepository>();
        services.AddScoped<ERP.Domain.Features.CRM.IOpportunityRepository, ERP.Domain.Features.CRM.OpportunityRepository>();
        services.AddScoped<ERP.Domain.Features.CRM.ICrmActivityRepository, ERP.Domain.Features.CRM.CrmActivityRepository>();

        // Sales
        services.AddScoped<ERP.Application.Features.Sales.ISalesPersistenceCoordinator, ERP.Infrastructure.Persistence.SalesPersistenceCoordinator>();
        services.AddScoped<Domain.Features.Sales.SalesOrder.ISalesOrderRepository, ERP.Infrastructure.Repositories.Sales.SalesOrderRepository>();
        services.AddScoped<Domain.Features.Sales.Shipment.ISalesShipmentRepository, ERP.Infrastructure.Repositories.Sales.SalesShipmentRepository>();
        services.AddScoped<Domain.Features.Sales.Invoice.ISalesInvoiceRepository, ERP.Infrastructure.Repositories.Sales.SalesInvoiceRepository>();

        return services;
    }
}
