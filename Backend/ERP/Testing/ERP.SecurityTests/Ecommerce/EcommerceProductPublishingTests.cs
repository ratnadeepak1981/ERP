using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Common.TenantResolution;
using API.Features.Ecommerce;
using Domain.Features.Ecommerce;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.Ecommerce;
using ERP.Domain.Features.MasterData.Category;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Security.Interfaces;
using Xunit;

namespace ERP.SecurityTests.Ecommerce;

public class EcommerceProductPublishingTests
{
    private readonly DomainDbContext _context;
    private readonly EcommerceProductService _service;
    private readonly Guid _tenantAlphaId = Guid.NewGuid();
    private readonly Guid _tenantBetaId = Guid.NewGuid();
    private readonly Guid _companyAId = Guid.NewGuid();
    private readonly Guid _companyBId = Guid.NewGuid();
    private readonly Guid _companyBetaId = Guid.NewGuid();
    private readonly Guid _categoryAlphaId = Guid.NewGuid();

    public EcommerceProductPublishingTests()
    {
        var options = new DbContextOptionsBuilder<DomainDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DomainDbContext(options);
        _service = new EcommerceProductService(_context, NullLogger<EcommerceProductService>.Instance);

        SeedInitialData();
    }

    private void SeedInitialData()
    {
        // Companies
        var compA = new Company { Id = _companyAId, TenantId = _tenantAlphaId, Code = "CMP-A", Name = "Company A", IsActive = true };
        var compB = new Company { Id = _companyBId, TenantId = _tenantAlphaId, Code = "CMP-B", Name = "Company B", IsActive = true };
        var compBeta = new Company { Id = _companyBetaId, TenantId = _tenantBetaId, Code = "CMP-BETA", Name = "Company Beta", IsActive = true };

        _context.Companies.AddRange(compA, compB, compBeta);

        // Categories
        var cat = Category.Create(_tenantAlphaId, "CAT-01", "Hardware", categoryId: _categoryAlphaId);
        _context.Categories.Add(cat);

        _context.SaveChanges();
    }

    private Product CreateProduct(
        Guid tenantId,
        string code,
        string name,
        decimal sellingPrice = 150m,
        decimal standardCost = 90m,
        bool isActive = true,
        bool isSellable = true)
    {
        var p = Product.Create(
            tenantId,
            code,
            name,
            _categoryAlphaId,
            unitOfMeasure: "PCS",
            sellingPrice: sellingPrice,
            standardCost: standardCost,
            isSellable: isSellable);

        if (!isActive)
        {
            p.Deactivate();
        }

        _context.Products.Add(p);
        _context.SaveChanges();
        return p;
    }

    [Fact]
    public async Task Test01_CompanyCanCreatePublication_ForExistingEligibleProduct_Succeeds()
    {
        var prod = CreateProduct(_tenantAlphaId, "PRD-01", "Power Drill", 120m, 60m);

        var request = new CreateEcommerceProductRequest
        {
            CompanyId = _companyAId,
            ProductId = prod.ProductId,
            OnlineDescription = "High performance cordless power drill",
            DisplayOrder = 1,
            IsPublished = true
        };

        var result = await _service.CreatePublicationAsync(_tenantAlphaId, _companyAId, request);

        Assert.NotNull(result);
        Assert.Equal(_companyAId, result.CompanyId);
        Assert.Equal(prod.ProductId, result.ProductId);
        Assert.True(result.IsPublished);
        Assert.Equal("High performance cordless power drill", result.OnlineDescription);
        Assert.Equal(1, result.DisplayOrder);
        Assert.Equal(120m, result.SellingPrice);

        // Verify database persistence
        var persisted = await _context.EcommerceProducts.FirstOrDefaultAsync(ep => ep.Id == result.Id);
        Assert.NotNull(persisted);
        Assert.Equal(_companyAId, persisted.CompanyId);
        Assert.True(persisted.IsPublished);
    }

