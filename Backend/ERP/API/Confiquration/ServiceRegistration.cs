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
        services.AddScoped<ITenantUserSetupService, TenantUserSetupService>();

        services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
        services.AddScoped<ISubscriptionParameterService, SubscriptionParameterService>();
        services.AddScoped<ISubscriptionPlanParameterService, SubscriptionPlanParameterService>();
        services.AddScoped<ISubscriptionLimitService, SubscriptionLimitService>();

        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<ITenantAdminProvisioningService, TenantAdminProvisioningService>();

        services.AddScoped<ISubscriptionUsageService, SubscriptionUsageService>();

        services.AddScoped<IRecurringBillingService, RecurringBillingService>();
        services.AddScoped<IBillingNotificationService, DummyBillingNotificationService>();

        // Centralised Email & Document Generation Infrastructure
        services.AddOptions<ERP.Infrastructure.Email.Configuration.SmtpOptions>()
            .Bind(configuration.GetSection(ERP.Infrastructure.Email.Configuration.SmtpOptions.SectionName));

        services.AddOptions<ERP.Infrastructure.Email.Configuration.EmailRetrySettings>()
            .Bind(configuration.GetSection(ERP.Infrastructure.Email.Configuration.EmailRetrySettings.SectionName))
            .Validate(settings =>
            {
                settings.Validate();
                return true;
            }, "Invalid EmailRetrySettings configuration.")
            .ValidateOnStart();

        services.AddSingleton<ERP.Infrastructure.Email.ISmtpClientFactory, ERP.Infrastructure.Email.DefaultSmtpClientFactory>();
        services.AddScoped<ERP.Application.Common.Email.IEmailTemplateEngine, ERP.Infrastructure.Email.Templates.EmailTemplateEngine>();
        
        // Register concrete MailKit transport implementation
        services.AddScoped<ERP.Infrastructure.Email.MailKitSmtpEmailService>();

        // Register IEmailService decorated with RetryingEmailServiceDecorator
        services.AddScoped<ERP.Application.Common.Email.IEmailService>(sp =>
        {
            var transport = sp.GetRequiredService<ERP.Infrastructure.Email.MailKitSmtpEmailService>();
            var retrySettings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ERP.Infrastructure.Email.Configuration.EmailRetrySettings>>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ERP.Infrastructure.Email.RetryingEmailServiceDecorator>>();
            return new ERP.Infrastructure.Email.RetryingEmailServiceDecorator(transport, retrySettings, logger);
        });

        services.AddScoped<ERP.Application.Features.Procurement.PurchaseOrderFeature.IPurchaseOrderPdfService, ERP.Infrastructure.Pdf.PurchaseOrderPdfService>();
        services.AddScoped<ERP.Application.Features.Sales.SalesQuotation.ISalesQuotationPdfService, ERP.Infrastructure.Pdf.SalesQuotationPdfService>();
        services.AddScoped<ERP.Application.Features.Sales.SalesQuotation.ISalesQuotationEmailService, ERP.Application.Features.Sales.SalesQuotation.SalesQuotationEmailService>();

        services.AddScoped<Domain.Features.MasterData.Company.ICompanyService, ERP.Application.Features.MasterData.Company.CompanyService>();
        services.AddScoped<Domain.Features.Setup.ITenantOrganizationSetupService, ERP.Application.Features.Setup.TenantOrganizationSetupService>();
        services.AddScoped<Domain.Features.Setup.ITenantMasterDataSetupService, ERP.Application.Features.Setup.TenantMasterDataSetupService>();
        services.AddScoped<IUserScopeService, UserScopeService>();
        services.AddScoped<IUserAccessService, UserAccessService>();
        services.AddScoped<Domain.Features.MasterData.Product.IProductService, ERP.Application.Features.MasterData.Product.ProductService>();
        services.AddScoped<Domain.Features.MasterData.Product.IProductPackagingService, ERP.Application.Features.MasterData.Product.ProductPackagingService>();
        services.AddScoped<Domain.Features.MasterData.Product.IProductIdentifierService, ERP.Application.Features.MasterData.Product.ProductIdentifierService>();
        services.AddScoped<Domain.Features.MasterData.Branch.IBranchService, ERP.Application.Features.MasterData.Branch.BranchService>();
        services.AddScoped<Domain.Features.Procurement.PurchaseOrder.IPurchaseOrderService, ERP.Application.Features.Procurement.PurchaseOrderFeature.PurchaseOrderService>();
        services.AddScoped<ERP.Domain.Features.MasterData.Category.ICategoryService, ERP.Application.Features.MasterData.Category.CategoryService>();
        services.AddScoped<Domain.Features.MasterData.UnitOfMeasure.IUnitOfMeasureService, ERP.Application.Features.MasterData.UnitOfMeasure.UnitOfMeasureService>();
        services.AddScoped<Domain.Features.MasterData.Currency.ICurrencyService, ERP.Application.Features.MasterData.Currency.CurrencyService>();
        services.AddScoped<Domain.Features.MasterData.Currency.IExchangeRateService, ERP.Application.Features.MasterData.Currency.ExchangeRateService>();
        services.AddScoped<Domain.Features.MasterData.Brand.IBrandService, ERP.Application.Features.MasterData.Brand.BrandService>();
        services.AddScoped<Domain.Features.MasterData.PaymentTerm.IPaymentTermService, ERP.Application.Features.MasterData.PaymentTerm.PaymentTermService>();
        services.AddScoped<Domain.Features.MasterData.TaxCode.ITaxCodeService, ERP.Application.Features.MasterData.TaxCode.TaxCodeService>();
        services.AddScoped<ERP.Domain.Features.MasterData.Customer.ICustomerService, ERP.Application.Features.MasterData.Customer.CustomerService>();
        services.AddScoped<ERP.Domain.Features.MasterData.Supplier.ISupplierService, ERP.Application.Features.MasterData.Supplier.SupplierService>();
        services.AddScoped<Domain.Features.MasterData.PriceCard.IPriceCardService, ERP.Application.Features.MasterData.PriceCard.PriceCardService>();
        services.AddScoped<Domain.Features.MasterData.PriceCard.IPricingResolutionService, ERP.Application.Features.MasterData.PriceCard.PricingResolutionService>();
        services.AddScoped<ERP.Domain.Features.MasterData.Warehouse.IWarehouseService, ERP.Application.Features.MasterData.Warehouse.WarehouseService>();
        services.AddScoped<ERP.Domain.Features.MasterData.Warehouse.ILocationIdentifierService, ERP.Application.Features.MasterData.Warehouse.LocationIdentifierService>();
        services.AddScoped<Domain.Features.MasterData.Address.IAddressService, ERP.Application.Features.MasterData.Address.AddressService>();
        services.AddScoped<Domain.Features.MasterData.Supplier.ISupplierProductPriceService, ERP.Application.Features.MasterData.Supplier.SupplierProductPriceService>();
        services.AddScoped<Domain.Features.MasterData.UnitOfMeasure.ISpecialUomConversionSettingsProvider, API.Services.SpecialUomConversionSettingsProvider>();
        services.AddScoped<Domain.Features.Procurement.Approval.IProcurementApprovalSettingsProvider, API.Services.ProcurementApprovalSettingsProvider>();
        services.AddScoped<Domain.Features.Procurement.Approval.IApprovalWorkflowService, ERP.Application.Features.Procurement.Approval.ApprovalWorkflowService>();
        services.AddScoped<Domain.Features.Procurement.PurchaseRequisition.IPurchaseRequisitionService, ERP.Application.Features.Procurement.PurchaseRequisitionFeature.PurchaseRequisitionService>();
        services.AddScoped<Domain.Features.Procurement.Route.IProcurementRouteService, Domain.Features.Procurement.Route.ProcurementRouteService>();
        services.AddScoped<Domain.Features.Procurement.RequestForQuotation.IRfqTokenService, Domain.Features.Procurement.RequestForQuotation.RfqTokenService>();
        services.AddScoped<Domain.Features.Procurement.RequestForQuotation.IRfqEmailNotificationService, ERP.Application.Features.Procurement.RFQ.RfqEmailNotificationService>();
        services.AddScoped<Domain.Features.Procurement.RequestForQuotation.IRFQService, Domain.Features.Procurement.RequestForQuotation.RFQService>();
        services.AddScoped<Domain.Features.Procurement.RequestForQuotation.ISupplierQuotationService, Domain.Features.Procurement.RequestForQuotation.SupplierQuotationService>();
        services.AddScoped<ERP.Domain.Features.Inventory.IInventoryPostingService, ERP.Application.Features.Inventory.InventoryPosting.InventoryPostingService>();
        services.AddScoped<ERP.Domain.Features.Inventory.IInventoryService, ERP.Application.Features.Inventory.Management.InventoryService>();
        services.AddScoped<ERP.Domain.Features.Inventory.StockCount.IStockCountService, ERP.Domain.Features.Inventory.StockCount.StockCountService>();
        services.AddScoped<ERP.Domain.Features.Inventory.Planning.IInventoryPlanningService, ERP.Application.Features.Inventory.Planning.InventoryPlanningService>();
        services.AddScoped<ERP.Domain.Features.Inventory.Batch.IBatchService, ERP.Application.Features.Inventory.Batch.BatchService>();
        services.AddScoped<Domain.Features.Inventory.Putaway.IPutawayService, ERP.Application.Features.Inventory.Putaway.PutawayService>();
        services.AddScoped<ERP.Application.Features.Inventory.Replenishment.IInventoryReplenishmentEngine, ERP.Application.Features.Inventory.Replenishment.InventoryReplenishmentEngine>();
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
        services.AddScoped<ERP.Application.Features.Sales.SalesQuotation.ISalesQuotationService, ERP.Application.Features.Sales.SalesQuotation.SalesQuotationService>();
        services.AddScoped<Domain.Features.Sales.Shipment.ISalesShipmentService, ERP.Application.Features.Sales.Shipment.SalesShipmentService>();
        services.AddScoped<Domain.Features.Sales.Invoice.ISalesInvoiceService, ERP.Application.Features.Sales.Invoice.SalesInvoiceService>();

        services.AddHttpContextAccessor();

        // Application URL Resolution Infrastructure
        services.Configure<Domain.Common.ApplicationUrl.ApplicationUrlOptions>(
            configuration.GetSection(Domain.Common.ApplicationUrl.ApplicationUrlOptions.SectionName));
        services.AddScoped<Domain.Common.ApplicationUrl.IHttpRequestOriginAccessor, API.Services.HttpRequestOriginAccessor>();
        services.AddScoped<Domain.Common.ApplicationUrl.IApplicationUrlService, Domain.Common.ApplicationUrl.ApplicationUrlService>();

        services.AddScoped<API.Common.TenantResolution.ITenantContext, API.Common.TenantResolution.TenantContext>();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();

        services.AddScoped<PasswordService>();

        // Customer Portal
        services.AddScoped<API.Features.Portal.Services.ICustomerRegistrationService, API.Features.Portal.Services.CustomerRegistrationService>();
        services.AddScoped<API.Features.Portal.Services.ICustomerAuthService, API.Features.Portal.Services.CustomerAuthService>();
        services.AddScoped<API.Features.Portal.Context.ICurrentCustomerContext, API.Features.Portal.Context.CurrentCustomerContext>();
        services.AddScoped<ERP.Application.Features.Sales.DirectOrder.IDirectCustomerOrderService, ERP.Application.Features.Sales.DirectOrder.DirectCustomerOrderService>();
        services.AddScoped<ERP.Application.Features.Sales.CustomerPortal.ICustomerOrderPortalService, ERP.Application.Features.Sales.CustomerPortal.CustomerOrderPortalService>();

        // E-commerce Product Publishing & Shopping Cart
        services.AddScoped<ERP.Application.Features.Ecommerce.IEcommerceProductService, ERP.Application.Features.Ecommerce.EcommerceProductService>();
        services.AddScoped<ERP.Application.Features.Ecommerce.Cart.IEcommerceCartService, ERP.Application.Features.Ecommerce.Cart.EcommerceCartService>();

        return services;
    }
}
