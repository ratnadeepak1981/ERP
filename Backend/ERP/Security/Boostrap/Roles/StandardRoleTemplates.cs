using Security.Bootstrap.Permissions;
using Security.Core.Models;

namespace Security.Bootstrap.Roles;

/// <summary>
/// A centrally-defined standard role template. Tenant provisioning creates a
/// tenant-specific copy of each template (Role.TenantId = tenant). Templates
/// are versioned; upgrading a template only adds newly required permissions and
/// never removes tenant customisations.
/// </summary>
public sealed record StandardRoleTemplate(
    string Key,
    string Name,
    string Description,
    int Version,
    IReadOnlyList<string> PermissionCodes);

/// <summary>
/// Authoritative catalogue of standard tenant role templates and their
/// least-privilege permission matrix.
/// </summary>
public static class StandardRoleTemplates
{
    public const int CurrentVersion = 1;

    public const string TenantAdminKey = "TENANT_ADMIN";
    public const string TenantAuditorKey = "TENANT_AUDITOR";

    public static IReadOnlyList<StandardRoleTemplate> All { get; } = Build();

    public static StandardRoleTemplate? Find(string key)
    {
        return All.FirstOrDefault(x =>
            string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Guards against templates referencing permissions that are not present in
    /// the central catalogue. Called by tests and by bootstrap startup.
    /// </summary>
    public static IReadOnlyList<string> GetUnknownPermissionCodes()
    {
        var known = PermissionCatalogue.All
            .Select(x => x.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return All
            .SelectMany(x => x.PermissionCodes)
            .Where(code => !known.Contains(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code)
            .ToList();
    }

    private static List<StandardRoleTemplate> Build()
    {
        var tenantCodes = PermissionCatalogue.TenantCodes;

        // Tenant Admin is deliberately not granted platform permissions and is
        // not granted the transactional price override capability, which is a
        // specialist pricing control. Everything else in the tenant catalogue is
        // explicitly enumerated (never "all permissions").
        var tenantAdminPermissions = tenantCodes
            .Where(code => !string.Equals(code, "PRICE_CARD.OVERRIDE", StringComparison.OrdinalIgnoreCase))
            .OrderBy(code => code)
            .ToArray();

        // Read-only auditor: every tenant view permission.
        var auditorPermissions = PermissionCatalogue.Tenant
            .Where(x => string.Equals(x.Operation, PermissionOperation.View, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Code)
            .OrderBy(code => code)
            .ToArray();

        var masterDataViewPermissions = new[]
        {
            "COMPANY.VIEW", "BRANCH.VIEW", "PRODUCT.VIEW", "CATEGORY.VIEW",
            "UOM.VIEW", "CURRENCY.VIEW", "BRAND.VIEW", "PAYMENT_TERM.VIEW",
            "TAX_CODE.VIEW", "WAREHOUSE.VIEW", "LOCATION.VIEW", "BATCH.VIEW",
            "ADDRESS.VIEW", "CONTACT.VIEW", "COUNTRY.VIEW", "SUPPLIER.VIEW",
            "CUSTOMER.VIEW", "SUPPLIER_PRICE.VIEW", "PRICE_CARD.VIEW"
        };

        var templates = new List<StandardRoleTemplate>
        {
            new(
                TenantAdminKey,
                "Tenant Admin",
                "Tenant administrator with full tenant business administration. Platform permissions are never included.",
                CurrentVersion,
                tenantAdminPermissions),

            new(
                TenantAuditorKey,
                "Tenant Read-only Auditor",
                "Read-only access to tenant master data and operational records.",
                CurrentVersion,
                auditorPermissions),

            new(
                "MASTER_DATA_MANAGER",
                "Master Data Manager",
                "Maintains master data across products, parties, pricing and facilities.",
                CurrentVersion,
                Combine(
                    new[]
                    {
                        "COMPANY.VIEW", "COMPANY.CREATE", "COMPANY.EDIT", "COMPANY.DELETE", "COMPANY.SOFT_DELETE",
                        "BRANCH.VIEW", "BRANCH.CREATE", "BRANCH.EDIT", "BRANCH.DELETE", "BRANCH.SOFT_DELETE",
                        "COUNTRY.VIEW"
                    },
                    ProductPermissions(),
                    CataloguePermissions(),
                    UomPermissions(),
                    CurrencyPermissions(),
                    BrandPermissions(),
                    PaymentTermPermissions(),
                    TaxCodePermissions(),
                    WarehousePermissions(),
                    LocationPermissions(),
                    BatchPermissions(),
                    AddressPermissions(),
                    ContactPermissions(),
                    SupplierPermissions(),
                    CustomerPermissions(),
                    SupplierPricePermissions(),
                    PriceCardPermissions())),

            new(
                "PRODUCT_CATEGORY_OFFICER",
                "Product & Category Officer",
                "Maintains products, categories, brands and batch metadata.",
                CurrentVersion,
                Combine(
                    ProductPermissions(),
                    CataloguePermissions(),
                    BrandPermissions(),
                    BatchPermissions(),
                    new[] { "UOM.VIEW", "COUNTRY.VIEW" })),

            new(
                "UOM_PACKAGING_OFFICER",
                "UOM & Packaging Officer",
                "Maintains units of measure and packaging conversions.",
                CurrentVersion,
                Combine(
                    UomPermissions(),
                    new[] { "PRODUCT.VIEW", "BATCH.VIEW", "WAREHOUSE.VIEW" })),

            new(
                "SUPPLIER_CUSTOMER_OFFICER",
                "Supplier & Customer Officer",
                "Maintains supplier and customer masters, addresses and contacts.",
                CurrentVersion,
                Combine(
                    SupplierPermissions(),
                    CustomerPermissions(),
                    AddressPermissions(),
                    ContactPermissions(),
                    new[] { "COUNTRY.VIEW" })),

            new(
                "PRICING_OFFICER",
                "Pricing Officer",
                "Maintains price cards and supplier price lists, including price overrides.",
                CurrentVersion,
                Combine(
                    PriceCardPermissions(),
                    SupplierPricePermissions(),
                    new[]
                    {
                        "CUSTOMER.PRICE_CARD.ASSIGN", "SUPPLIER.PRICE_CARD.ASSIGN",
                        "CURRENCY.VIEW", "TAX_CODE.VIEW", "UOM.VIEW",
                        "PRODUCT.VIEW", "CUSTOMER.VIEW", "SUPPLIER.VIEW"
                    })),

            new(
                "FINANCE_CONFIGURATION_OFFICER",
                "Finance Configuration Officer",
                "Maintains currencies, tax codes and payment terms.",
                CurrentVersion,
                Combine(
                    CurrencyPermissions(),
                    TaxCodePermissions(),
                    PaymentTermPermissions(),
                    new[] { "COMPANY.VIEW", "PRICE_CARD.VIEW", "SUPPLIER_PRICE.VIEW" })),

            new(
                "WAREHOUSE_CONFIGURATION_OFFICER",
                "Warehouse Configuration Officer",
                "Maintains warehouses, bins and storage locations.",
                CurrentVersion,
                Combine(
                    WarehousePermissions(),
                    LocationPermissions(),
                    new[] { "PRODUCT.VIEW", "UOM.VIEW", "BATCH.VIEW", "INVENTORY.VIEW" })),

            new(
                "MASTER_DATA_VIEWER",
                "Master Data Viewer",
                "Read-only access to master data.",
                CurrentVersion,
                masterDataViewPermissions),

            new(
                "PROCUREMENT_MANAGER",
                "Procurement Manager",
                "Manages requisitions, purchase orders and goods receipts, including approvals.",
                CurrentVersion,
                Combine(
                    PurchaseRequisitionPermissions(),
                    PurchaseOrderPermissions(),
                    GrnPermissions(),
                    new[]
                    {
                        "PROCUREMENT.SETTINGS.VIEW", "PROCUREMENT.SETTINGS.EDIT",
                        "SUPPLIER.VIEW", "SUPPLIER_PRICE.VIEW", "PRODUCT.VIEW",
                        "WAREHOUSE.VIEW", "INVENTORY.VIEW", "BATCH.VIEW"
                    })),

            new(
                "PROCUREMENT_OFFICER",
                "Procurement Officer",
                "Creates and submits requisitions, purchase orders and goods receipts. Cannot approve.",
                CurrentVersion,
                Combine(
                    new[]
                    {
                        "PURCHASE_REQUISITION.VIEW", "PURCHASE_REQUISITION.CREATE",
                        "PURCHASE_REQUISITION.EDIT", "PURCHASE_REQUISITION.DELETE",
                        "PURCHASE_REQUISITION.SUBMIT", "PURCHASE_REQUISITION.CANCEL",
                        "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE", "PURCHASE_ORDER.EDIT",
                        "PURCHASE_ORDER.DELETE", "PURCHASE_ORDER.SUBMIT", "PURCHASE_ORDER.CANCEL",
                        "GRN.VIEW", "GRN.CREATE", "GRN.EDIT", "GRN.DELETE",
                        "GRN.CONFIRM", "GRN.CANCEL",
                        "PROCUREMENT.SETTINGS.VIEW",
                        "SUPPLIER.VIEW", "SUPPLIER_PRICE.VIEW", "PRODUCT.VIEW",
                        "WAREHOUSE.VIEW", "INVENTORY.VIEW"
                    })),

            new(
                "PROCUREMENT_VIEWER",
                "Procurement Viewer",
                "Read-only access to procurement records.",
                CurrentVersion,
                new[]
                {
                    "PURCHASE_REQUISITION.VIEW", "PURCHASE_ORDER.VIEW", "GRN.VIEW",
                    "PROCUREMENT.SETTINGS.VIEW", "SUPPLIER.VIEW",
                    "SUPPLIER_PRICE.VIEW", "PRODUCT.VIEW", "INVENTORY.VIEW"
                }),

            new(
                "INVENTORY_MANAGER",
                "Inventory Manager",
                "Manages stock receipts, issues, putaway and stock counts, including approval and posting.",
                CurrentVersion,
                Combine(
                    new[]
                    {
                        "INVENTORY.VIEW", "STOCK_RECEIPT.POST", "STOCK_ISSUE.POST",
                        "PUTAWAY.POST",
                        "STOCK_COUNT.VIEW", "STOCK_COUNT.CREATE", "STOCK_COUNT.EDIT",
                        "STOCK_COUNT.SUBMIT", "STOCK_COUNT.APPROVE", "STOCK_COUNT.POST",
                        "STOCK_COUNT.CANCEL",
                        "BATCH.VIEW", "BATCH.CREATE", "BATCH.EDIT",
                        "WAREHOUSE.VIEW", "LOCATION.VIEW", "PRODUCT.VIEW",
                        "UOM.VIEW", "GRN.VIEW", "SUPPLIER.VIEW"
                    })),

            new(
                "INVENTORY_OFFICER",
                "Inventory Officer",
                "Executes stock receipts, issues and putaway, and prepares stock counts. Cannot approve or post stock counts.",
                CurrentVersion,
                new[]
                {
                    "INVENTORY.VIEW", "STOCK_RECEIPT.POST", "STOCK_ISSUE.POST", "PUTAWAY.POST",
                    "STOCK_COUNT.VIEW", "STOCK_COUNT.CREATE", "STOCK_COUNT.EDIT",
                    "STOCK_COUNT.SUBMIT", "STOCK_COUNT.CANCEL",
                    "BATCH.VIEW", "BATCH.CREATE", "BATCH.EDIT",
                    "WAREHOUSE.VIEW", "LOCATION.VIEW", "PRODUCT.VIEW", "UOM.VIEW",
                    "GRN.VIEW"
                }),

            new(
                "INVENTORY_VIEWER",
                "Inventory Viewer",
                "Read-only access to inventory, stock counts and warehouse structures.",
                CurrentVersion,
                new[]
                {
                    "INVENTORY.VIEW", "STOCK_COUNT.VIEW", "BATCH.VIEW",
                    "WAREHOUSE.VIEW", "LOCATION.VIEW", "PRODUCT.VIEW", "UOM.VIEW", "GRN.VIEW"
                }),

            new(
                "SALES_MANAGER",
                "Sales Manager",
                "Manages sales orders, shipments and invoices, including confirmation and cancellation.",
                CurrentVersion,
                Combine(
                    SalesOrderPermissions(),
                    SalesShipmentPermissions(),
                    SalesInvoicePermissions(),
                    new[]
                    {
                        "CUSTOMER.VIEW", "PRICE_CARD.VIEW", "PRODUCT.VIEW", "INVENTORY.VIEW"
                    })),

            new(
                "SALES_OFFICER",
                "Sales Officer",
                "Creates and submits sales orders, dispatches shipments and prepares invoices. Cannot confirm orders or invoices.",
                CurrentVersion,
                new[]
                {
                    "SALES_ORDER.VIEW", "SALES_ORDER.CREATE", "SALES_ORDER.EDIT",
                    "SALES_ORDER.DELETE", "SALES_ORDER.SUBMIT", "SALES_ORDER.CANCEL",
                    "SALES_SHIPMENT.VIEW", "SALES_SHIPMENT.CREATE", "SALES_SHIPMENT.EDIT",
                    "SALES_SHIPMENT.DISPATCH", "SALES_SHIPMENT.DELIVER", "SALES_SHIPMENT.CANCEL",
                    "SALES_INVOICE.VIEW", "SALES_INVOICE.CREATE", "SALES_INVOICE.EDIT",
                    "CUSTOMER.VIEW", "PRICE_CARD.VIEW", "PRODUCT.VIEW", "INVENTORY.VIEW"
                }),

            new(
                "SALES_VIEWER",
                "Sales Viewer",
                "Read-only access to sales orders, shipments and invoices.",
                CurrentVersion,
                new[]
                {
                    "SALES_ORDER.VIEW", "SALES_SHIPMENT.VIEW", "SALES_INVOICE.VIEW",
                    "CUSTOMER.VIEW", "PRICE_CARD.VIEW", "PRODUCT.VIEW", "INVENTORY.VIEW"
                })
        };

        return templates;
    }

    private static string[] Combine(params string[][] groups)
    {
        return groups
            .SelectMany(group => group)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] ProductPermissions() => new[]
    {
        "PRODUCT.VIEW", "PRODUCT.CREATE", "PRODUCT.EDIT", "PRODUCT.DELETE", "PRODUCT.SOFT_DELETE"
    };

    private static string[] CataloguePermissions() => new[]
    {
        "CATEGORY.VIEW", "CATEGORY.CREATE", "CATEGORY.EDIT", "CATEGORY.DELETE", "CATEGORY.SOFT_DELETE"
    };

    private static string[] BrandPermissions() => new[]
    {
        "BRAND.VIEW", "BRAND.CREATE", "BRAND.EDIT", "BRAND.DELETE"
    };

    private static string[] BatchPermissions() => new[]
    {
        "BATCH.VIEW", "BATCH.CREATE", "BATCH.EDIT"
    };

    private static string[] UomPermissions() => new[]
    {
        "UOM.VIEW", "UOM.CREATE", "UOM.EDIT", "UOM.DELETE", "UOM.CONVERT"
    };

    private static string[] CurrencyPermissions() => new[]
    {
        "CURRENCY.VIEW", "CURRENCY.CREATE", "CURRENCY.EDIT", "CURRENCY.DELETE"
    };

    private static string[] PaymentTermPermissions() => new[]
    {
        "PAYMENT_TERM.VIEW", "PAYMENT_TERM.CREATE", "PAYMENT_TERM.EDIT", "PAYMENT_TERM.DELETE"
    };

    private static string[] TaxCodePermissions() => new[]
    {
        "TAX_CODE.VIEW", "TAX_CODE.CREATE", "TAX_CODE.EDIT", "TAX_CODE.DELETE"
    };

    private static string[] WarehousePermissions() => new[]
    {
        "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "WAREHOUSE.EDIT", "WAREHOUSE.UPDATE",
        "WAREHOUSE.DELETE", "WAREHOUSE.SOFT_DELETE", "WAREHOUSE.ACTIVATE",
        "WAREHOUSE.DEACTIVATE", "WAREHOUSE.MANAGE_ZONES"
    };

    private static string[] LocationPermissions() => new[]
    {
        "LOCATION.VIEW", "LOCATION.CREATE", "LOCATION.EDIT", "LOCATION.UPDATE",
        "LOCATION.DELETE", "LOCATION.ACTIVATE", "LOCATION.DEACTIVATE"
    };

    private static string[] AddressPermissions() => new[]
    {
        "ADDRESS.VIEW", "ADDRESS.CREATE", "ADDRESS.EDIT", "ADDRESS.DELETE", "ADDRESS.MANAGE"
    };

    private static string[] ContactPermissions() => new[]
    {
        "CONTACT.VIEW", "CONTACT.CREATE", "CONTACT.EDIT", "CONTACT.DELETE", "CONTACT.MANAGE"
    };

    private static string[] SupplierPermissions() => new[]
    {
        "SUPPLIER.VIEW", "SUPPLIER.CREATE", "SUPPLIER.EDIT", "SUPPLIER.DELETE", "SUPPLIER.SOFT_DELETE"
    };

    private static string[] CustomerPermissions() => new[]
    {
        "CUSTOMER.VIEW", "CUSTOMER.CREATE", "CUSTOMER.EDIT", "CUSTOMER.DELETE", "CUSTOMER.SOFT_DELETE"
    };

    private static string[] SupplierPricePermissions() => new[]
    {
        "SUPPLIER_PRICE.VIEW", "SUPPLIER_PRICE.CREATE", "SUPPLIER_PRICE.EDIT",
        "SUPPLIER_PRICE.DELETE", "SUPPLIER_PRICE.MANAGE"
    };

    private static string[] PriceCardPermissions() => new[]
    {
        "PRICE_CARD.VIEW", "PRICE_CARD.CREATE", "PRICE_CARD.EDIT",
        "PRICE_CARD.MANAGE", "PRICE_CARD.OVERRIDE"
    };

    private static string[] PurchaseRequisitionPermissions() => new[]
    {
        "PURCHASE_REQUISITION.VIEW", "PURCHASE_REQUISITION.CREATE",
        "PURCHASE_REQUISITION.EDIT", "PURCHASE_REQUISITION.DELETE",
        "PURCHASE_REQUISITION.SUBMIT", "PURCHASE_REQUISITION.APPROVE",
        "PURCHASE_REQUISITION.CANCEL"
    };

    private static string[] PurchaseOrderPermissions() => new[]
    {
        "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE", "PURCHASE_ORDER.EDIT",
        "PURCHASE_ORDER.DELETE", "PURCHASE_ORDER.SUBMIT", "PURCHASE_ORDER.APPROVE",
        "PURCHASE_ORDER.CANCEL"
    };

    private static string[] GrnPermissions() => new[]
    {
        "GRN.VIEW", "GRN.CREATE", "GRN.EDIT", "GRN.DELETE", "GRN.CONFIRM", "GRN.CANCEL"
    };

    private static string[] SalesOrderPermissions() => new[]
    {
        "SALES_ORDER.VIEW", "SALES_ORDER.CREATE", "SALES_ORDER.EDIT", "SALES_ORDER.DELETE",
        "SALES_ORDER.SUBMIT", "SALES_ORDER.CONFIRM", "SALES_ORDER.CANCEL"
    };

    private static string[] SalesShipmentPermissions() => new[]
    {
        "SALES_SHIPMENT.VIEW", "SALES_SHIPMENT.CREATE", "SALES_SHIPMENT.EDIT",
        "SALES_SHIPMENT.DELETE", "SALES_SHIPMENT.DISPATCH", "SALES_SHIPMENT.DELIVER",
        "SALES_SHIPMENT.CANCEL"
    };

    private static string[] SalesInvoicePermissions() => new[]
    {
        "SALES_INVOICE.VIEW", "SALES_INVOICE.CREATE", "SALES_INVOICE.EDIT",
        "SALES_INVOICE.CONFIRM", "SALES_INVOICE.CANCEL"
    };
}