    [Fact]
    public async Task Test02_DuplicatePublication_SameTenantCompanyAndProduct_IsRejected()
    {
        var prod = CreateProduct(_tenantAlphaId, "PRD-02", "Hammer", 25m, 10m);

        var request = new CreateEcommerceProductRequest
        {
            CompanyId = _companyAId,
            ProductId = prod.ProductId,
            OnlineDescription = "Steel claw hammer",
            DisplayOrder = 1,
            IsPublished = false
        };

        await _service.CreatePublicationAsync(_tenantAlphaId, _companyAId, request);

        // Second attempt for same Company + Product
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreatePublicationAsync(_tenantAlphaId, _companyAId, request));

        Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test03_DifferentCompanies_CanPublishSameProduct_Independently()
    {
        var prod = CreateProduct(_tenantAlphaId, "PRD-SHARED", "Shared Industrial Wrench", 80m, 40m);

        // Company A creates publication
        var pubA = await _service.CreatePublicationAsync(_tenantAlphaId, _companyAId, new CreateEcommerceProductRequest
        {
            CompanyId = _companyAId,
            ProductId = prod.ProductId,
            OnlineDescription = "Company A Wrench Offer",
            DisplayOrder = 10,
            IsPublished = true
        });

        // Company B creates publication for the same ERP product
        var pubB = await _service.CreatePublicationAsync(_tenantAlphaId, _companyBId, new CreateEcommerceProductRequest
        {
            CompanyId = _companyBId,
            ProductId = prod.ProductId,
            OnlineDescription = "Company B Wrench Special",
            DisplayOrder = 5,
            IsPublished = false // Unfinished draft for Company B
        });

        Assert.NotEqual(pubA.Id, pubB.Id);
        Assert.Equal(_companyAId, pubA.CompanyId);
        Assert.Equal(_companyBId, pubB.CompanyId);
        Assert.True(pubA.IsPublished);
        Assert.False(pubB.IsPublished);
    }

    [Fact]
    public async Task Test04_InactiveProduct_CannotBePublished_ThrowsInvalidOperationException()
    {
        var inactiveProd = CreateProduct(_tenantAlphaId, "PRD-INACTIVE", "Deprecated Item", 50m, 20m, isActive: false);

        var request = new CreateEcommerceProductRequest
        {
            CompanyId = _companyAId,
            ProductId = inactiveProd.ProductId,
            IsPublished = true
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreatePublicationAsync(_tenantAlphaId, _companyAId, request));

        Assert.Contains("inactive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test05_NonSellableProduct_CannotBePublished_ThrowsInvalidOperationException()
    {
        var nonSellableProd = CreateProduct(_tenantAlphaId, "PRD-RAW-MAT", "Raw Steel Ingot", 500m, 300m, isActive: true, isSellable: false);

        var request = new CreateEcommerceProductRequest
        {
            CompanyId = _companyAId,
            ProductId = nonSellableProd.ProductId,
            IsPublished = true
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreatePublicationAsync(_tenantAlphaId, _companyAId, request));

        Assert.Contains("non-sellable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test06_PublishedProducts_AppearInPublicCatalogue()
    {
        var prod1 = CreateProduct(_tenantAlphaId, "PRD-PUB-1", "Screwdriver Set", 45m, 20m);
        var prod2 = CreateProduct(_tenantAlphaId, "PRD-PUB-2", "Pliers", 30m, 15m);

        await _service.CreatePublicationAsync(_tenantAlphaId, _companyAId, new CreateEcommerceProductRequest
        {
            CompanyId = _companyAId,
            ProductId = prod1.ProductId,
            DisplayOrder = 1,
            IsPublished = true
        });

        await _service.CreatePublicationAsync(_tenantAlphaId, _companyAId, new CreateEcommerceProductRequest
        {
            CompanyId = _companyAId,
            ProductId = prod2.ProductId,
            DisplayOrder = 2,
            IsPublished = true
        });

        var catalogue = await _service.GetPublicCatalogueAsync(_companyAId, tenantId: _tenantAlphaId);

        Assert.Equal(2, catalogue.Count);
        Assert.Contains(catalogue, p => p.ProductId == prod1.ProductId);
        Assert.Contains(catalogue, p => p.ProductId == prod2.ProductId);
    }

    [Fact]
    public async Task Test07_UnpublishedProducts_DoNotAppearPublicly()
    {
        var pubProd = CreateProduct(_tenantAlphaId, "PRD-VISIBLE", "Visible Product", 100m, 50m);
        var unpubProd = CreateProduct(_tenantAlphaId, "PRD-HIDDEN", "Hidden Product", 200m, 90m);

        // Published product
        await _service.CreatePublicationAsync(_tenantAlphaId, _companyAId, new CreateEcommerceProductRequest
        {
            CompanyId = _companyAId,
            ProductId = pubProd.ProductId,
            IsPublished = true
        });

        // Unpublished product
        var hiddenPub = await _service.CreatePublicationAsync(_tenantAlphaId, _companyAId, new CreateEcommerceProductRequest
        {
            CompanyId = _companyAId,
            ProductId = unpubProd.ProductId,
            IsPublished = false
        });

        var catalogue = await _service.GetPublicCatalogueAsync(_companyAId, tenantId: _tenantAlphaId);

        Assert.Single(catalogue);
        Assert.Equal(pubProd.ProductId, catalogue[0].ProductId);
        Assert.DoesNotContain(catalogue, p => p.ProductId == unpubProd.ProductId);

        // Directly querying detail for unpublished product throws KeyNotFoundException
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.GetPublicProductDetailAsync(_companyAId, unpubProd.ProductId, tenantId: _tenantAlphaId));

        // When subsequently published, it appears
        await _service.PublishAsync(_tenantAlphaId, _companyAId, hiddenPub.Id);
        var updatedCatalogue = await _service.GetPublicCatalogueAsync(_companyAId, tenantId: _tenantAlphaId);
        Assert.Equal(2, updatedCatalogue.Count);

        // When subsequently unpublished, it disappears from public view
        await _service.UnpublishAsync(_tenantAlphaId, _companyAId, hiddenPub.Id);
        var finalCatalogue = await _service.GetPublicCatalogueAsync(_companyAId, tenantId: _tenantAlphaId);
        Assert.Single(finalCatalogue);
    }

    [Fact]
    public async Task Test08_DeletingPublication_DoesNotDeleteOrModifyProductMaster()
    {
        var prod = CreateProduct(_tenantAlphaId, "PRD-KEEP", "Master Product To Preserve", 99m, 40m);

        var pub = await _service.CreatePublicationAsync(_tenantAlphaId, _companyAId, new CreateEcommerceProductRequest
        {
            CompanyId = _companyAId,
            ProductId = prod.ProductId,
            IsPublished = true
        });

        // Delete the publication record
        bool deleted = await _service.DeletePublicationAsync(_tenantAlphaId, _companyAId, pub.Id);
        Assert.True(deleted);

        // Publication is removed
        var pubInDb = await _context.EcommerceProducts.FirstOrDefaultAsync(ep => ep.Id == pub.Id);
        Assert.Null(pubInDb);

        // Product Master record remains completely intact and active
        var masterProduct = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == prod.ProductId);
        Assert.NotNull(masterProduct);
        Assert.True(masterProduct.IsActive);
        Assert.True(masterProduct.IsSellable);
        Assert.Equal("Master Product To Preserve", masterProduct.ProductName);
    }

    [Fact]
    public async Task Test09_OneCompany_CannotManageAnotherCompanyPublications()
    {
        var prod = CreateProduct(_tenantAlphaId, "PRD-ISOLATED", "Isolated Product", 55m, 25m);

        var pubA = await _service.CreatePublicationAsync(_tenantAlphaId, _companyAId, new CreateEcommerceProductRequest
        {
            CompanyId = _companyAId,
            ProductId = prod.ProductId,
            IsPublished = true
        });

        // Company B attempts to unpublish Company A's publication
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UnpublishAsync(_tenantAlphaId, _companyBId, pubA.Id));

        // Company B attempts to delete Company A's publication
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.DeletePublicationAsync(_tenantAlphaId, _companyBId, pubA.Id));

        // Company B attempts to update Company A's publication
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpdatePublicationAsync(_tenantAlphaId, _companyBId, pubA.Id, new UpdateEcommerceProductRequest
            {
                OnlineDescription = "Hacked description",
                DisplayOrder = 99
            }));

