using ERP.Domain.Features.MasterData.Category;
using Domain.Features.MasterData.Branch;
using ERP.Domain.Features.MasterData.Customer;
using ERP.Domain.Features.MasterData.Supplier;
using ERP.Domain.Features.MasterData.Warehouse;
using ERP.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.Contact;
using Domain.Features.MasterData.Supplier;

namespace Domain.Infrastructure.Persistence;

public class DomainDbContext : DbContext
{
    public DomainDbContext(
        DbContextOptions<DomainDbContext> options)
        : base(options)
    {
    }

    public DbSet<Company> Companies
        => Set<Company>();

    public DbSet<Branch> Branches
        => Set<Branch>();

    public DbSet<Product> Products
        => Set<Product>();

    public DbSet<Category> Categories
        => Set<Category>();

    public DbSet<Customer> Customers
        => Set<Customer>();

    public DbSet<Supplier> Suppliers
        => Set<Supplier>();

    public DbSet<Warehouse> Warehouses
        => Set<Warehouse>();

    public DbSet<WarehouseZone> WarehouseZones
        => Set<WarehouseZone>();

    public DbSet<WarehouseLocation> WarehouseLocations
        => Set<WarehouseLocation>();

    public DbSet<Domain.Features.MasterData.Country.Country> Countries
        => Set<Domain.Features.MasterData.Country.Country>();

    public DbSet<Domain.Features.MasterData.AddressType.AddressType> AddressTypes
        => Set<Domain.Features.MasterData.AddressType.AddressType>();

    public DbSet<Domain.Features.MasterData.Address.Address> Addresses
        => Set<Domain.Features.MasterData.Address.Address>();

    public DbSet<CustomerAddress> CustomerAddresses
        => Set<CustomerAddress>();

    public DbSet<SupplierAddress> SupplierAddresses
        => Set<SupplierAddress>();

    public DbSet<CompanyAddress> CompanyAddresses
        => Set<CompanyAddress>();

    public DbSet<BranchAddress> BranchAddresses
        => Set<BranchAddress>();

    public DbSet<WarehouseAddress> WarehouseAddresses
        => Set<WarehouseAddress>();

    public DbSet<ContactType> ContactTypes
        => Set<ContactType>();

    public DbSet<Domain.Features.MasterData.Contact.Contact> Contacts
        => Set<Domain.Features.MasterData.Contact.Contact>();

    public DbSet<CustomerContact> CustomerContacts
        => Set<CustomerContact>();

    public DbSet<SupplierContact> SupplierContacts
        => Set<SupplierContact>();

    public DbSet<Domain.Features.MasterData.UnitOfMeasure.UnitOfMeasure> UnitsOfMeasure
        => Set<Domain.Features.MasterData.UnitOfMeasure.UnitOfMeasure>();

    public DbSet<Domain.Features.MasterData.UnitOfMeasure.UnitOfMeasureConversion> UnitOfMeasureConversions
        => Set<Domain.Features.MasterData.UnitOfMeasure.UnitOfMeasureConversion>();

    public DbSet<LocationType> LocationTypes
        => Set<LocationType>();

    public DbSet<SupplierProductPrice> SupplierProductPrices
        => Set<SupplierProductPrice>();

    public DbSet<Domain.Features.Procurement.PurchaseOrder.PurchaseOrder> PurchaseOrders
        => Set<Domain.Features.Procurement.PurchaseOrder.PurchaseOrder>();

    public DbSet<Domain.Features.Procurement.PurchaseOrder.PurchaseOrderItem> PurchaseOrderItems
        => Set<Domain.Features.Procurement.PurchaseOrder.PurchaseOrderItem>();

    public DbSet<Domain.Features.Procurement.PurchaseRequisition.PurchaseRequisition> PurchaseRequisitions
        => Set<Domain.Features.Procurement.PurchaseRequisition.PurchaseRequisition>();

    public DbSet<Domain.Features.Procurement.PurchaseRequisition.PurchaseRequisitionItem> PurchaseRequisitionItems
        => Set<Domain.Features.Procurement.PurchaseRequisition.PurchaseRequisitionItem>();

    public DbSet<Domain.Features.Procurement.Approval.ApprovalAuditRecord> ApprovalAuditRecords
        => Set<Domain.Features.Procurement.Approval.ApprovalAuditRecord>();

    public DbSet<DomainAuditRecord> DomainAuditRecords
        => Set<DomainAuditRecord>();

    public DbSet<ERP.Domain.Features.Inventory.InventoryTransaction> InventoryTransactions
        => Set<ERP.Domain.Features.Inventory.InventoryTransaction>();

    public DbSet<ERP.Domain.Features.Inventory.InventoryBalance> InventoryBalances
        => Set<ERP.Domain.Features.Inventory.InventoryBalance>();

    public DbSet<Domain.Features.Procurement.GoodsReceiptNote.GoodsReceiptNote> GoodsReceiptNotes
        => Set<Domain.Features.Procurement.GoodsReceiptNote.GoodsReceiptNote>();

    public DbSet<Domain.Features.Procurement.GoodsReceiptNote.GoodsReceiptNoteLine> GoodsReceiptNoteLines
        => Set<Domain.Features.Procurement.GoodsReceiptNote.GoodsReceiptNoteLine>();

    public DbSet<ERP.Domain.Features.Inventory.Valuation.InventoryCostLayer> InventoryCostLayers
        => Set<ERP.Domain.Features.Inventory.Valuation.InventoryCostLayer>();

    public DbSet<ERP.Domain.Features.Inventory.Valuation.ProductValuationBalance> ProductValuationBalances
        => Set<ERP.Domain.Features.Inventory.Valuation.ProductValuationBalance>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(DomainDbContext).Assembly);
    }
}