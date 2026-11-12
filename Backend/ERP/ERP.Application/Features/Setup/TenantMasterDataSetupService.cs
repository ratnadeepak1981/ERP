using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Address;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Brand;
using Domain.Features.MasterData.Common;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.MasterData.PaymentTerm;
using Domain.Features.MasterData.PriceCard;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.TaxCode;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Setup;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Features.MasterData.Customer;
using ERP.Domain.Features.MasterData.Supplier;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Setup;

public class TenantMasterDataSetupService : ITenantMasterDataSetupService
{
    private readonly DomainDbContext _db;

    public TenantMasterDataSetupService(DomainDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<TenantMasterDataSetupResult> SetupMasterDataAsync(
        Guid tenantId,
        TenantMasterDataSetupRequest? request = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        // 1. Resolve Company and Branch
        var company = await ResolveCompanyAsync(tenantId, request?.CompanyId);
        if (company == null)
            throw new InvalidOperationException($"No active company found for tenant '{tenantId}'. Tenant organization setup must run first.");

        var branch = await ResolveBranchAsync(tenantId, company.Id, request?.BranchId);
        if (branch == null)
            throw new InvalidOperationException($"No active branch found for company '{company.Id}'. Tenant organization setup must run first.");

        // 2. Load JSON Profile
        string profileJson = await LoadProfileJsonAsync(request?.ProfileCode, request?.ProfileJson);
        using var doc = JsonDocument.Parse(profileJson);
        var root = doc.RootElement;

        var result = new TenantMasterDataSetupResult
        {
            TenantId = tenantId,
            CompanyId = company.Id,
            BranchId = branch.Id,
            Success = true
        };

        // 3. Currencies
        await SeedCurrenciesAsync(tenantId, root, result);

        // 4. Exchange Rates
        await SeedExchangeRatesAsync(tenantId, root, result);

        // 5. Payment Terms & Tax Codes
        await SeedPaymentTermsAsync(tenantId, root, result);
        await SeedTaxCodesAsync(tenantId, root, result);

        // 6. Categories & Brands
        await SeedCategoriesAsync(tenantId, root, result);
        await SeedBrandsAsync(tenantId, root, result);

        // 7. Units of Measure & Conversions
        await SeedUnitsOfMeasureAsync(tenantId, root, result);
        await SeedConversionsAsync(tenantId, root, result);

        // 8. Products, Packagings, Identifiers
        await SeedProductsAsync(tenantId, root, result);

        // 9. Warehouses, Zones, Locations
        await SeedWarehousesAsync(tenantId, company.Id, branch.Id, root, result);

        // 10. Suppliers & Customers
        await SeedSuppliersAsync(tenantId, root, result);
        await SeedCustomersAsync(tenantId, root, result);

        // 11. Price Cards & Lines
        await SeedPriceCardsAsync(tenantId, root, result);

        await _db.SaveChangesAsync();
        result.Messages.Add("Master data setup completed successfully.");
        return result;
    }

    private async Task<Company?> ResolveCompanyAsync(Guid tenantId, Guid? requestedCompanyId)
    {
        if (requestedCompanyId.HasValue && requestedCompanyId.Value != Guid.Empty)
        {
            return await _db.Companies.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == requestedCompanyId.Value);
        }
        return await _db.Companies.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.IsActive);
    }

    private async Task<Branch?> ResolveBranchAsync(Guid tenantId, Guid companyId, Guid? requestedBranchId)
    {
        if (requestedBranchId.HasValue && requestedBranchId.Value != Guid.Empty)
        {
            return await _db.Branches.FirstOrDefaultAsync(b => b.CompanyId == companyId && b.Id == requestedBranchId.Value);
        }
        return await _db.Branches.FirstOrDefaultAsync(b => b.CompanyId == companyId && b.IsActive);
    }

    private static async Task<string> LoadProfileJsonAsync(string? profileCode, string? profileJsonOverride)
    {
        if (!string.IsNullOrWhiteSpace(profileJsonOverride))
            return profileJsonOverride;

        string code = string.IsNullOrWhiteSpace(profileCode) ? "retail" : profileCode.Trim().ToLowerInvariant();

        // Look for profile JSON in executable / output directories
        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Features", "Setup", "Profiles", $"{code}.json"),
            Path.Combine(AppContext.BaseDirectory, "Profiles", $"{code}.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Features", "Setup", "Profiles", $"{code}.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Backend", "ERP", "ERP.Application", "Features", "Setup", "Profiles", $"{code}.json"),
            Path.Combine("Z:\\ERP\\Backend\\ERP\\ERP.Application\\Features\\Setup\\Profiles", $"{code}.json")
        };

        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                return await File.ReadAllTextAsync(path);
            }
        }

        throw new ArgumentException(
            $"Industry profile '{profileCode}' is not supported. Supported profiles: 'retail' (Retail Gate 6 Baseline).",
            nameof(profileCode));
    }

    private async Task SeedCurrenciesAsync(Guid tenantId, JsonElement root, TenantMasterDataSetupResult result)
    {
        if (!root.TryGetProperty("currencies", out var currenciesEl)) return;

        foreach (var c in currenciesEl.EnumerateArray())
        {
            string code = c.GetProperty("code").GetString()!.Trim().ToUpperInvariant();
            string name = c.GetProperty("name").GetString()!;
            string symbol = c.GetProperty("symbol").GetString()!;
            int decimals = c.TryGetProperty("decimalPlaces", out var d) ? d.GetInt32() : 2;

            var existing = await _db.Currencies.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CurrencyCode == code);
            if (existing == null)
            {
                var currency = Currency.Create(tenantId, code, name, symbol, decimals);
                await _db.Currencies.AddAsync(currency);
                result.CurrenciesProcessed++;
                result.Messages.Add($"Created currency '{code}' ({name}).");
            }
            else
            {
                result.Messages.Add($"Reused existing currency '{code}'.");
            }
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedExchangeRatesAsync(Guid tenantId, JsonElement root, TenantMasterDataSetupResult result)
    {
        if (!root.TryGetProperty("exchangeRates", out var ratesEl)) return;

        var currencies = await _db.Currencies.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.CurrencyCode, x => x.CurrencyId);

        foreach (var r in ratesEl.EnumerateArray())
        {
            string from = r.GetProperty("fromCurrency").GetString()!.Trim().ToUpperInvariant();
            string to = r.GetProperty("toCurrency").GetString()!.Trim().ToUpperInvariant();
            decimal rate = r.GetProperty("rate").GetDecimal();

            if (!currencies.TryGetValue(from, out var fromId) || !currencies.TryGetValue(to, out var toId))
                continue;

            var today = DateTime.UtcNow.Date;
            var existing = await _db.ExchangeRates.FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.FromCurrencyId == fromId &&
                x.ToCurrencyId == toId &&
                x.RateType == ExchangeRateType.Daily &&
                x.EffectiveDate == today);

            if (existing == null)
            {
                var exRate = ExchangeRate.Create(tenantId, fromId, toId, ExchangeRateType.Daily, today, rate);
                await _db.ExchangeRates.AddAsync(exRate);
                result.ExchangeRatesProcessed++;
                result.Messages.Add($"Created exchange rate {from}->{to} ({rate}).");
            }
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedPaymentTermsAsync(Guid tenantId, JsonElement root, TenantMasterDataSetupResult result)
    {
        if (!root.TryGetProperty("paymentTerms", out var termsEl)) return;

        foreach (var t in termsEl.EnumerateArray())
        {
            string code = t.GetProperty("code").GetString()!.Trim().ToUpperInvariant();
            string name = t.GetProperty("name").GetString()!;
            int days = t.GetProperty("days").GetInt32();
            string? desc = t.TryGetProperty("description", out var d) ? d.GetString() : null;

            var existing = await _db.PaymentTerms.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.TermCode == code);
            if (existing == null)
            {
                var term = PaymentTerm.Create(tenantId, code, name, days, desc);
                await _db.PaymentTerms.AddAsync(term);
                result.PaymentTermsProcessed++;
                result.Messages.Add($"Created payment term '{code}'.");
            }
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedTaxCodesAsync(Guid tenantId, JsonElement root, TenantMasterDataSetupResult result)
    {
        if (!root.TryGetProperty("taxCodes", out var taxEl)) return;

        foreach (var tc in taxEl.EnumerateArray())
        {
            string code = tc.GetProperty("code").GetString()!.Trim().ToUpperInvariant();
            string name = tc.GetProperty("name").GetString()!;
            decimal rate = tc.GetProperty("rate").GetDecimal();
            string? desc = tc.TryGetProperty("description", out var d) ? d.GetString() : null;

            var existing = await _db.TaxCodes.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code);
            if (existing == null)
            {
                var tax = TaxCode.Create(tenantId, code, name, rate, TaxApplicability.Both, null, null, desc);
                await _db.TaxCodes.AddAsync(tax);
                result.TaxCodesProcessed++;
                result.Messages.Add($"Created tax code '{code}' ({rate}%).");
            }
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedCategoriesAsync(Guid tenantId, JsonElement root, TenantMasterDataSetupResult result)
    {
        if (!root.TryGetProperty("categories", out var catEl)) return;

        foreach (var c in catEl.EnumerateArray())
        {
            string code = c.GetProperty("code").GetString()!.Trim().ToUpperInvariant();
            string name = c.GetProperty("name").GetString()!;
            string? desc = c.TryGetProperty("description", out var d) ? d.GetString() : null;

            var existing = await _db.Categories.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CategoryCode == code);
            if (existing == null)
            {
                var cat = Category.Create(tenantId, code, name, desc);
                await _db.Categories.AddAsync(cat);
                result.CategoriesProcessed++;
                result.Messages.Add($"Created category '{code}' ({name}).");
            }
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedBrandsAsync(Guid tenantId, JsonElement root, TenantMasterDataSetupResult result)
    {
        if (!root.TryGetProperty("brands", out var brandEl)) return;

        foreach (var b in brandEl.EnumerateArray())
        {
            string code = b.GetProperty("code").GetString()!.Trim().ToUpperInvariant();
            string name = b.GetProperty("name").GetString()!;
            string? desc = b.TryGetProperty("description", out var d) ? d.GetString() : null;

            var existing = await _db.Brands.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.BrandCode == code);
            if (existing == null)
            {
                var brand = Brand.Create(tenantId, code, name, desc);
                await _db.Brands.AddAsync(brand);
                result.BrandsProcessed++;
                result.Messages.Add($"Created brand '{code}' ({name}).");
            }
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedUnitsOfMeasureAsync(Guid tenantId, JsonElement root, TenantMasterDataSetupResult result)
    {
        if (!root.TryGetProperty("unitsOfMeasure", out var uomEl)) return;

        foreach (var u in uomEl.EnumerateArray())
        {
            string code = u.GetProperty("code").GetString()!.Trim().ToUpperInvariant();
            string name = u.GetProperty("name").GetString()!;
            string? desc = u.TryGetProperty("description", out var d) ? d.GetString() : null;

            var existing = await _db.UnitsOfMeasure.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code);
            if (existing == null)
            {
                var uom = UnitOfMeasure.Create(tenantId, code, name, desc);
                await _db.UnitsOfMeasure.AddAsync(uom);
                result.UomsProcessed++;
                result.Messages.Add($"Created UOM '{code}'.");
            }
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedConversionsAsync(Guid tenantId, JsonElement root, TenantMasterDataSetupResult result)
    {
        if (!root.TryGetProperty("conversions", out var convEl)) return;

        var uoms = await _db.UnitsOfMeasure.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.Code, x => x.UnitOfMeasureId);

        foreach (var cv in convEl.EnumerateArray())
        {
            string from = cv.GetProperty("fromUom").GetString()!.Trim().ToUpperInvariant();
            string to = cv.GetProperty("toUom").GetString()!.Trim().ToUpperInvariant();
            decimal factor = cv.GetProperty("conversionFactor").GetDecimal();

            if (!uoms.TryGetValue(from, out var fromId) || !uoms.TryGetValue(to, out var toId))
                continue;

            var existing = await _db.UnitOfMeasureConversions.FirstOrDefaultAsync(x =>
                x.TenantId == tenantId && x.FromUnitId == fromId && x.ToUnitId == toId);

            if (existing == null)
            {
                var conversion = UnitOfMeasureConversion.Create(tenantId, fromId, toId, factor);
                await _db.UnitOfMeasureConversions.AddAsync(conversion);
                result.ConversionsProcessed++;
                result.Messages.Add($"Created UOM conversion {from}->{to} ({factor}).");
            }
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedProductsAsync(Guid tenantId, JsonElement root, TenantMasterDataSetupResult result)
    {
        if (!root.TryGetProperty("products", out var prodEl)) return;

        var categories = await _db.Categories.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.CategoryCode, x => x.CategoryId);
        var brands = await _db.Brands.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.BrandCode, x => x.BrandId);
        var uoms = await _db.UnitsOfMeasure.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.Code, x => x.UnitOfMeasureId);

        foreach (var p in prodEl.EnumerateArray())
        {
            string code = p.GetProperty("code").GetString()!.Trim().ToUpperInvariant();
            string name = p.GetProperty("name").GetString()!;
            string catCode = p.GetProperty("categoryCode").GetString()!.Trim().ToUpperInvariant();
            string baseUomCode = p.GetProperty("baseUom").GetString()!.Trim().ToUpperInvariant();
            string trackingStr = p.TryGetProperty("trackingMode", out var tm) ? tm.GetString()! : "None";
            string valuationStr = p.TryGetProperty("valuationMethod", out var vm) ? vm.GetString()! : "FIFO";
            decimal purchasePrice = p.TryGetProperty("purchasePrice", out var pp) ? pp.GetDecimal() : 0m;
            decimal sellingPrice = p.TryGetProperty("sellingPrice", out var sp) ? sp.GetDecimal() : 0m;
            bool purchasable = !p.TryGetProperty("purchasable", out var pch) || pch.GetBoolean();
            bool sellable = !p.TryGetProperty("sellable", out var sll) || sll.GetBoolean();

            if (!categories.TryGetValue(catCode, out var categoryId))
                continue;

            Guid? uomId = uoms.TryGetValue(baseUomCode, out var uid) ? uid : null;
            Guid? brandId = null;
            if (p.TryGetProperty("brandCode", out var bc) && bc.GetString() is { } bCode && brands.TryGetValue(bCode.ToUpperInvariant(), out var bid))
            {
                brandId = bid;
            }

            var tracking = Enum.TryParse<TrackingMode>(trackingStr, true, out var trk) ? trk : TrackingMode.None;
            var valuation = Enum.TryParse<ValuationMethod>(valuationStr, true, out var val) ? val : ValuationMethod.FIFO;

            var existingProduct = await _db.Products.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ProductCode == code);
            Product product;

            if (existingProduct == null)
            {
                product = Product.Create(
                    tenantId,
                    code,
                    name,
                    categoryId,
                    unitOfMeasure: baseUomCode,
                    description: $"{name} ({code})",
                    valuationMethod: valuation,
                    standardCost: purchasePrice,
                    sellingPrice: sellingPrice,
                    reorderLevel: 20,
                    reorderQuantity: 50,
                    isPurchasable: purchasable,
                    isSellable: sellable,
                    isStockTracked: true,
                    productType: ProductType.StockItem,
                    trackingMode: tracking,
                    unitOfMeasureId: uomId,
                    brandId: brandId);

                await _db.Products.AddAsync(product);
                await _db.SaveChangesAsync();
                result.ProductsProcessed++;
                result.Messages.Add($"Created product '{code}' ({name}).");
            }
            else
            {
                product = existingProduct;
                result.Messages.Add($"Reused product '{code}'.");
            }

            // Packagings
            if (p.TryGetProperty("packagings", out var packagingsEl))
            {
                foreach (var pkg in packagingsEl.EnumerateArray())
                {
                    string pkgCode = pkg.GetProperty("packagingCode").GetString()!.Trim().ToUpperInvariant();
                    string pkgUomCode = pkg.GetProperty("packagingUom").GetString()!.Trim().ToUpperInvariant();
                    decimal multiplier = pkg.GetProperty("multiplier").GetDecimal();

                    if (!uoms.TryGetValue(pkgUomCode, out var pkgUomId))
                        continue;

                    var existingPkg = await _db.ProductPackagings.FirstOrDefaultAsync(x =>
                        x.TenantId == tenantId && x.ProductId == product.ProductId && x.PackagingCode == pkgCode);

                    if (existingPkg == null)
                    {
                        var packaging = ProductPackaging.Create(
                            tenantId,
                            product.ProductId,
                            pkgCode,
                            $"{product.ProductName} {pkgUomCode}",
                            pkgUomId,
                            multiplier);

                        await _db.ProductPackagings.AddAsync(packaging);
                        result.PackagingsProcessed++;
                    }
                }
            }

            // Identifiers / Barcodes
            if (p.TryGetProperty("identifiers", out var identifiersEl))
            {
                foreach (var idEl in identifiersEl.EnumerateArray())
                {
                    string barcode = idEl.GetProperty("barcode").GetString()!.Trim();
                    string symbologyStr = idEl.TryGetProperty("symbology", out var sym) ? sym.GetString()! : "EAN13";
                    bool isPrimary = idEl.TryGetProperty("isPrimary", out var ip) && ip.GetBoolean();

                    var symbology = Enum.TryParse<IdentifierSymbology>(symbologyStr, true, out var s) ? s : IdentifierSymbology.EAN13;

                    var existingId = await _db.ProductIdentifiers.FirstOrDefaultAsync(x =>
                        x.TenantId == tenantId && x.ProductId == product.ProductId && x.IdentifierValue == barcode);

                    if (existingId == null)
                    {
                        var identifier = ProductIdentifier.Create(
                            tenantId,
                            product.ProductId,
                            barcode,
                            IdentifierType.LinearBarcode,
                            symbology,
                            IdentifierSource.Internal,
                            uomId,
                            isPrimary);

                        await _db.ProductIdentifiers.AddAsync(identifier);
                        result.IdentifiersProcessed++;
                    }
                }
            }
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedWarehousesAsync(Guid tenantId, Guid companyId, Guid branchId, JsonElement root, TenantMasterDataSetupResult result)
    {
        if (!root.TryGetProperty("warehouses", out var whEl)) return;

        var locTypes = await _db.LocationTypes.ToDictionaryAsync(x => x.Code, x => x.LocationTypeId);

        foreach (var w in whEl.EnumerateArray())
        {
            string code = w.GetProperty("code").GetString()!.Trim().ToUpperInvariant();
            string name = w.GetProperty("name").GetString()!;
            string? address = w.TryGetProperty("addressLine1", out var a) ? a.GetString() : null;
            string? city = w.TryGetProperty("city", out var c) ? c.GetString() : "Colombo";
            string? country = w.TryGetProperty("country", out var cnt) ? cnt.GetString() : "Sri Lanka";

            var existingWh = await _db.Warehouses.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.WarehouseCode == code);
            Warehouse warehouse;

            if (existingWh == null)
            {
                warehouse = Warehouse.Create(
                    tenantId,
                    code,
                    name,
                    $"{name} ({code})",
                    companyId: companyId,
                    branchId: branchId,
                    addressLine1: address,
                    city: city,
                    country: country);

                await _db.Warehouses.AddAsync(warehouse);
                await _db.SaveChangesAsync();
                result.WarehousesProcessed++;
                result.Messages.Add($"Created warehouse '{code}' ({name}).");
            }
            else
            {
                warehouse = existingWh;
                result.Messages.Add($"Reused warehouse '{code}'.");
            }

            // Zones
            if (w.TryGetProperty("zones", out var zonesEl))
            {
                foreach (var z in zonesEl.EnumerateArray())
                {
                    string zCode = z.GetProperty("code").GetString()!.Trim().ToUpperInvariant();
                    string zName = z.GetProperty("name").GetString()!;
                    string? zDesc = z.TryGetProperty("description", out var zd) ? zd.GetString() : null;

                    var existingZone = await _db.WarehouseZones.FirstOrDefaultAsync(x =>
                        x.TenantId == tenantId && x.WarehouseId == warehouse.WarehouseId && x.ZoneCode == zCode);

                    WarehouseZone zone;
                    if (existingZone == null)
                    {
                        zone = WarehouseZone.Create(tenantId, warehouse.WarehouseId, zCode, zName, zDesc);
                        await _db.WarehouseZones.AddAsync(zone);
                        await _db.SaveChangesAsync();
                        result.ZonesProcessed++;
                        result.Messages.Add($"Created zone '{zCode}' in warehouse '{code}'.");
                    }
                    else
                    {
                        zone = existingZone;
                    }

                    // Locations
                    if (z.TryGetProperty("locations", out var locsEl))
                    {
                        foreach (var l in locsEl.EnumerateArray())
                        {
                            string lCode = l.GetProperty("code").GetString()!.Trim().ToUpperInvariant();
                            string lName = l.GetProperty("name").GetString()!;
                            string locTypeCode = l.TryGetProperty("locationTypeCode", out var ltc) ? ltc.GetString()!.Trim().ToUpperInvariant() : "RACK";

                            Guid? typeId = locTypes.TryGetValue(locTypeCode, out var tid) ? tid : null;

                            var existingLoc = await _db.WarehouseLocations.FirstOrDefaultAsync(x =>
                                x.TenantId == tenantId && x.WarehouseId == warehouse.WarehouseId && x.LocationCode == lCode);

                            if (existingLoc == null)
                            {
                                var location = WarehouseLocation.Create(
                                    tenantId,
                                    warehouse.WarehouseId,
                                    zone.WarehouseZoneId,
                                    lCode,
                                    lName,
                                    locationTypeId: typeId);

                                await _db.WarehouseLocations.AddAsync(location);
                                result.LocationsProcessed++;
                                result.Messages.Add($"Created location '{lCode}' in zone '{zCode}'.");
                            }
                        }
                    }
                }
            }
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedSuppliersAsync(Guid tenantId, JsonElement root, TenantMasterDataSetupResult result)
    {
        if (!root.TryGetProperty("suppliers", out var supEl)) return;

        var currencies = await _db.Currencies.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.CurrencyCode, x => x.CurrencyId);

        foreach (var s in supEl.EnumerateArray())
        {
            string code = s.GetProperty("code").GetString()!.Trim().ToUpperInvariant();
            string name = s.GetProperty("name").GetString()!;
            string? email = s.TryGetProperty("email", out var e) ? e.GetString() : null;
            string? phone = s.TryGetProperty("phone", out var ph) ? ph.GetString() : null;
            string? addr1 = s.TryGetProperty("addressLine1", out var a) ? a.GetString() : null;
            string? city = s.TryGetProperty("city", out var ct) ? ct.GetString() : null;
            string? postal = s.TryGetProperty("postalCode", out var p) ? p.GetString() : null;
            string? country = s.TryGetProperty("country", out var cnt) ? cnt.GetString() : "Sri Lanka";
            string? terms = s.TryGetProperty("paymentTerms", out var pt) ? pt.GetString() : "NET30";
            string? contact = s.TryGetProperty("contactPerson", out var cp) ? cp.GetString() : null;
            string? currCode = s.TryGetProperty("currencyCode", out var cCode) ? cCode.GetString()?.ToUpperInvariant() : "LKR";

            Guid? currId = (currCode != null && currencies.TryGetValue(currCode, out var cid)) ? cid : null;

            var existing = await _db.Suppliers.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SupplierCode == code);
            if (existing == null)
            {
                var supplier = Supplier.Create(
                    tenantId,
                    code,
                    name,
                    contactPerson: contact,
                    email: email,
                    phone: phone,
                    addressLine1: addr1,
                    city: city,
                    postalCode: postal,
                    country: country,
                    paymentTerms: terms,
                    currencyId: currId);

                await _db.Suppliers.AddAsync(supplier);
                result.SuppliersProcessed++;
                result.Messages.Add($"Created supplier '{code}' ({name}).");
            }
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedCustomersAsync(Guid tenantId, JsonElement root, TenantMasterDataSetupResult result)
    {
        if (!root.TryGetProperty("customers", out var custEl)) return;

        var currencies = await _db.Currencies.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.CurrencyCode, x => x.CurrencyId);

        foreach (var c in custEl.EnumerateArray())
        {
            string code = c.GetProperty("code").GetString()!.Trim().ToUpperInvariant();
            string name = c.GetProperty("name").GetString()!;
            string? email = c.TryGetProperty("email", out var e) ? e.GetString() : null;
            string? phone = c.TryGetProperty("phone", out var ph) ? ph.GetString() : null;
            string? addr1 = c.TryGetProperty("addressLine1", out var a) ? a.GetString() : null;
            string? city = c.TryGetProperty("city", out var ct) ? ct.GetString() : "Colombo";
            string? postal = c.TryGetProperty("postalCode", out var p) ? p.GetString() : null;
            string? country = c.TryGetProperty("country", out var cnt) ? cnt.GetString() : "Sri Lanka";
            string? terms = c.TryGetProperty("paymentTerms", out var pt) ? pt.GetString() : "NET30";
            string? contact = c.TryGetProperty("contactPerson", out var cp) ? cp.GetString() : null;
            string? currCode = c.TryGetProperty("currencyCode", out var cCode) ? cCode.GetString()?.ToUpperInvariant() : "LKR";

            Guid? currId = (currCode != null && currencies.TryGetValue(currCode, out var cid)) ? cid : null;

            var existing = await _db.Customers.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CustomerCode == code);
            if (existing == null)
            {
                var customer = Customer.Create(
                    tenantId,
                    code,
                    name,
                    contactPerson: contact,
                    email: email,
                    phone: phone,
                    addressLine1: addr1,
                    city: city,
                    postalCode: postal,
                    country: country,
                    paymentTerms: terms,
                    creditLimit: 500000m,
                    currencyId: currId);

                await _db.Customers.AddAsync(customer);
                result.CustomersProcessed++;
                result.Messages.Add($"Created customer '{code}' ({name}).");
            }
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedPriceCardsAsync(Guid tenantId, JsonElement root, TenantMasterDataSetupResult result)
    {
        if (!root.TryGetProperty("priceCards", out var pcEl)) return;

        var products = await _db.Products.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.ProductCode, x => x.ProductId);
        var uoms = await _db.UnitsOfMeasure.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.Code, x => x.UnitOfMeasureId);

        foreach (var pc in pcEl.EnumerateArray())
        {
            string code = pc.GetProperty("code").GetString()!.Trim().ToUpperInvariant();
            string name = pc.GetProperty("name").GetString()!;
            string typeStr = pc.GetProperty("priceCardType").GetString()!;
            string currency = pc.GetProperty("currency").GetString()!.Trim().ToUpperInvariant();
            string? desc = pc.TryGetProperty("description", out var d) ? d.GetString() : null;

            var pcType = Enum.TryParse<PriceCardType>(typeStr, true, out var t) ? t : PriceCardType.Sales;
            var today = DateTime.UtcNow.Date;

            var existing = await _db.PriceCards.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PriceCardCode == code);
            PriceCard priceCard;

            if (existing == null)
            {
                priceCard = PriceCard.Create(
                    tenantId,
                    code,
                    name,
                    pcType,
                    currency,
                    today,
                    today.AddYears(5),
                    desc);

                await _db.PriceCards.AddAsync(priceCard);
                await _db.SaveChangesAsync();
                result.PriceCardsProcessed++;
                result.Messages.Add($"Created price card '{code}' ({pcType}).");
            }
            else
            {
                priceCard = existing;
                result.Messages.Add($"Reused price card '{code}'.");
            }

            // Lines
            if (pc.TryGetProperty("lines", out var linesEl))
            {
                foreach (var line in linesEl.EnumerateArray())
                {
                    string pCode = line.GetProperty("productCode").GetString()!.Trim().ToUpperInvariant();
                    string uCode = line.GetProperty("uomCode").GetString()!.Trim().ToUpperInvariant();
                    decimal price = line.GetProperty("unitPrice").GetDecimal();

                    if (!products.TryGetValue(pCode, out var prodId))
                        continue;

                    Guid? uId = uoms.TryGetValue(uCode, out var uomVal) ? uomVal : null;

                    var existingLine = await _db.PriceCardLines.FirstOrDefaultAsync(x =>
                        x.TenantId == tenantId && x.PriceCardId == priceCard.PriceCardId && x.ProductId == prodId);

                    if (existingLine == null)
                    {
                        var pcLine = PriceCardLine.Create(
                            tenantId,
                            priceCard.PriceCardId,
                            prodId,
                            price,
                            uId,
                            minimumQuantity: 1m,
                            effectiveFrom: today);

                        await _db.PriceCardLines.AddAsync(pcLine);
                        result.Messages.Add($"Added line to price card '{code}': {pCode} @ {price:N2}.");
                    }
                }
            }
        }
        await _db.SaveChangesAsync();
    }
}