        // Verify Company A's publication was NOT modified
        var intactPub = await _service.GetPublicationByIdAsync(_tenantAlphaId, _companyAId, pubA.Id);
        Assert.True(intactPub.IsPublished);
        Assert.NotEqual("Hacked description", intactPub.OnlineDescription);
    }

    [Fact]
    public async Task Test10_TenantIsolation_IsEnforced_AcrossCompaniesAndProducts()
    {
        var prodTenantA = CreateProduct(_tenantAlphaId, "PRD-TENANT-A", "Tenant Alpha Product", 100m, 50m);
        var prodTenantB = CreateProduct(_tenantBetaId, "PRD-TENANT-B", "Tenant Beta Product", 120m, 60m);

        // Company Beta cannot publish Tenant Alpha's product
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreatePublicationAsync(_tenantBetaId, _companyBetaId, new CreateEcommerceProductRequest
            {
                CompanyId = _companyBetaId,
                ProductId = prodTenantA.ProductId,
                IsPublished = true
            }));

        Assert.Contains("not found within tenant scope", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Company A cannot publish Tenant Beta's product
        var ex2 = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreatePublicationAsync(_tenantAlphaId, _companyAId, new CreateEcommerceProductRequest
            {
                CompanyId = _companyAId,
                ProductId = prodTenantB.ProductId,
                IsPublished = true
            }));

        Assert.Contains("not found within tenant scope", ex2.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test11_PublicCatalogue_DoesNotExpose_StandardCostOrInternalFinancialData()
    {
        var prod = CreateProduct(_tenantAlphaId, "PRD-CONFIDENTIAL", "Industrial Generator",
            sellingPrice: 5000m,
            standardCost: 2800m); // Secret cost structure

        await _service.CreatePublicationAsync(_tenantAlphaId, _companyAId, new CreateEcommerceProductRequest
        {
            CompanyId = _companyAId,
            ProductId = prod.ProductId,
            OnlineDescription = "Emergency backup generator",
            IsPublished = true
        });

        var catalogue = await _service.GetPublicCatalogueAsync(_companyAId, tenantId: _tenantAlphaId);
        Assert.Single(catalogue);

        var publicItem = catalogue[0];
        Assert.Equal(5000m, publicItem.SellingPrice);

        // PublicEcommerceProductDto type does not even declare StandardCost, ValuationMethod or margin fields
        var properties = typeof(PublicEcommerceProductDto).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("StandardCost", properties);
        Assert.DoesNotContain("Cost", properties);
        Assert.DoesNotContain("ValuationMethod", properties);
        Assert.DoesNotContain("Margin", properties);
        Assert.DoesNotContain("PurchasePrice", properties);
    }

    [Fact]
    public async Task Test12_EmployeeController_CompanyScopeValidation_RejectsCrossCompanyAccess()
    {
        var userContext = new FakeCurrentUserContext
        {
            IsPlatformUser = false,
            TenantId = _tenantAlphaId,
            UserId = Guid.NewGuid()
        };

        var userAccessService = new FakeUserAccessService(allowedCompanyIds: new[] { _companyAId });

        var controller = new EcommerceProductController(
            _service,
            userContext,
            userAccessService);

        // User attempts to query publications for Company A -> Allowed (200 OK)
        var allowedResult = await controller.GetPublications(_companyAId);
        Assert.IsType<OkObjectResult>(allowedResult);

        // User attempts to query publications for Company B -> Denied (403 Forbid)
        var deniedResult = await controller.GetPublications(_companyBId);
        Assert.IsType<ForbidResult>(deniedResult);
    }

    [Fact]
    public void Test13_EcommerceProduct_DomainEntity_TransitionsCorrectly()
    {
        var entity = EcommerceProduct.Create(
            _tenantAlphaId,
            _companyAId,
            Guid.NewGuid(),
            "Initial online description",
            displayOrder: 2,
            isPublished: false);

        Assert.False(entity.IsPublished);
        Assert.Equal("Initial online description", entity.OnlineDescription);
        Assert.Equal(2, entity.DisplayOrder);
        Assert.Null(entity.UpdatedAtUtc);

        // Publish
        entity.Publish();
        Assert.True(entity.IsPublished);
        Assert.NotNull(entity.UpdatedAtUtc);

        // Update details
        entity.UpdateDetails("Updated online description", 1);
        Assert.Equal("Updated online description", entity.OnlineDescription);
        Assert.Equal(1, entity.DisplayOrder);

        // Unpublish
        entity.Unpublish();
        Assert.False(entity.IsPublished);

        // Invariant: negative display order throws ArgumentOutOfRangeException
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EcommerceProduct.Create(_tenantAlphaId, _companyAId, Guid.NewGuid(), displayOrder: -1));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            entity.UpdateDetails("Valid text", -5));
    }

    private class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; set; }
        public Guid? TenantId { get; set; }
        public bool IsPlatformUser { get; set; }
        public bool IsTenantUser => TenantId.HasValue;
        public IReadOnlyCollection<Guid> CompanyIds { get; set; } = Array.Empty<Guid>();
        public IReadOnlyCollection<Guid> BranchIds { get; set; } = Array.Empty<Guid>();
    }

    private class FakeUserAccessService : IUserAccessService
    {
        private readonly HashSet<Guid> _allowedCompanyIds;

        public FakeUserAccessService(IEnumerable<Guid> allowedCompanyIds)
        {
            _allowedCompanyIds = new HashSet<Guid>(allowedCompanyIds);
        }

        public Task<bool> CanAccessCompanyAsync(Guid userId, Guid tenantId, Guid companyId)
        {
            return Task.FromResult(_allowedCompanyIds.Contains(companyId));
        }

        public Task<bool> CanAccessBranchAsync(Guid userId, Guid tenantId, Guid companyId, Guid branchId)
        {
            return Task.FromResult(true);
        }
    }
}
