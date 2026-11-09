using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Security.Bootstrap.Permissions.Domain;
using Security.Bootstrap.Roles;
using Security.Core.Models;
using Security.Infrastructure.Persistence;
using Security.Infrastructure.Persistence.Repositories;
using Xunit;

namespace ERP.SecurityTests.Sales;

public class SalesInvoicePermissionTests
{
    private static SecurityDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SecurityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SecurityDbContext(options);
    }

    [Fact]
    public async Task SalesInvoicePermissionSeed_SeedsAllExpectedPermissions()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new SecurityBootstrapRepository(context);

        await SalesInvoicePermissionSeed.SeedAsync(repo);

        var permissions = await context.Permissions.ToListAsync();
        Assert.Equal(5, permissions.Count);

        var codes = permissions.Select(p => p.Code).ToHashSet();
        Assert.Contains("SALES_INVOICE.VIEW", codes);
        Assert.Contains("SALES_INVOICE.CREATE", codes);
        Assert.Contains("SALES_INVOICE.EDIT", codes);
        Assert.Contains("SALES_INVOICE.CONFIRM", codes);
        Assert.Contains("SALES_INVOICE.CANCEL", codes);

        Assert.All(permissions, p =>
        {
            Assert.Equal("SalesInvoice", p.Module);
            Assert.True(p.IsActive);
        });
    }

    [Fact]
    public async Task SalesInvoicePermissionSeed_IsIdempotent()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new SecurityBootstrapRepository(context);

        await SalesInvoicePermissionSeed.SeedAsync(repo);
        await SalesInvoicePermissionSeed.SeedAsync(repo);

        var count = await context.Permissions.CountAsync();
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task TenantAdminRolePermissionSeed_AssignsSalesInvoicePermissionsToTenantAdmin()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new SecurityBootstrapRepository(context);

        // 1. Create Tenant Admin role
        var tenantAdminRole = new Role
        {
            Id = Guid.NewGuid(),
            Name = "Tenant Admin",
            Description = "Tenant Administrator",
            IsActive = true
        };
        context.Roles.Add(tenantAdminRole);
        await context.SaveChangesAsync();

        // 2. Seed all domain permissions
        await ERP.Domain.Bootstrap.Permissions.DomainPermissionSeeder.SeedAsync(repo);
        await ProductPermissionSeed.SeedAsync(repo);
        await CustomerPermissionSeed.SeedAsync(repo);
        await SupplierPermissionSeed.SeedAsync(repo);
        await PurchaseOrderPermissionSeed.SeedAsync(repo);
        await PurchaseRequisitionPermissionSeed.SeedAsync(repo);
        await GoodsReceiptNotePermissionSeed.SeedAsync(repo);
        await SalesOrderPermissionSeed.SeedAsync(repo);
        await SalesShipmentPermissionSeed.SeedAsync(repo);
        await SalesInvoicePermissionSeed.SeedAsync(repo);

        // 3. Seed Tenant Admin Role Permissions
        await TenantAdminRolePermissionSeed.SeedAsync(repo);

        // 4. Verify that SALES_INVOICE permissions are linked to Tenant Admin Role
        var rolePermissions = await context.RolePermissions
            .Include(rp => rp.Permission)
            .Where(rp => rp.RoleId == tenantAdminRole.Id)
            .ToListAsync();

        var assignedCodes = rolePermissions.Select(rp => rp.Permission.Code).ToHashSet();
        Assert.Contains("SALES_INVOICE.VIEW", assignedCodes);
        Assert.Contains("SALES_INVOICE.CREATE", assignedCodes);
        Assert.Contains("SALES_INVOICE.EDIT", assignedCodes);
        Assert.Contains("SALES_INVOICE.CONFIRM", assignedCodes);
        Assert.Contains("SALES_INVOICE.CANCEL", assignedCodes);
        Assert.Contains("STOCK_COUNT.VIEW", assignedCodes);
        Assert.Contains("STOCK_COUNT.CREATE", assignedCodes);
        Assert.Contains("STOCK_COUNT.EDIT", assignedCodes);
        Assert.Contains("STOCK_COUNT.SUBMIT", assignedCodes);
        Assert.Contains("STOCK_COUNT.APPROVE", assignedCodes);
        Assert.Contains("STOCK_COUNT.POST", assignedCodes);
        Assert.Contains("STOCK_COUNT.CANCEL", assignedCodes);
    }
}
