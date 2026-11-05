using API.Security;
using API.Services;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Domain.Infrastructure.Persistence;
using Domain.Services;
using ERP.AI.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using SaaS.Application.Interfaces;
using SaaS.Application.Services;
using SaaS.Services;
using Security.Infrastructure.Services;
using Security.Interfaces;
using Security.Options;
using Security.Services;

namespace API.Configuration;

public static class ServiceRegistration
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAIInfrastructure(configuration);

        services.AddOptions<AuthenticationOptions>()
            .Bind(configuration.GetSection(AuthenticationOptions.SectionName))
            .Validate(options =>
            {
                options.Validate();
                return true;
            }, "Authentication configuration is invalid.")
            .ValidateOnStart();

        services.AddScoped<IDomainDatabaseConfiguration, DomainDatabaseConfiguration>();

        services.AddScoped<SaaSService>();
        services.AddScoped<DomainService>();
        services.AddScoped<SecurityService>();

        services.AddSingleton<RsaPrivateKeyLoader>();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ITenantDomainService, TenantDomainService>();
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
        services.AddScoped<Domain.Features.MasterData.Product.IProductPackagingService, Domain.Features.MasterData.Product.ProductPackagingService>();
        services.AddScoped<Domain.Features.MasterData.Product.IProductIdentifierService, Domain.Features.MasterData.Product.ProductIdentifierService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<Domain.Features.Procurement.PurchaseOrder.IPurchaseOrderService, ERP.Application.Features.Procurement.PurchaseOrderFeature.PurchaseOrderService>();
        services.AddScoped<ERP.Domain.Features.MasterData.Category.ICategoryService, ERP.Domain.Features.MasterData.Category.CategoryService>();
        services.AddScoped<Domain.Features.MasterData.UnitOfMeasure.IUnitOfMeasureService, Domain.Features.MasterData.UnitOfMeasure.UnitOfMeasureService>();
        services.AddScoped<Domain.Features.MasterData.Currency.ICurrencyService, Domain.Features.MasterData.Currency.CurrencyService>();
        services.AddScoped<Domain.Features.MasterData.Currency.IExchangeRateService, Domain.Features.MasterData.Currency.ExchangeRateService>();
        services.AddScoped<Domain.Features.MasterData.Brand.IBrandService, Domain.Features.MasterData.Brand.BrandService>();
        services.AddScoped<Domain.Features.MasterData.PaymentTerm.IPaymentTermService, Domain.Features.MasterData.PaymentTerm.PaymentTermService>();
        services.AddScoped<Domain.Features.MasterData.TaxCode.ITaxCodeService, Domain.Features.MasterData.TaxCode.TaxCodeService>();
        services.AddScoped<ERP.Domain.Features.MasterData.Customer.ICustomerService, ERP.Domain.Features.MasterData.Customer.CustomerService>();
        services.AddScoped<ERP.Domain.Features.MasterData.Supplier.ISupplierService, ERP.Domain.Features.MasterData.Supplier.SupplierService>();
        services.AddScoped<Domain.Features.MasterData.PriceCard.IPriceCardService, Domain.Features.MasterData.PriceCard.PriceCardService>();
        services.AddScoped<Domain.Features.MasterData.PriceCard.IPricingResolutionService, Domain.Features.MasterData.PriceCard.PricingResolutionService>();
        services.AddScoped<ERP.Domain.Features.MasterData.Warehouse.IWarehouseService, ERP.Domain.Features.MasterData.Warehouse.WarehouseService>();
        services.AddScoped<ERP.Domain.Features.MasterData.Warehouse.ILocationIdentifierService, ERP.Domain.Features.MasterData.Warehouse.LocationIdentifierService>();
        services.AddScoped<Domain.Features.MasterData.Address.IAddressService, Domain.Features.MasterData.Address.AddressService>();
        services.AddScoped<Domain.Features.MasterData.Supplier.ISupplierProductPriceService, Domain.Features.MasterData.Supplier.SupplierProductPriceService>();
        services.AddScoped<Domain.Features.MasterData.UnitOfMeasure.ISpecialUomConversionSettingsProvider, API.Services.SpecialUomConversionSettingsProvider>();
        services.AddScoped<Domain.Features.Procurement.Approval.IProcurementApprovalSettingsProvider, API.Services.ProcurementApprovalSettingsProvider>();
        services.AddScoped<Domain.Features.Procurement.Approval.IApprovalWorkflowService, ERP.Application.Features.Procurement.Approval.ApprovalWorkflowService>();
        services.AddScoped<Domain.Features.Procurement.PurchaseRequisition.IPurchaseRequisitionService, ERP.Application.Features.Procurement.PurchaseRequisitionFeature.PurchaseRequisitionService>();
        services.AddScoped<ERP.Domain.Features.Inventory.IInventoryPostingService, ERP.Application.Features.Inventory.InventoryPosting.InventoryPostingService>();
        services.AddScoped<ERP.Domain.Features.Inventory.IInventoryService, ERP.Application.Features.Inventory.Management.InventoryService>();
        services.AddScoped<ERP.Domain.Features.Inventory.Planning.IInventoryPlanningService, ERP.Application.Features.Inventory.Planning.InventoryPlanningService>();
        services.AddScoped<ERP.Domain.Features.Inventory.Batch.IBatchService, ERP.Application.Features.Inventory.Batch.BatchService>();
        services.AddScoped<Domain.Features.Inventory.Putaway.IPutawayService, ERP.Application.Features.Inventory.Putaway.PutawayService>();
        services.AddScoped<Domain.Features.Procurement.GoodsReceiptNote.IGoodsReceiptNoteService, ERP.Application.Features.Procurement.GoodsReceiptNoteFeature.GoodsReceiptNoteService>();

        // Inventory Valuation Strategies & Service
        services.AddScoped<ERP.Domain.Features.Inventory.Valuation.IInventoryValuationStrategy, ERP.Application.Features.Inventory.Valuation.FifoValuationStrategy>();
        services.AddScoped<ERP.Domain.Features.Inventory.Valuation.IInventoryValuationStrategy, ERP.Application.Features.Inventory.Valuation.LifoValuationStrategy>();
        services.AddScoped<ERP.Domain.Features.Inventory.Valuation.IInventoryValuationStrategy, ERP.Application.Features.Inventory.Valuation.WeightedAverageValuationStrategy>();
        services.AddScoped<ERP.Domain.Features.Inventory.Valuation.IInventoryValuationStrategy, ERP.Application.Features.Inventory.Valuation.StandardCostValuationStrategy>();
        services.AddScoped<ERP.Domain.Features.Inventory.Valuation.IInventoryValuationService, ERP.Application.Features.Inventory.Valuation.InventoryValuationService>();
        services.AddScoped<ERP.Domain.Features.CRM.Deduplication.ICustomerDeduplicationService, ERP.Domain.Features.CRM.Deduplication.CustomerDeduplicationService>();
        services.AddScoped<ERP.Domain.Features.CRM.ILeadService, ERP.Domain.Features.CRM.LeadService>();
        services.AddScoped<ERP.Domain.Features.CRM.IOpportunityService, ERP.Domain.Features.CRM.OpportunityService>();
        services.AddScoped<ERP.Domain.Features.CRM.ICrmActivityService, ERP.Domain.Features.CRM.CrmActivityService>();

        // Sales
        services.AddScoped<Domain.Features.Sales.SalesOrder.ISalesOrderService, ERP.Application.Features.Sales.SalesOrder.SalesOrderService>();
        services.AddScoped<Domain.Features.Sales.Shipment.ISalesShipmentService, ERP.Application.Features.Sales.Shipment.SalesShipmentService>();
        services.AddScoped<Domain.Features.Sales.Invoice.ISalesInvoiceService, ERP.Application.Features.Sales.Invoice.SalesInvoiceService>();

        services.AddHttpContextAccessor();

        services.AddScoped<API.Common.TenantResolution.ITenantContext, API.Common.TenantResolution.TenantContext>();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();

        services.AddScoped<PasswordService>();

        return services;
    }
}
