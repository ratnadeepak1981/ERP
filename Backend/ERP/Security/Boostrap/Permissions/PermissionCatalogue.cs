using Security.Core.Models;

namespace Security.Bootstrap.Permissions;

/// <summary>
/// A single canonical permission definition. The catalogue is the single
/// authoritative source of permission metadata for ApexERP.
/// </summary>
public sealed record PermissionDefinition(
    string Code,
    string Name,
    string Module,
    string Description,
    string Category)
{
    public string Operation => PermissionOperation.FromCode(Code);

    public bool IsPlatform => PermissionCategory.IsPlatform(Category);
}

/// <summary>
/// Central permission catalogue. All permissions defined by the platform and
/// business modules must be registered here. Permission codes are global (not
/// tenant scoped) and uniquely identify a capability.
/// </summary>
public static class PermissionCatalogue
{
    public static IReadOnlyList<PermissionDefinition> All { get; } = Build();

    public static IReadOnlyList<PermissionDefinition> Platform { get; } =
        All.Where(x => x.IsPlatform).ToList();

    public static IReadOnlyList<PermissionDefinition> Tenant { get; } =
        All.Where(x => !x.IsPlatform).ToList();

    public static IReadOnlyCollection<string> TenantCodes { get; } =
        Tenant.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static PermissionDefinition? Find(string code)
    {
        return All.FirstOrDefault(x =>
            string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    private static List<PermissionDefinition> Build()
    {
        var permissions = new List<PermissionDefinition>();

        // =========================
        // Platform administration
        // =========================
        AddPlatform(permissions, "TENANT.VIEW", "View Tenant", "Tenant",
            "Allows viewing tenant records.");
        AddPlatform(permissions, "TENANT.CREATE", "Create Tenant", "Tenant",
            "Allows creating tenants.");
        AddPlatform(permissions, "TENANT.EDIT", "Edit Tenant", "Tenant",
            "Allows editing tenants.");
        AddPlatform(permissions, "TENANT.SUSPEND", "Suspend Tenant", "Tenant",
            "Allows suspending tenants.");

        AddPlatform(permissions, "SUBSCRIPTION.VIEW", "View Subscription", "Subscription",
            "Allows viewing subscriptions.");
        AddPlatform(permissions, "SUBSCRIPTION.CREATE", "Create Subscription", "Subscription",
            "Allows creating subscriptions.");
        AddPlatform(permissions, "SUBSCRIPTION.EDIT", "Edit Subscription", "Subscription",
            "Allows editing subscriptions.");

        AddPlatform(permissions, "AUDIT.VIEW", "View Audit", "Audit",
            "Allows viewing platform audit information.");

        AddPlatform(permissions, "AI.READINESS", "AI Readiness", "AI",
            "Allows invoking AI service readiness endpoints.");

        // =========================
        // Master data: company & branch
        // =========================
        AddTenant(permissions, "COMPANY.VIEW", "View Company", "Company",
            "Allows viewing companies.");
        AddTenant(permissions, "COMPANY.CREATE", "Create Company", "Company",
            "Allows creating companies.");
        AddTenant(permissions, "COMPANY.EDIT", "Edit Company", "Company",
            "Allows editing companies.");
        AddTenant(permissions, "COMPANY.DELETE", "Delete Company", "Company",
            "Allows deleting companies.");
        AddTenant(permissions, "COMPANY.SOFT_DELETE", "Soft Delete Company", "Company",
            "Allows soft deleting companies.");

        AddTenant(permissions, "BRANCH.VIEW", "View Branch", "Branch",
            "Allows viewing branches.");
        AddTenant(permissions, "BRANCH.CREATE", "Create Branch", "Branch",
            "Allows creating branches.");
        AddTenant(permissions, "BRANCH.EDIT", "Edit Branch", "Branch",
            "Allows editing branches.");
        AddTenant(permissions, "BRANCH.DELETE", "Delete Branch", "Branch",
            "Allows deleting branches.");
        AddTenant(permissions, "BRANCH.SOFT_DELETE", "Soft Delete Branch", "Branch",
            "Allows soft deleting branches.");

        // =========================
        // Master data: users & access
        // =========================
        AddTenant(permissions, "USER.VIEW", "View Users", "User",
            "Allows viewing and listing user profiles and operating scopes.");
        AddTenant(permissions, "USER.CREATE", "Create Users", "User",
            "Allows creating new user accounts.");
        AddTenant(permissions, "USER.EDIT", "Edit Users", "User",
            "Allows updating user profile details, active status, and operating scopes.");
        AddTenant(permissions, "USER.DELETE", "Deactivate Users", "User",
            "Allows deactivating or suspending user accounts.");
        AddTenant(permissions, "USER.ASSIGN_ROLE", "Assign User Roles", "User",
            "Allows binding and unbinding roles to user accounts.");

        // =========================
        // Master data: roles & access
        // =========================
        AddTenant(permissions, "ROLE.VIEW", "View Roles", "Role",
            "Allows viewing and listing role definitions and assigned permissions.");
        AddTenant(permissions, "ROLE.CREATE", "Create Roles", "Role",
            "Allows creating custom tenant roles.");
        AddTenant(permissions, "ROLE.EDIT", "Edit Roles", "Role",
            "Allows updating custom role metadata and activating/reactivating roles.");
        AddTenant(permissions, "ROLE.DELETE", "Deactivate Roles", "Role",
            "Allows deactivating custom tenant roles.");

        // =========================
        // Master data: product & catalogue
        // =========================
        AddTenant(permissions, "PRODUCT.VIEW", "View Product", "Product",
            "Allows viewing products.");
        AddTenant(permissions, "PRODUCT.CREATE", "Create Product", "Product",
            "Allows creating products.");
        AddTenant(permissions, "PRODUCT.EDIT", "Edit Product", "Product",
            "Allows editing products.");
        AddTenant(permissions, "PRODUCT.DELETE", "Delete Product", "Product",
            "Allows deleting products.");
        AddTenant(permissions, "PRODUCT.SOFT_DELETE", "Soft Delete Product", "Product",
            "Allows soft deleting products.");

        AddTenant(permissions, "CATEGORY.VIEW", "View Category", "Category",
            "Allows viewing product categories.");
        AddTenant(permissions, "CATEGORY.CREATE", "Create Category", "Category",
            "Allows creating product categories.");
        AddTenant(permissions, "CATEGORY.EDIT", "Edit Category", "Category",
            "Allows editing product categories.");
        AddTenant(permissions, "CATEGORY.DELETE", "Delete Category", "Category",
            "Allows deleting product categories.");
        AddTenant(permissions, "CATEGORY.SOFT_DELETE", "Soft Delete Category", "Category",
            "Allows soft deleting product categories.");

        AddTenant(permissions, "BRAND.VIEW", "View Brands", "Brand",
            "Allows viewing brand records.");
        AddTenant(permissions, "BRAND.CREATE", "Create Brand", "Brand",
            "Allows creating brand records.");
        AddTenant(permissions, "BRAND.EDIT", "Edit Brand", "Brand",
            "Allows editing or reactivating brand records.");
        AddTenant(permissions, "BRAND.DELETE", "Deactivate Brand", "Brand",
            "Allows deactivating brand records.");

        AddTenant(permissions, "BATCH.VIEW", "View Batches", "Batch",
            "Allows viewing product batch details and expiry dates.");
        AddTenant(permissions, "BATCH.CREATE", "Create Batches", "Batch",
            "Allows creating product batches.");
        AddTenant(permissions, "BATCH.EDIT", "Edit Batches", "Batch",
            "Allows editing product batch metadata.");

        // =========================
        // Master data: units, currency, tax
        // =========================
        AddTenant(permissions, "UOM.VIEW", "View Unit of Measure", "UnitOfMeasure",
            "Allows viewing units of measure.");
        AddTenant(permissions, "UOM.CREATE", "Create Unit of Measure", "UnitOfMeasure",
            "Allows creating units of measure.");
        AddTenant(permissions, "UOM.EDIT", "Edit Unit of Measure", "UnitOfMeasure",
            "Allows editing units of measure.");
        AddTenant(permissions, "UOM.DELETE", "Delete Unit of Measure", "UnitOfMeasure",
            "Allows deleting units of measure.");
        AddTenant(permissions, "UOM.CONVERT", "Convert Unit of Measure", "UnitOfMeasure",
            "Allows converting quantities between units of measure.");

        AddTenant(permissions, "CURRENCY.VIEW", "View Currency", "Currency",
            "Allows viewing currencies.");
        AddTenant(permissions, "CURRENCY.CREATE", "Create Currency", "Currency",
            "Allows creating currencies.");
        AddTenant(permissions, "CURRENCY.EDIT", "Edit Currency", "Currency",
            "Allows editing currencies.");
        AddTenant(permissions, "CURRENCY.DELETE", "Delete Currency", "Currency",
            "Allows deleting currencies.");

        AddTenant(permissions, "TAX_CODE.VIEW", "View Tax Codes", "TaxCode",
            "Allows viewing tax codes and rates.");
        AddTenant(permissions, "TAX_CODE.CREATE", "Create Tax Code", "TaxCode",
            "Allows creating tax codes.");
        AddTenant(permissions, "TAX_CODE.EDIT", "Edit Tax Code", "TaxCode",
            "Allows editing tax codes.");
        AddTenant(permissions, "TAX_CODE.DELETE", "Deactivate Tax Code", "TaxCode",
            "Allows deactivating tax codes.");

        AddTenant(permissions, "PAYMENT_TERM.VIEW", "View Payment Terms", "PaymentTerm",
            "Allows viewing payment terms.");
        AddTenant(permissions, "PAYMENT_TERM.CREATE", "Create Payment Term", "PaymentTerm",
            "Allows creating payment terms.");
        AddTenant(permissions, "PAYMENT_TERM.EDIT", "Edit Payment Term", "PaymentTerm",
            "Allows editing payment terms.");
        AddTenant(permissions, "PAYMENT_TERM.DELETE", "Deactivate Payment Term", "PaymentTerm",
            "Allows deactivating payment terms.");

        // =========================
        // Master data: warehouse & location
        // =========================
        AddTenant(permissions, "WAREHOUSE.VIEW", "View Warehouse", "Warehouse",
            "Allows viewing warehouses.");
        AddTenant(permissions, "WAREHOUSE.CREATE", "Create Warehouse", "Warehouse",
            "Allows creating warehouses.");
        AddTenant(permissions, "WAREHOUSE.EDIT", "Edit Warehouse", "Warehouse",
            "Allows editing warehouses.");
        AddTenant(permissions, "WAREHOUSE.UPDATE", "Update Warehouse", "Warehouse",
            "Allows updating warehouse configuration.");
        AddTenant(permissions, "WAREHOUSE.DELETE", "Delete Warehouse", "Warehouse",
            "Allows deleting warehouses.");
        AddTenant(permissions, "WAREHOUSE.SOFT_DELETE", "Soft Delete Warehouse", "Warehouse",
            "Allows soft deleting warehouses.");
        AddTenant(permissions, "WAREHOUSE.ACTIVATE", "Activate Warehouse", "Warehouse",
            "Allows activating warehouses.");
        AddTenant(permissions, "WAREHOUSE.DEACTIVATE", "Deactivate Warehouse", "Warehouse",
            "Allows deactivating warehouses.");
        AddTenant(permissions, "WAREHOUSE.MANAGE_ZONES", "Manage Warehouse Zones", "Warehouse",
            "Allows managing warehouse zones and storage structure.");

        AddTenant(permissions, "LOCATION.VIEW", "View Warehouse Location", "Location",
            "Allows viewing warehouse locations.");
        AddTenant(permissions, "LOCATION.CREATE", "Create Warehouse Location", "Location",
            "Allows creating warehouse locations.");
        AddTenant(permissions, "LOCATION.EDIT", "Edit Warehouse Location", "Location",
            "Allows editing warehouse locations.");
        AddTenant(permissions, "LOCATION.UPDATE", "Update Warehouse Location", "Location",
            "Allows updating warehouse location configuration.");
        AddTenant(permissions, "LOCATION.DELETE", "Delete Warehouse Location", "Location",
            "Allows deleting warehouse locations.");
        AddTenant(permissions, "LOCATION.ACTIVATE", "Activate Warehouse Location", "Location",
            "Allows activating warehouse locations.");
        AddTenant(permissions, "LOCATION.DEACTIVATE", "Deactivate Warehouse Location", "Location",
            "Allows deactivating warehouse locations.");

        // =========================
        // Master data: parties
        // =========================
        AddTenant(permissions, "ADDRESS.VIEW", "View Address", "Address",
            "Allows viewing addresses.");
        AddTenant(permissions, "ADDRESS.CREATE", "Create Address", "Address",
            "Allows creating addresses.");
        AddTenant(permissions, "ADDRESS.EDIT", "Edit Address", "Address",
            "Allows editing addresses.");
        AddTenant(permissions, "ADDRESS.DELETE", "Delete Address", "Address",
            "Allows deleting addresses.");
        AddTenant(permissions, "ADDRESS.MANAGE", "Manage Addresses", "Address",
            "Allows managing addresses for parties.");

        AddTenant(permissions, "CONTACT.VIEW", "View Contact", "Contact",
            "Allows viewing contacts.");
        AddTenant(permissions, "CONTACT.CREATE", "Create Contact", "Contact",
            "Allows creating contacts.");
        AddTenant(permissions, "CONTACT.EDIT", "Edit Contact", "Contact",
            "Allows editing contacts.");
        AddTenant(permissions, "CONTACT.DELETE", "Delete Contact", "Contact",
            "Allows deleting contacts.");
        AddTenant(permissions, "CONTACT.MANAGE", "Manage Contacts", "Contact",
            "Allows managing contacts for parties.");

        AddTenant(permissions, "COUNTRY.VIEW", "View Country", "Country",
            "Allows viewing countries.");

        AddTenant(permissions, "SUPPLIER.VIEW", "View Supplier", "Supplier",
            "Allows viewing suppliers.");
        AddTenant(permissions, "SUPPLIER.CREATE", "Create Supplier", "Supplier",
            "Allows creating suppliers.");
        AddTenant(permissions, "SUPPLIER.EDIT", "Edit Supplier", "Supplier",
            "Allows editing suppliers.");
        AddTenant(permissions, "SUPPLIER.DELETE", "Delete Supplier", "Supplier",
            "Allows deleting suppliers.");
        AddTenant(permissions, "SUPPLIER.SOFT_DELETE", "Soft Delete Supplier", "Supplier",
            "Allows soft deleting suppliers.");
        AddTenant(permissions, "SUPPLIER.PRICE_CARD.ASSIGN", "Assign Supplier Price Card", "Supplier",
            "Allows assigning price cards to suppliers.");

        AddTenant(permissions, "CUSTOMER.VIEW", "View Customer", "Customer",
            "Allows viewing customers.");
        AddTenant(permissions, "CUSTOMER.CREATE", "Create Customer", "Customer",
            "Allows creating customers.");
        AddTenant(permissions, "CUSTOMER.EDIT", "Edit Customer", "Customer",
            "Allows editing customers.");
        AddTenant(permissions, "CUSTOMER.DELETE", "Delete Customer", "Customer",
            "Allows deleting customers.");
        AddTenant(permissions, "CUSTOMER.SOFT_DELETE", "Soft Delete Customer", "Customer",
            "Allows soft deleting customers.");
        AddTenant(permissions, "CUSTOMER.PRICE_CARD.ASSIGN", "Assign Customer Price Card", "Customer",
            "Allows assigning price cards to customers.");

        // =========================
        // Pricing
        // =========================
        AddTenant(permissions, "SUPPLIER_PRICE.VIEW", "View Supplier Product Price", "SupplierPrice",
            "Allows viewing supplier product prices.");
        AddTenant(permissions, "SUPPLIER_PRICE.CREATE", "Create Supplier Product Price", "SupplierPrice",
            "Allows creating supplier product prices.");
        AddTenant(permissions, "SUPPLIER_PRICE.EDIT", "Edit Supplier Product Price", "SupplierPrice",
            "Allows editing supplier product prices.");
        AddTenant(permissions, "SUPPLIER_PRICE.DELETE", "Delete Supplier Product Price", "SupplierPrice",
            "Allows deleting supplier product prices.");
        AddTenant(permissions, "SUPPLIER_PRICE.MANAGE", "Manage Supplier Product Prices", "SupplierPrice",
            "Allows managing supplier product prices.");

        AddTenant(permissions, "PRICE_CARD.VIEW", "View Price Cards", "PriceCard",
            "Allows viewing price cards.");
        AddTenant(permissions, "PRICE_CARD.CREATE", "Create Price Card", "PriceCard",
            "Allows creating price cards.");
        AddTenant(permissions, "PRICE_CARD.EDIT", "Edit Price Card", "PriceCard",
            "Allows editing price cards.");
        AddTenant(permissions, "PRICE_CARD.MANAGE", "Manage Price Cards", "PriceCard",
            "Allows managing price cards.");
        AddTenant(permissions, "PRICE_CARD.OVERRIDE", "Override Transaction Price Card", "PriceCard",
            "Allows overriding the price card applied to a transaction.");

        // =========================
        // Procurement
        // =========================
        AddTenant(permissions, "PURCHASE_REQUISITION.VIEW", "View Purchase Requisition", "Procurement",
            "Allows viewing purchase requisitions.");
        AddTenant(permissions, "PURCHASE_REQUISITION.CREATE", "Create Purchase Requisition", "Procurement",
            "Allows creating purchase requisitions.");
        AddTenant(permissions, "PURCHASE_REQUISITION.EDIT", "Edit Purchase Requisition", "Procurement",
            "Allows editing purchase requisitions.");
        AddTenant(permissions, "PURCHASE_REQUISITION.DELETE", "Delete Purchase Requisition", "Procurement",
            "Allows deleting purchase requisition drafts.");
        AddTenant(permissions, "PURCHASE_REQUISITION.SUBMIT", "Submit Purchase Requisition", "Procurement",
            "Allows submitting purchase requisitions.");
        AddTenant(permissions, "PURCHASE_REQUISITION.APPROVE", "Approve/Reject Purchase Requisition", "Procurement",
            "Allows approving or rejecting purchase requisitions.");
        AddTenant(permissions, "PURCHASE_REQUISITION.CANCEL", "Cancel Purchase Requisition", "Procurement",
            "Allows cancelling purchase requisitions.");

        AddTenant(permissions, "PURCHASE_ORDER.VIEW", "View Purchase Order", "PurchaseOrder",
            "Allows viewing purchase orders.");
        AddTenant(permissions, "PURCHASE_ORDER.CREATE", "Create Purchase Order", "PurchaseOrder",
            "Allows creating purchase orders.");
        AddTenant(permissions, "PURCHASE_ORDER.EDIT", "Edit Purchase Order", "PurchaseOrder",
            "Allows editing purchase orders.");
        AddTenant(permissions, "PURCHASE_ORDER.DELETE", "Delete Purchase Order", "PurchaseOrder",
            "Allows deleting purchase order drafts.");
        AddTenant(permissions, "PURCHASE_ORDER.SUBMIT", "Submit Purchase Order", "PurchaseOrder",
            "Allows submitting purchase orders.");
        AddTenant(permissions, "PURCHASE_ORDER.APPROVE", "Approve/Reject Purchase Order", "PurchaseOrder",
            "Allows approving or rejecting purchase orders.");
        AddTenant(permissions, "PURCHASE_ORDER.CANCEL", "Cancel Purchase Order", "PurchaseOrder",
            "Allows cancelling purchase orders.");

        AddTenant(permissions, "GRN.VIEW", "View Goods Receipt Note", "GoodsReceiptNote",
            "Allows viewing goods receipt notes.");
        AddTenant(permissions, "GRN.CREATE", "Create Goods Receipt Note", "GoodsReceiptNote",
            "Allows creating goods receipt notes.");
        AddTenant(permissions, "GRN.EDIT", "Edit Goods Receipt Note", "GoodsReceiptNote",
            "Allows editing goods receipt notes.");
        AddTenant(permissions, "GRN.DELETE", "Delete Goods Receipt Note Draft", "GoodsReceiptNote",
            "Allows deleting goods receipt note drafts.");
        AddTenant(permissions, "GRN.CONFIRM", "Confirm Goods Receipt Note", "GoodsReceiptNote",
            "Allows confirming goods receipt notes.");
        AddTenant(permissions, "GRN.CANCEL", "Cancel Goods Receipt Note", "GoodsReceiptNote",
            "Allows cancelling goods receipt notes.");

        AddTenant(permissions, "PROCUREMENT.SETTINGS.VIEW", "View Procurement Approval Settings", "Procurement",
            "Allows viewing procurement approval settings.");
        AddTenant(permissions, "PROCUREMENT.SETTINGS.EDIT", "Edit Procurement Approval Settings", "Procurement",
            "Allows editing procurement approval settings.");

        // =========================
        // Inventory
        // =========================
        AddTenant(permissions, "INVENTORY.VIEW", "View Inventory", "Inventory",
            "Allows viewing inventory balances and stock transactions.");
        AddTenant(permissions, "STOCK_RECEIPT.POST", "Post Stock Receipt", "StockReceipt",
            "Allows posting stock receipts.");
        AddTenant(permissions, "STOCK_ISSUE.POST", "Post Stock Issue", "StockIssue",
            "Allows posting stock issues.");
        AddTenant(permissions, "PUTAWAY.POST", "Post Putaway", "Putaway",
            "Allows creating, confirming and cancelling putaway transactions.");

        AddTenant(permissions, "STOCK_COUNT.VIEW", "View Stock Counts", "StockCount",
            "Allows viewing stock counts.");
        AddTenant(permissions, "STOCK_COUNT.CREATE", "Create Stock Counts", "StockCount",
            "Allows creating stock count drafts.");
        AddTenant(permissions, "STOCK_COUNT.EDIT", "Edit Stock Counts", "StockCount",
            "Allows editing stock count drafts.");
        AddTenant(permissions, "STOCK_COUNT.SUBMIT", "Submit Stock Counts", "StockCount",
            "Allows submitting stock counts for approval.");
        AddTenant(permissions, "STOCK_COUNT.APPROVE", "Approve Stock Counts", "StockCount",
            "Allows approving submitted stock counts.");
        AddTenant(permissions, "STOCK_COUNT.POST", "Post Stock Counts", "StockCount",
            "Allows posting approved stock count adjustments.");
        AddTenant(permissions, "STOCK_COUNT.CANCEL", "Cancel Stock Counts", "StockCount",
            "Allows cancelling unposted stock counts.");

        // =========================
        // Sales
        // =========================
        AddTenant(permissions, "SALES_ORDER.VIEW", "View Sales Order", "SalesOrder",
            "Allows viewing sales orders.");
        AddTenant(permissions, "SALES_ORDER.CREATE", "Create Sales Order", "SalesOrder",
            "Allows creating sales orders.");
        AddTenant(permissions, "SALES_ORDER.EDIT", "Edit Sales Order", "SalesOrder",
            "Allows editing sales orders.");
        AddTenant(permissions, "SALES_ORDER.DELETE", "Delete Sales Order", "SalesOrder",
            "Allows deleting sales order drafts.");
        AddTenant(permissions, "SALES_ORDER.SUBMIT", "Submit Sales Order", "SalesOrder",
            "Allows submitting sales orders.");
        AddTenant(permissions, "SALES_ORDER.CONFIRM", "Confirm/Approve Sales Order", "SalesOrder",
            "Allows confirming or approving sales orders.");
        AddTenant(permissions, "SALES_ORDER.CANCEL", "Cancel Sales Order", "SalesOrder",
            "Allows cancelling sales orders.");

        AddTenant(permissions, "SALES_SHIPMENT.VIEW", "View Sales Shipment", "SalesShipment",
            "Allows viewing sales shipments.");
        AddTenant(permissions, "SALES_SHIPMENT.CREATE", "Create Sales Shipment", "SalesShipment",
            "Allows creating sales shipments.");
        AddTenant(permissions, "SALES_SHIPMENT.EDIT", "Edit Sales Shipment", "SalesShipment",
            "Allows editing sales shipments.");
        AddTenant(permissions, "SALES_SHIPMENT.DELETE", "Delete Sales Shipment", "SalesShipment",
            "Allows deleting sales shipment drafts.");
        AddTenant(permissions, "SALES_SHIPMENT.DISPATCH", "Dispatch Sales Shipment", "SalesShipment",
            "Allows dispatching sales shipments.");
        AddTenant(permissions, "SALES_SHIPMENT.DELIVER", "Deliver Sales Shipment", "SalesShipment",
            "Allows delivering sales shipments.");
        AddTenant(permissions, "SALES_SHIPMENT.CANCEL", "Cancel Sales Shipment", "SalesShipment",
            "Allows cancelling sales shipments.");

        AddTenant(permissions, "SALES_INVOICE.VIEW", "View Sales Invoice", "SalesInvoice",
            "Allows viewing sales invoices.");
        AddTenant(permissions, "SALES_INVOICE.CREATE", "Create Sales Invoice", "SalesInvoice",
            "Allows creating sales invoices.");
        AddTenant(permissions, "SALES_INVOICE.EDIT", "Edit Sales Invoice", "SalesInvoice",
            "Allows editing sales invoices.");
        AddTenant(permissions, "SALES_INVOICE.CONFIRM", "Confirm Sales Invoice", "SalesInvoice",
            "Allows confirming sales invoices.");
        AddTenant(permissions, "SALES_INVOICE.CANCEL", "Cancel Sales Invoice", "SalesInvoice",
            "Allows cancelling sales invoices.");

        // =========================
        // CRM
        // =========================
        AddTenant(permissions, "CRM.LEAD.VIEW", "View CRM Leads", "CRM",
            "Allows viewing CRM leads.");
        AddTenant(permissions, "CRM.LEAD.CREATE", "Create CRM Lead", "CRM",
            "Allows creating CRM leads.");
        AddTenant(permissions, "CRM.LEAD.EDIT", "Edit CRM Lead", "CRM",
            "Allows editing CRM leads.");
        AddTenant(permissions, "CRM.LEAD.QUALIFY", "Qualify/Disqualify CRM Lead", "CRM",
            "Allows qualifying or disqualifying CRM leads.");
        AddTenant(permissions, "CRM.LEAD.CONVERT", "Convert CRM Lead", "CRM",
            "Allows converting CRM leads to opportunities.");

        AddTenant(permissions, "CRM.OPPORTUNITY.VIEW", "View CRM Opportunities", "CRM",
            "Allows viewing CRM opportunities.");
        AddTenant(permissions, "CRM.OPPORTUNITY.MANAGE", "Manage CRM Opportunities", "CRM",
            "Allows managing CRM opportunities.");

        AddTenant(permissions, "CRM.ACTIVITY.VIEW", "View CRM Activities", "CRM",
            "Allows viewing CRM activities.");
        AddTenant(permissions, "CRM.ACTIVITY.MANAGE", "Manage CRM Activities", "CRM",
            "Allows managing CRM activities.");

        return permissions;
    }

    private static void AddPlatform(
        List<PermissionDefinition> permissions,
        string code,
        string name,
        string module,
        string description)
    {
        permissions.Add(new PermissionDefinition(
            code, name, module, description, PermissionCategory.Platform));
    }

    private static void AddTenant(
        List<PermissionDefinition> permissions,
        string code,
        string name,
        string module,
        string description)
    {
        permissions.Add(new PermissionDefinition(
            code, name, module, description, PermissionCategory.Tenant));
    }
}
