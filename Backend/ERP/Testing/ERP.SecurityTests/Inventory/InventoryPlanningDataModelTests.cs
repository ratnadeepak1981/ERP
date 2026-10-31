using System;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.Inventory;

public class InventoryPlanningDataModelTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public InventoryPlanningDataModelTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Product_DefaultPlanningSettings_AreNullOrZero()
    {
        var tenantId = Guid.NewGuid();
        var product = Product.Create(
            tenantId,
            $"P-{Guid.NewGuid():N}"[..12],
            "Test Product",
            Guid.NewGuid(),
            "PCS");

        Assert.Equal(0m, product.ReorderLevel);
        Assert.Equal(0m, product.ReorderQuantity);
        Assert.Null(product.MinStockQuantity);
        Assert.Null(product.MaxStockQuantity);
        Assert.Null(product.SafetyStockQuantity);
        Assert.Null(product.LeadTimeDays);
    }

    [Fact]
    public void Product_ValidPlanningSettings_AreAssignedCorrectly()
    {
        var tenantId = Guid.NewGuid();
        var product = Product.Create(
            tenantId,
            $"P-{Guid.NewGuid():N}"[..12],
            "Test Product",
            Guid.NewGuid(),
            "PCS",
            reorderLevel: 25m,
            reorderQuantity: 100m,
            minStockQuantity: 10m,
            maxStockQuantity: 200m,
            safetyStockQuantity: 5m,
            leadTimeDays: 14);

        Assert.Equal(25m, product.ReorderLevel);
        Assert.Equal(100m, product.ReorderQuantity);
        Assert.Equal(10m, product.MinStockQuantity);
        Assert.Equal(200m, product.MaxStockQuantity);
        Assert.Equal(5m, product.SafetyStockQuantity);
        Assert.Equal(14, product.LeadTimeDays);
    }

    [Fact]
    public void Product_UpdatePlanningSettings_ModifiesSettings()
    {
        var tenantId = Guid.NewGuid();
        var product = Product.Create(
            tenantId,
            $"P-{Guid.NewGuid():N}"[..12],
            "Test Product",
            Guid.NewGuid(),
            "PCS");

        product.UpdatePlanningSettings(
            reorderLevel: 50m,
            reorderQuantity: 150m,
            minStockQuantity: 20m,
            maxStockQuantity: 300m,
            safetyStockQuantity: 10m,
            leadTimeDays: 7);

        Assert.Equal(50m, product.ReorderLevel);
        Assert.Equal(150m, product.ReorderQuantity);
        Assert.Equal(20m, product.MinStockQuantity);
        Assert.Equal(300m, product.MaxStockQuantity);
        Assert.Equal(10m, product.SafetyStockQuantity);
        Assert.Equal(7, product.LeadTimeDays);
    }

    [Theory]
    [InlineData(-1, 10, 0, 100, 0, 5)]     // Negative reorderLevel
    [InlineData(10, -1, 0, 100, 0, 5)]     // Negative reorderQuantity
    [InlineData(10, 10, -1, 100, 0, 5)]    // Negative minStockQuantity
    [InlineData(10, 10, 0, -1, 0, 5)]      // Negative maxStockQuantity
    [InlineData(10, 10, 0, 100, -1, 5)]    // Negative safetyStockQuantity
    [InlineData(10, 10, 0, 100, 0, -1)]    // Negative leadTimeDays
    public void Product_NegativePlanningSettings_ThrowsArgumentOutOfRangeException(
        decimal reorderLevel,
        decimal reorderQuantity,
        decimal minStock,
        decimal maxStock,
        decimal safetyStock,
        int leadTime)
    {
        var tenantId = Guid.NewGuid();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Product.Create(
                tenantId,
                $"P-{Guid.NewGuid():N}"[..12],
                "Invalid Product",
                Guid.NewGuid(),
                "PCS",
                reorderLevel: reorderLevel,
                reorderQuantity: reorderQuantity,
                minStockQuantity: minStock < 0 ? minStock : null,
                maxStockQuantity: maxStock < 0 ? maxStock : null,
                safetyStockQuantity: safetyStock < 0 ? safetyStock : null,
                leadTimeDays: leadTime < 0 ? leadTime : null));
    }

    [Fact]
    public void Product_MinGreaterThanMax_ThrowsInvalidOperationException()
    {
        var tenantId = Guid.NewGuid();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            Product.Create(
                tenantId,
                $"P-{Guid.NewGuid():N}"[..12],
                "Invalid Product",
                Guid.NewGuid(),
                "PCS",
                minStockQuantity: 100m,
                maxStockQuantity: 50m));

        Assert.Contains("Minimum stock quantity cannot be greater than maximum stock quantity", ex.Message);
    }

    [Fact]
    public void Product_SafetyGreaterThanMax_ThrowsInvalidOperationException()
    {
        var tenantId = Guid.NewGuid();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            Product.Create(
                tenantId,
                $"P-{Guid.NewGuid():N}"[..12],
                "Invalid Product",
                Guid.NewGuid(),
                "PCS",
                minStockQuantity: 10m,
                maxStockQuantity: 50m,
                safetyStockQuantity: 60m));

        Assert.Contains("Safety stock quantity cannot be greater than maximum stock quantity", ex.Message);
    }

    [Fact]
    public void Product_SafetyGreaterThanMin_ThrowsInvalidOperationException()
    {
        var tenantId = Guid.NewGuid();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            Product.Create(
                tenantId,
                $"P-{Guid.NewGuid():N}"[..12],
                "Invalid Product",
                Guid.NewGuid(),
                "PCS",
                minStockQuantity: 20m,
                maxStockQuantity: 100m,
                safetyStockQuantity: 30m));

        Assert.Contains("Safety stock quantity cannot be greater than minimum stock quantity", ex.Message);
    }

    [Fact]
    public void ProductWarehouseSetting_CreateAndLifecycle_Succeeds()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var setting = ProductWarehouseSetting.Create(
            tenantId,
            productId,
            warehouseId,
            reorderLevel: 40m,
            reorderQuantity: 200m,
            minStockQuantity: 15m,
            maxStockQuantity: 500m,
            safetyStockQuantity: 10m,
            leadTimeDays: 5);

        Assert.True(setting.IsActive);
        Assert.Equal(tenantId, setting.TenantId);
        Assert.Equal(productId, setting.ProductId);
        Assert.Equal(warehouseId, setting.WarehouseId);
        Assert.Equal(40m, setting.ReorderLevel);
        Assert.Equal(200m, setting.ReorderQuantity);
        Assert.Equal(15m, setting.MinStockQuantity);
        Assert.Equal(500m, setting.MaxStockQuantity);
        Assert.Equal(10m, setting.SafetyStockQuantity);
        Assert.Equal(5, setting.LeadTimeDays);

        // Update
        setting.Update(
            reorderLevel: 60m,
            reorderQuantity: 300m,
            minStockQuantity: 25m,
            maxStockQuantity: 600m,
            safetyStockQuantity: 20m,
            leadTimeDays: 8);

        Assert.Equal(60m, setting.ReorderLevel);
        Assert.Equal(300m, setting.ReorderQuantity);
        Assert.Equal(25m, setting.MinStockQuantity);
        Assert.Equal(600m, setting.MaxStockQuantity);
        Assert.Equal(20m, setting.SafetyStockQuantity);
        Assert.Equal(8, setting.LeadTimeDays);

        // Deactivate / Activate
        setting.Deactivate();
        Assert.False(setting.IsActive);

        setting.Activate();
        Assert.True(setting.IsActive);
    }

    [Fact]
    public void ProductWarehouseSetting_EmptyIds_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            ProductWarehouseSetting.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid()));

        Assert.Throws<ArgumentException>(() =>
            ProductWarehouseSetting.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid()));

        Assert.Throws<ArgumentException>(() =>
            ProductWarehouseSetting.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void ProductWarehouseSetting_InvalidRanges_ThrowsExceptions()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // Negative
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProductWarehouseSetting.Create(tenantId, productId, warehouseId, reorderLevel: -5m));

        // Min > Max
        Assert.Throws<InvalidOperationException>(() =>
            ProductWarehouseSetting.Create(tenantId, productId, warehouseId, minStockQuantity: 100m, maxStockQuantity: 50m));

        // Safety > Max
        Assert.Throws<InvalidOperationException>(() =>
            ProductWarehouseSetting.Create(tenantId, productId, warehouseId, maxStockQuantity: 50m, safetyStockQuantity: 60m));

        // Safety > Min
        Assert.Throws<InvalidOperationException>(() =>
            ProductWarehouseSetting.Create(tenantId, productId, warehouseId, minStockQuantity: 30m, safetyStockQuantity: 40m));
    }

    [Fact]
    public void Precedence_NoWarehouseOverride_ReturnsProductDefaults()
    {
        var tenantId = Guid.NewGuid();
        var product = Product.Create(
            tenantId,
            $"P-{Guid.NewGuid():N}"[..12],
            "Test Product",
            Guid.NewGuid(),
            "PCS",
            reorderLevel: 20m,
            reorderQuantity: 80m,
            minStockQuantity: 10m,
            maxStockQuantity: 200m,
            safetyStockQuantity: 5m,
            leadTimeDays: 10);

        var effective = product.GetEffectivePlanningSettings(null);

        Assert.False(effective.IsWarehouseOverrideApplied);
        Assert.Null(effective.WarehouseId);
        Assert.Equal(20m, effective.ReorderLevel);
        Assert.Equal(80m, effective.ReorderQuantity);
        Assert.Equal(10m, effective.MinStockQuantity);
        Assert.Equal(200m, effective.MaxStockQuantity);
        Assert.Equal(5m, effective.SafetyStockQuantity);
        Assert.Equal(10, effective.LeadTimeDays);
    }

    [Fact]
    public void Precedence_InactiveWarehouseOverride_FallsBackToProductDefaults()
    {
        var tenantId = Guid.NewGuid();
        var product = Product.Create(
            tenantId,
            $"P-{Guid.NewGuid():N}"[..12],
            "Test Product",
            Guid.NewGuid(),
            "PCS",
            reorderLevel: 20m,
            reorderQuantity: 80m);

        var warehouseSetting = ProductWarehouseSetting.Create(
            tenantId,
            product.ProductId,
            Guid.NewGuid(),
            reorderLevel: 50m,
            reorderQuantity: 200m);
        warehouseSetting.Deactivate();

        var effective = product.GetEffectivePlanningSettings(warehouseSetting);

        Assert.False(effective.IsWarehouseOverrideApplied);
        Assert.Equal(20m, effective.ReorderLevel);
        Assert.Equal(80m, effective.ReorderQuantity);
    }

    [Fact]
    public void Precedence_MismatchedProductIdWarehouseOverride_FallsBackToProductDefaults()
    {
        var tenantId = Guid.NewGuid();
        var product = Product.Create(
            tenantId,
            $"P-{Guid.NewGuid():N}"[..12],
            "Test Product",
            Guid.NewGuid(),
            "PCS",
            reorderLevel: 20m);

        var warehouseSetting = ProductWarehouseSetting.Create(
            tenantId,
            Guid.NewGuid(), // Different product
            Guid.NewGuid(),
            reorderLevel: 99m);

        var effective = product.GetEffectivePlanningSettings(warehouseSetting);

        Assert.False(effective.IsWarehouseOverrideApplied);
        Assert.Equal(20m, effective.ReorderLevel);
    }

    [Fact]
    public void Precedence_PartialWarehouseOverride_OverridesSpecifiedAndInheritsRemaining()
    {
        var tenantId = Guid.NewGuid();
        var product = Product.Create(
            tenantId,
            $"P-{Guid.NewGuid():N}"[..12],
            "Test Product",
            Guid.NewGuid(),
            "PCS",
            reorderLevel: 20m,
            reorderQuantity: 80m,
            minStockQuantity: 10m,
            maxStockQuantity: 300m,
            safetyStockQuantity: 5m,
            leadTimeDays: 14);

        var warehouseId = Guid.NewGuid();
        // Override only ReorderLevel and MinStock; leave remaining fields null
        var warehouseSetting = ProductWarehouseSetting.Create(
            tenantId,
            product.ProductId,
            warehouseId,
            reorderLevel: 55m,
            minStockQuantity: 30m);

        var effective = product.GetEffectivePlanningSettings(warehouseSetting);

        Assert.True(effective.IsWarehouseOverrideApplied);
        Assert.Equal(warehouseId, effective.WarehouseId);
        // Overridden
        Assert.Equal(55m, effective.ReorderLevel);
        Assert.Equal(30m, effective.MinStockQuantity);
        // Inherited from Product defaults
        Assert.Equal(80m, effective.ReorderQuantity);
        Assert.Equal(300m, effective.MaxStockQuantity);
        Assert.Equal(5m, effective.SafetyStockQuantity);
        Assert.Equal(14, effective.LeadTimeDays);

        // Effective settings validate successfully
        effective.Validate();
    }

    [Fact]
    public void Precedence_CrossLevelBoundsConflict_EffectiveValidateThrowsException()
    {
        var tenantId = Guid.NewGuid();
        var product = Product.Create(
            tenantId,
            $"P-{Guid.NewGuid():N}"[..12],
            "Test Product",
            Guid.NewGuid(),
            "PCS",
            minStockQuantity: 100m,
            maxStockQuantity: 500m);

        var warehouseSetting = ProductWarehouseSetting.Create(
            tenantId,
            product.ProductId,
            Guid.NewGuid(),
            maxStockQuantity: 50m); // Overrides max to 50, but product min was 100

        var effective = product.GetEffectivePlanningSettings(warehouseSetting);

        Assert.Equal(100m, effective.MinStockQuantity);
        Assert.Equal(50m, effective.MaxStockQuantity);

        var ex = Assert.Throws<InvalidOperationException>(() => effective.Validate());
        Assert.Contains("Effective minimum stock quantity cannot be greater than effective maximum stock quantity", ex.Message);
    }

    [Fact]
    public async Task Persistence_ProductAndWarehouseSetting_SavedAndQueriedSuccessfully()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var tenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // Seed Category & Warehouse
        var category = ERP.Domain.Features.MasterData.Category.Category.Create(
            tenantId,
            $"CAT-{Guid.NewGuid():N}"[..12],
            "Planning Test Category");
        db.Categories.Add(category);

        var warehouse = Warehouse.Create(
            tenantId,
            $"WH-{Guid.NewGuid():N}"[..12],
            "Planning Test Warehouse");
        db.Warehouses.Add(warehouse);

        var product = Product.Create(
            tenantId,
            $"P-{Guid.NewGuid():N}"[..12],
            "Planning Persisted Product",
            category.CategoryId,
            "PCS",
            reorderLevel: 15m,
            reorderQuantity: 60m,
            minStockQuantity: 10m,
            maxStockQuantity: 150m,
            safetyStockQuantity: 5m,
            leadTimeDays: 7);
        db.Products.Add(product);

        var warehouseSetting = ProductWarehouseSetting.Create(
            tenantId,
            product.ProductId,
            warehouse.WarehouseId,
            reorderLevel: 25m,
            reorderQuantity: 100m,
            minStockQuantity: 18m,
            maxStockQuantity: 250m,
            safetyStockQuantity: 8m,
            leadTimeDays: 4);
        db.ProductWarehouseSettings.Add(warehouseSetting);

        await db.SaveChangesAsync();

        // Query back from fresh scope
        using var queryScope = _fixture.Services.CreateScope();
        var queryDb = queryScope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var loadedProduct = await queryDb.Products
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == product.ProductId);
        Assert.NotNull(loadedProduct);
        Assert.Equal(15m, loadedProduct.ReorderLevel);
        Assert.Equal(60m, loadedProduct.ReorderQuantity);
        Assert.Equal(10m, loadedProduct.MinStockQuantity);
        Assert.Equal(150m, loadedProduct.MaxStockQuantity);
        Assert.Equal(5m, loadedProduct.SafetyStockQuantity);
        Assert.Equal(7, loadedProduct.LeadTimeDays);

        var loadedSetting = await queryDb.ProductWarehouseSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ProductId == product.ProductId && s.WarehouseId == warehouse.WarehouseId);
        Assert.NotNull(loadedSetting);
        Assert.Equal(25m, loadedSetting.ReorderLevel);
        Assert.Equal(100m, loadedSetting.ReorderQuantity);
        Assert.Equal(18m, loadedSetting.MinStockQuantity);
        Assert.Equal(250m, loadedSetting.MaxStockQuantity);
        Assert.Equal(8m, loadedSetting.SafetyStockQuantity);
        Assert.Equal(4, loadedSetting.LeadTimeDays);

        // Verify precedence on persisted entities
        var resolved = loadedProduct.GetEffectivePlanningSettings(loadedSetting);
        Assert.True(resolved.IsWarehouseOverrideApplied);
        Assert.Equal(warehouse.WarehouseId, resolved.WarehouseId);
        Assert.Equal(25m, resolved.ReorderLevel);
        Assert.Equal(100m, resolved.ReorderQuantity);
        Assert.Equal(18m, resolved.MinStockQuantity);
        Assert.Equal(250m, resolved.MaxStockQuantity);
        Assert.Equal(8m, resolved.SafetyStockQuantity);
        Assert.Equal(4, resolved.LeadTimeDays);
    }

    [Fact]
    public async Task Persistence_DuplicateWarehouseSetting_ThrowsDbUpdateException()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var tenantId = Guid.NewGuid();

        var category = ERP.Domain.Features.MasterData.Category.Category.Create(
            tenantId,
            $"CAT-{Guid.NewGuid():N}"[..12],
            "Dup Test Category");
        db.Categories.Add(category);

        var warehouse = Warehouse.Create(
            tenantId,
            $"WH-{Guid.NewGuid():N}"[..12],
            "Dup Test Warehouse");
        db.Warehouses.Add(warehouse);

        var product = Product.Create(
            tenantId,
            $"P-{Guid.NewGuid():N}"[..12],
            "Dup Persisted Product",
            category.CategoryId,
            "PCS");
        db.Products.Add(product);

        var setting1 = ProductWarehouseSetting.Create(
            tenantId,
            product.ProductId,
            warehouse.WarehouseId,
            reorderLevel: 10m);
        db.ProductWarehouseSettings.Add(setting1);

        await db.SaveChangesAsync();

        // Attempt duplicate setting for same Tenant, Product, and Warehouse
        var setting2 = ProductWarehouseSetting.Create(
            tenantId,
            product.ProductId,
            warehouse.WarehouseId,
            reorderLevel: 20m);
        db.ProductWarehouseSettings.Add(setting2);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
