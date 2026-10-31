using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.PriceCard;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.MasterData;

[Collection("SecurityTestCollection")]
public class PriceCardTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public PriceCardTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task CreatePurchasePriceCard_WithValidData_ReturnsCreated()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRICE_CARD.VIEW", "PRICE_CARD.CREATE" });

        var (productId, uomId) = await GetTenantProductAndUomAsync(TestConstants.TenantAlphaId);

        var code = $"PC-PUR-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var name = $"Purchase Card {code}";

        var request = new CreatePriceCardRequest
        {
            PriceCardCode = code,
            PriceCardName = name,
            PriceCardType = PriceCardType.Purchase,
            Currency = "USD",
            EffectiveFrom = DateTime.UtcNow.Date,
            EffectiveTo = DateTime.UtcNow.Date.AddYears(1),
            Description = "Standard vendor price card",
            Lines = new List<CreatePriceCardLineRequest>
            {
                new()
                {
                    ProductId = productId,
                    UnitOfMeasureId = uomId,
                    UnitPrice = 45.50m,
                    MinimumQuantity = 5m
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/master-data/price-cards", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var data = await response.ReadDataJsonElementAsync();
        Assert.Equal(code, data.GetProperty("priceCardCode").GetString());
        Assert.Equal(name, data.GetProperty("priceCardName").GetString());
        Assert.Equal("USD", data.GetProperty("currency").GetString());
        Assert.True(data.GetProperty("isActive").GetBoolean());

        var lines = data.GetProperty("lines");
        Assert.Equal(1, lines.GetArrayLength());
        var firstLine = lines[0];
        Assert.Equal(productId, firstLine.GetProperty("productId").GetGuid());
        Assert.Equal(45.50m, firstLine.GetProperty("unitPrice").GetDecimal());
    }

    [Fact]
    public async Task CreateSalesPriceCard_WithValidData_ReturnsCreated()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRICE_CARD.VIEW", "PRICE_CARD.CREATE" });

        var (productId, uomId) = await GetTenantProductAndUomAsync(TestConstants.TenantAlphaId);

        var code = $"PC-SAL-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var name = $"Sales Card {code}";

        var request = new CreatePriceCardRequest
        {
            PriceCardCode = code,
            PriceCardName = name,
            PriceCardType = PriceCardType.Sales,
            Currency = "USD",
            EffectiveFrom = DateTime.UtcNow.Date,
            EffectiveTo = DateTime.UtcNow.Date.AddMonths(6),
            Description = "VIP Customer pricing",
            Lines = new List<CreatePriceCardLineRequest>
            {
                new()
                {
                    ProductId = productId,
                    UnitOfMeasureId = uomId,
                    UnitPrice = 99.99m,
                    MinimumQuantity = 1m
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/master-data/price-cards", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var data = await response.ReadDataJsonElementAsync();
        Assert.Equal(code, data.GetProperty("priceCardCode").GetString());
        Assert.Equal((int)PriceCardType.Sales, data.GetProperty("priceCardType").GetInt32());
    }

    [Fact]
    public async Task ListPriceCards_FilterByType_ReturnsOnlyMatchingCards()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRICE_CARD.VIEW", "PRICE_CARD.CREATE" });

        var purCode = $"PUR-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var salCode = $"SAL-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        await client.PostAsJsonAsync("/api/master-data/price-cards", new CreatePriceCardRequest
        {
            PriceCardCode = purCode,
            PriceCardName = $"Pur {purCode}",
            PriceCardType = PriceCardType.Purchase,
            Currency = "USD",
            EffectiveFrom = DateTime.UtcNow.Date
        });

        await client.PostAsJsonAsync("/api/master-data/price-cards", new CreatePriceCardRequest
        {
            PriceCardCode = salCode,
            PriceCardName = $"Sal {salCode}",
            PriceCardType = PriceCardType.Sales,
            Currency = "USD",
            EffectiveFrom = DateTime.UtcNow.Date
        });

        var purResponse = await client.GetAsync($"/api/master-data/price-cards?type=Purchase&search={purCode}");
        Assert.Equal(HttpStatusCode.OK, purResponse.StatusCode);
        var purData = await purResponse.ReadDataJsonElementAsync();
        Assert.True(purData.GetArrayLength() >= 1);
        Assert.All(purData.EnumerateArray(), item =>
        {
            Assert.Equal((int)PriceCardType.Purchase, item.GetProperty("priceCardType").GetInt32());
        });

        var salResponse = await client.GetAsync($"/api/master-data/price-cards?type=Sales&search={salCode}");
        Assert.Equal(HttpStatusCode.OK, salResponse.StatusCode);
        var salData = await salResponse.ReadDataJsonElementAsync();
        Assert.True(salData.GetArrayLength() >= 1);
        Assert.All(salData.EnumerateArray(), item =>
        {
            Assert.Equal((int)PriceCardType.Sales, item.GetProperty("priceCardType").GetInt32());
        });
    }

    [Fact]
    public async Task ListPriceCards_SearchAndPagination_ReturnsMatchingPagedResult()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRICE_CARD.VIEW", "PRICE_CARD.CREATE" });

        var uniquePrefix = $"SCH{Guid.NewGuid():N}"[..6].ToUpperInvariant();

        for (int i = 1; i <= 3; i++)
        {
            await client.PostAsJsonAsync("/api/master-data/price-cards", new CreatePriceCardRequest
            {
                PriceCardCode = $"{uniquePrefix}_{i}",
                PriceCardName = $"Search Card {uniquePrefix} {i}",
                PriceCardType = PriceCardType.Purchase,
                Currency = "USD",
                EffectiveFrom = DateTime.UtcNow.Date
            });
        }

        var response = await client.GetAsync($"/api/master-data/price-cards?search={uniquePrefix}&page=1&pageSize=2&sortBy=priceCardCode&sortDirection=asc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.ReadDataJsonElementAsync();
        Assert.Equal(1, json.GetProperty("page").GetInt32());
        Assert.Equal(2, json.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, json.GetProperty("totalCount").GetInt32());

        var items = json.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal($"{uniquePrefix}_1", items[0].GetProperty("priceCardCode").GetString());
        Assert.Equal($"{uniquePrefix}_2", items[1].GetProperty("priceCardCode").GetString());
    }

    [Fact]
    public async Task ListPriceCards_Sorting_AppliesServerSideSortingAndRejectsInvalidFields()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRICE_CARD.VIEW" });

        // Valid sort fields
        var validRes = await client.GetAsync("/api/master-data/price-cards?sortBy=name&sortDirection=desc&page=1&pageSize=5");
        Assert.Equal(HttpStatusCode.OK, validRes.StatusCode);

        // Invalid sort field must be rejected with 400 Bad Request
        var invalidRes = await client.GetAsync("/api/master-data/price-cards?sortBy=nonExistentField&sortDirection=asc");
        Assert.Equal(HttpStatusCode.BadRequest, invalidRes.StatusCode);
    }

    [Fact]
    public async Task GetPriceCard_ById_ReturnsHeaderAndLines()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRICE_CARD.VIEW", "PRICE_CARD.CREATE" });

        var (productId, uomId) = await GetTenantProductAndUomAsync(TestConstants.TenantAlphaId);

        var code = $"GET-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var createRes = await client.PostAsJsonAsync("/api/master-data/price-cards", new CreatePriceCardRequest
        {
            PriceCardCode = code,
            PriceCardName = $"Card {code}",
            PriceCardType = PriceCardType.Purchase,
            Currency = "USD",
            EffectiveFrom = DateTime.UtcNow.Date,
            Lines = new List<CreatePriceCardLineRequest>
            {
                new()
                {
                    ProductId = productId,
                    UnitOfMeasureId = uomId,
                    UnitPrice = 12.34m,
                    MinimumQuantity = 10m
                }
            }
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.ReadDataJsonElementAsync();
        var cardId = created.GetProperty("priceCardId").GetGuid();

        var getRes = await client.GetAsync($"/api/master-data/price-cards/{cardId}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var card = await getRes.ReadDataJsonElementAsync();
        Assert.Equal(cardId, card.GetProperty("priceCardId").GetGuid());
        Assert.Equal(code, card.GetProperty("priceCardCode").GetString());
        var lines = card.GetProperty("lines");
        Assert.Equal(1, lines.GetArrayLength());
        Assert.Equal(12.34m, lines[0].GetProperty("unitPrice").GetDecimal());
    }

    [Fact]
    public async Task UpdatePriceCard_HeaderAndLines_ReturnsUpdatedData()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRICE_CARD.VIEW", "PRICE_CARD.CREATE", "PRICE_CARD.EDIT" });

        var (productId, uomId) = await GetTenantProductAndUomAsync(TestConstants.TenantAlphaId);

        var code = $"UPD-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var createRes = await client.PostAsJsonAsync("/api/master-data/price-cards", new CreatePriceCardRequest
        {
            PriceCardCode = code,
            PriceCardName = $"Original {code}",
            PriceCardType = PriceCardType.Purchase,
            Currency = "USD",
            EffectiveFrom = DateTime.UtcNow.Date
        });
        var created = await createRes.ReadDataJsonElementAsync();
        var cardId = created.GetProperty("priceCardId").GetGuid();

        var updateReq = new UpdatePriceCardRequest
        {
            PriceCardName = $"Updated {code}",
            Currency = "USD",
            EffectiveFrom = DateTime.UtcNow.Date.AddDays(1),
            EffectiveTo = DateTime.UtcNow.Date.AddYears(2),
            Description = "Updated description",
            Lines = new List<CreatePriceCardLineRequest>
            {
                new()
                {
                    ProductId = productId,
                    UnitOfMeasureId = uomId,
                    UnitPrice = 75.00m,
                    MinimumQuantity = 2m
                }
            }
        };

        var putRes = await client.PutAsJsonAsync($"/api/master-data/price-cards/{cardId}", updateReq);
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);

        var updated = await putRes.ReadDataJsonElementAsync();
        Assert.Equal($"Updated {code}", updated.GetProperty("priceCardName").GetString());
        Assert.Equal("Updated description", updated.GetProperty("description").GetString());
        var lines = updated.GetProperty("lines");
        Assert.Equal(1, lines.GetArrayLength());
        Assert.Equal(75.00m, lines[0].GetProperty("unitPrice").GetDecimal());
    }

    [Fact]
    public async Task DeactivateAndReactivatePriceCard_PatchEndpoints_UpdateStatusCorrectly()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRICE_CARD.VIEW", "PRICE_CARD.CREATE", "PRICE_CARD.MANAGE" });

        var code = $"LC-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var createRes = await client.PostAsJsonAsync("/api/master-data/price-cards", new CreatePriceCardRequest
        {
            PriceCardCode = code,
            PriceCardName = $"Lifecycle {code}",
            PriceCardType = PriceCardType.Purchase,
            Currency = "USD",
            EffectiveFrom = DateTime.UtcNow.Date
        });
        var cardId = (await createRes.ReadDataJsonElementAsync()).GetProperty("priceCardId").GetGuid();

        // PATCH Deactivate
        var deactRes = await client.PatchAsync($"/api/master-data/price-cards/{cardId}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactRes.StatusCode);

        var getDeact = await client.GetAsync($"/api/master-data/price-cards/{cardId}");
        var deactJson = await getDeact.ReadDataJsonElementAsync();
        Assert.False(deactJson.GetProperty("isActive").GetBoolean());

        // PATCH Reactivate
        var reactRes = await client.PatchAsync($"/api/master-data/price-cards/{cardId}/reactivate", null);
        Assert.Equal(HttpStatusCode.OK, reactRes.StatusCode);

        var getReact = await client.GetAsync($"/api/master-data/price-cards/{cardId}");
        var reactJson = await getReact.ReadDataJsonElementAsync();
        Assert.True(reactJson.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task CreatePriceCard_DuplicateCode_ReturnsConflict()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRICE_CARD.VIEW", "PRICE_CARD.CREATE" });

        var code = $"DUP-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        var firstRes = await client.PostAsJsonAsync("/api/master-data/price-cards", new CreatePriceCardRequest
        {
            PriceCardCode = code,
            PriceCardName = "First Card",
            PriceCardType = PriceCardType.Purchase,
            Currency = "USD",
            EffectiveFrom = DateTime.UtcNow.Date
        });
        Assert.Equal(HttpStatusCode.Created, firstRes.StatusCode);

        // Attempt second with same code
        var dupRes = await client.PostAsJsonAsync("/api/master-data/price-cards", new CreatePriceCardRequest
        {
            PriceCardCode = code,
            PriceCardName = "Duplicate Card",
            PriceCardType = PriceCardType.Purchase,
            Currency = "USD",
            EffectiveFrom = DateTime.UtcNow.Date
        });
        Assert.Equal(HttpStatusCode.Conflict, dupRes.StatusCode);
    }

    [Fact]
    public async Task CreatePriceCard_InvalidCurrencyAndDates_ReturnsBadRequest()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRICE_CARD.VIEW", "PRICE_CARD.CREATE" });

        // Invalid Currency
        var invalidCurrRes = await client.PostAsJsonAsync("/api/master-data/price-cards", new CreatePriceCardRequest
        {
            PriceCardCode = $"CURR-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            PriceCardName = "Invalid Currency Card",
            PriceCardType = PriceCardType.Purchase,
            Currency = "NON_EXISTENT_CURRENCY_XYZ",
            EffectiveFrom = DateTime.UtcNow.Date
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidCurrRes.StatusCode);

        // Inverted Dates (EffectiveTo < EffectiveFrom)
        var invalidDatesRes = await client.PostAsJsonAsync("/api/master-data/price-cards", new CreatePriceCardRequest
        {
            PriceCardCode = $"DATE-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            PriceCardName = "Invalid Dates Card",
            PriceCardType = PriceCardType.Purchase,
            Currency = "USD",
            EffectiveFrom = DateTime.UtcNow.Date,
            EffectiveTo = DateTime.UtcNow.Date.AddDays(-5)
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidDatesRes.StatusCode);
    }

    [Fact]
    public async Task PriceCard_TenantIsolation_CrossTenantAccessReturnsNotFound()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRICE_CARD.VIEW", "PRICE_CARD.CREATE" });

        var code = $"ISO-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var createRes = await clientA.PostAsJsonAsync("/api/master-data/price-cards", new CreatePriceCardRequest
        {
            PriceCardCode = code,
            PriceCardName = $"Tenant A Card {code}",
            PriceCardType = PriceCardType.Purchase,
            Currency = "USD",
            EffectiveFrom = DateTime.UtcNow.Date
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var cardId = (await createRes.ReadDataJsonElementAsync()).GetProperty("priceCardId").GetGuid();

        // Tenant B attempts to read Tenant A's price card
        var clientB = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "PRICE_CARD.VIEW" });

        var getRes = await clientB.GetAsync($"/api/master-data/price-cards/{cardId}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }

    [Fact]
    public async Task PriceCard_UnauthorizedAccess_WithoutPermissionReturnsForbidden()
    {
        var clientNoPermission = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantUserAlphaId,
            "tenantuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant User" },
            Array.Empty<string>()); // No permissions

        var getRes = await clientNoPermission.GetAsync("/api/master-data/price-cards");
        Assert.Equal(HttpStatusCode.Forbidden, getRes.StatusCode);

        var postRes = await clientNoPermission.PostAsJsonAsync("/api/master-data/price-cards", new CreatePriceCardRequest
        {
            PriceCardCode = "FORBIDDEN",
            PriceCardName = "Forbidden Card",
            PriceCardType = PriceCardType.Purchase,
            Currency = "USD",
            EffectiveFrom = DateTime.UtcNow.Date
        });
        Assert.Equal(HttpStatusCode.Forbidden, postRes.StatusCode);
    }

    private async Task<(Guid ProductId, Guid? UomId)> GetTenantProductAndUomAsync(Guid tenantId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var product = await db.Products.FirstOrDefaultAsync(p => p.TenantId == tenantId);
        if (product == null)
        {
            throw new InvalidOperationException($"No seeded products found for tenant '{tenantId}'.");
        }

        var uom = await db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Code == "PCS");
        return (product.ProductId, uom?.UnitOfMeasureId);
    }
}
