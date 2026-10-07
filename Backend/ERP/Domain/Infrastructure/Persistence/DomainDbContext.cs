using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Features.MasterData.Branch;
using ERP.Domain.Features.MasterData.Customer;
using ERP.Domain.Features.MasterData.Supplier;
using ERP.Domain.Features.MasterData.Warehouse;
using ERP.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;

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

    public DbSet<DomainAuditRecord> DomainAuditRecords
        => Set<DomainAuditRecord>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(DomainDbContext).Assembly);
    }
}