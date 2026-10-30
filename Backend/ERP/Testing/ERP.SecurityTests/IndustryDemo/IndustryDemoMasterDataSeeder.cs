using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Common;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory.Batch;
using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Features.MasterData.Warehouse;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;
using Security.Core.Models;
using Security.Infrastructure.Persistence;
using Security.Services;

namespace ERP.SecurityTests.IndustryDemo;

public static class IndustryDemoMasterDataSeeder
{
    public record TenantSpec(
        string Prefix,
        string Name,
        string Industry,
        string BaseCurrencyCode,
        string AdminEmail,
        string[] ExtraUserSuffixes,
        (string Code, string Name, string Desc)[] Categories,
        ProductSpec[] Products);

    public record ProductSpec(
        string Code,
        string Name,
        ProductType Type,
        string BaseUom,
        TrackingMode Tracking,
        ValuationMethod Valuation,
        bool Purchasable,
        bool Sellable,
        bool Manufacturable,
        bool CanConsumeInProduction,
        bool CanConsumeInMaintenance,
        string CategoryCode,
        IdentifierSymbology? Symbology,
        (string PkgCode, string PkgName, string PkgUom, decimal Multiplier)[] Packagings);

    public static readonly Dictionary<string, Guid> TenantIds = new()
    {
        ["retail"] = Guid.Parse("10000000-0000-0000-0000-000000000001"),
        ["wholesale"] = Guid.Parse("10000000-0000-0000-0000-000000000002"),
        ["impexp"] = Guid.Parse("10000000-0000-0000-0000-000000000003"),
        ["hardware"] = Guid.Parse("10000000-0000-0000-0000-000000000004"),
        ["industrial"] = Guid.Parse("10000000-0000-0000-0000-000000000005"),
        ["traders"] = Guid.Parse("10000000-0000-0000-0000-000000000006"),
        ["services"] = Guid.Parse("10000000-0000-0000-0000-000000000007"),
        ["manufacturing"] = Guid.Parse("10000000-0000-0000-0000-000000000008"),
        ["food"] = Guid.Parse("10000000-0000-0000-0000-000000000009"),
        ["solutions"] = Guid.Parse("10000000-0000-0000-0000-000000000010"),
        ["pharma"] = Guid.Parse("10000000-0000-0000-0000-000000000011")
    };

    public static void SeedAll(
        SaaSDbContext platformDb,
        SecurityDbContext securityDb,
        DomainDbContext domainDb)
    {
        var specs = GetTenantSpecs();

        // 1. Seed SaaS Tenants, Subscriptions, TenantConfigurations
        SeedPlatformTenants(platformDb, specs);

        // 2. Seed Master Data (Currencies, UOMs, Categories, Companies, Branches, Warehouses, Products, Packagings, Identifiers)
        SeedDomainData(domainDb, specs);

        // 3. Seed Security Roles, Users (hashed pw: 123), Role Assignments, Scopes
        SeedSecurityUsers(securityDb, domainDb, specs);
    }

    private static void SeedPlatformTenants(SaaSDbContext db, List<TenantSpec> specs)
    {
        var freePlanId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var now = DateTime.UtcNow;

        foreach (var spec in specs)
        {
            var tenantId = TenantIds[spec.Prefix];

            if (!db.Tenants.Any(x => x.Id == tenantId))
            {
                db.Tenants.Add(new Tenant
                {
                    Id = tenantId,
                    Name = spec.Name,
                    Code = spec.Prefix.ToUpperInvariant(),
                    IsActive = true
                });
            }

            if (!db.Subscriptions.Any(x => x.TenantId == tenantId))
            {
                db.Subscriptions.Add(new Subscription
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SubscriptionPlanId = freePlanId,
                    SubscriptionName = $"{spec.Name} Demo Subscription",
                    SubscriptionType = "FREE",
                    Status = SubscriptionStatus.Active,
                    StartDate = now.AddMonths(-1),
                    CurrentPeriodStart = now.AddMonths(-1),
                    CurrentPeriodEnd = now.AddYears(1),
                    BillingAnchorDate = now.AddMonths(-1),
                    IsActive = true
                });
            }

            var config = db.TenantConfigurations.FirstOrDefault(x => x.TenantId == tenantId);
            if (config == null)
            {
                config = TenantConfiguration.CreateDefault(tenantId);
                config.UpdateSpecialUomConversion(true);
                db.TenantConfigurations.Add(config);
            }
            else
            {
                config.UpdateSpecialUomConversion(true);
            }
        }

        db.SaveChanges();
    }

    private static void SeedDomainData(DomainDbContext db, List<TenantSpec> specs)
    {
        var standardCurrencies = new (string Code, string Name, string Symbol, int Decimals)[]
        {
            ("LKR", "Sri Lankan Rupee", "Rs", 2),
            ("USD", "US Dollar", "$", 2),
            ("EUR", "Euro", "€", 2),
            ("GBP", "British Pound", "£", 2),
            ("INR", "Indian Rupee", "₹", 2)
        };

        var all33Uoms = new (string Code, string Name, string Description)[]
        {
            ("PCS", "Pieces", "Individual count unit"),
            ("EA", "Each", "Each individual item"),
            ("BOX", "Box", "Packaging box"),
            ("PACK", "Pack", "Standard bundle or pack"),
            ("CARTON", "Carton", "Shipping carton"),
            ("BOTTLE", "Bottle", "Bottled container"),
            ("BTL", "Bottle (Short)", "Bottle container alias"),
            ("CAN", "Can", "Canister or metal can"),
            ("KG", "Kilogram", "Metric mass unit"),
            ("G", "Gram", "Metric mass sub-unit"),
            ("MTR", "Meter", "Metric linear length unit"),
            ("CM", "Centimeter", "Metric linear length sub-unit"),
            ("MM", "Millimeter", "Metric linear precision sub-unit"),
            ("LTR", "Liter", "Metric volume unit"),
            ("ML", "Milliliter", "Metric volume sub-unit"),
            ("HRS", "Hours", "Service duration unit"),
            ("DAY", "Day", "Daily service or rental unit"),
            ("JOB", "Job", "Discrete project or task unit"),
            ("PAIR", "Pair", "Two matched units"),
            ("ROLL", "Roll", "Coiled material unit"),
            ("BAG", "Bag", "Bag or sack packaging"),
            ("SET", "Set", "Multi-piece assembly or set"),
            ("DOZEN", "Dozen", "Twelve individual pieces"),
            ("PALLET", "Pallet", "Standard shipping pallet"),
            ("TABLET", "Tablet", "Pharmaceutical tablet dosage unit"),
            ("CAPSULE", "Capsule", "Pharmaceutical capsule dosage unit"),
            ("BALE", "Bale", "Compressed agricultural or textile bale"),
            ("CASE", "Case", "Protective shipping case"),
            ("SHEET", "Sheet", "Flat manufactured sheet"),
            ("TUB", "Tub", "Rigid tub container"),
            ("DRUM", "Drum", "Industrial cylindrical drum"),
            ("TIN", "Tin", "Rigid metal tin container"),
            ("VIAL", "Vial", "Small sterile pharmaceutical vial")
        };

        var conversionRules = new (string FromCode, string ToCode, decimal Factor)[]
        {
            ("BOX", "PCS", 10m),
            ("PACK", "PCS", 6m),
            ("CARTON", "BOX", 12m),
            ("KG", "G", 1000m),
            ("MTR", "CM", 100m),
            ("CM", "MM", 10m),
            ("LTR", "ML", 1000m),
            ("PAIR", "PCS", 2m),
            ("DOZEN", "PCS", 12m)
        };

        foreach (var spec in specs)
        {
            var tenantId = TenantIds[spec.Prefix];

            // 1. Currencies
            foreach (var c in standardCurrencies)
            {
                if (!db.Currencies.Any(x => x.TenantId == tenantId && x.CurrencyCode == c.Code))
                {
                    db.Currencies.Add(Currency.Create(tenantId, c.Code, c.Name, c.Symbol, c.Decimals));
                }
            }
            db.SaveChanges();

            var baseCurrency = db.Currencies.First(x => x.TenantId == tenantId && x.CurrencyCode == spec.BaseCurrencyCode);

            // 2. Company with assigned BaseCurrencyId
            var company = db.Companies.FirstOrDefault(x => x.TenantId == tenantId);
            if (company == null)
            {
                company = new Company
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = spec.Name,
                    Code = spec.Prefix.ToUpperInvariant(),
                    IsActive = true,
                    SpecialUomConversionEnabled = true,
                    BaseCurrencyId = baseCurrency.CurrencyId
                };
                db.Companies.Add(company);
                db.SaveChanges();
            }
            else
            {
                company.BaseCurrencyId = baseCurrency.CurrencyId;
                company.SpecialUomConversionEnabled = true;
                db.SaveChanges();
            }

            // 3. Branch
            var branch = db.Branches.FirstOrDefault(x => x.CompanyId == company.Id);
            if (branch == null)
            {
                branch = new Branch
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    Name = $"{spec.Name} Main Branch",
                    Code = "MAIN",
                    IsActive = true
                };
                db.Branches.Add(branch);
                db.SaveChanges();
            }

            // 4. Warehouse & Locations
            var warehouse = db.Warehouses.FirstOrDefault(x => x.TenantId == tenantId && x.CompanyId == company.Id);
            if (warehouse == null)
            {
                warehouse = Warehouse.Create(
                    tenantId,
                    $"{spec.Prefix.ToUpperInvariant()}-WH1",
                    $"{spec.Name} Central Warehouse",
                    companyId: company.Id,
                    branchId: branch.Id,
                    city: "Colombo",
                    country: "Sri Lanka");
                db.Warehouses.Add(warehouse);
                db.SaveChanges();

                var recLoc = WarehouseLocation.Create(
                    tenantId,
                    warehouse.WarehouseId,
                    null,
                    "LOC-REC-01",
                    "Receiving Dock");
                var stkLoc = WarehouseLocation.Create(
                    tenantId,
                    warehouse.WarehouseId,
                    null,
                    "LOC-STK-01",
                    "General Storage Bin");

                db.WarehouseLocations.Add(recLoc);
                db.WarehouseLocations.Add(stkLoc);
                db.SaveChanges();
            }

            // 5. UOMs
            foreach (var u in all33Uoms)
            {
                if (!db.UnitsOfMeasure.Any(x => x.TenantId == tenantId && x.Code == u.Code))
                {
                    db.UnitsOfMeasure.Add(UnitOfMeasure.Create(tenantId, u.Code, u.Name, u.Description));
                }
            }
            db.SaveChanges();

            // 6. UOM Conversions
            var uomLookup = db.UnitsOfMeasure.Where(x => x.TenantId == tenantId).ToDictionary(x => x.Code, x => x.UnitOfMeasureId);
            foreach (var cr in conversionRules)
            {
                if (uomLookup.TryGetValue(cr.FromCode, out var fromId) && uomLookup.TryGetValue(cr.ToCode, out var toId))
                {
                    if (!db.UnitOfMeasureConversions.Any(x => x.TenantId == tenantId && x.FromUnitId == fromId && x.ToUnitId == toId))
                    {
                        db.UnitOfMeasureConversions.Add(UnitOfMeasureConversion.Create(tenantId, fromId, toId, cr.Factor));
                    }
                }
            }
            db.SaveChanges();

            // 7. Categories
            foreach (var cat in spec.Categories)
            {
                if (!db.Categories.Any(x => x.TenantId == tenantId && x.CategoryCode == cat.Code))
                {
                    db.Categories.Add(Category.Create(tenantId, cat.Code, cat.Name, cat.Desc));
                }
            }
            db.SaveChanges();

            var catLookup = db.Categories.Where(x => x.TenantId == tenantId).ToDictionary(x => x.CategoryCode, x => x.CategoryId);

            // 8. Products
            int prodIndex = 1;
            foreach (var p in spec.Products)
            {
                if (!catLookup.TryGetValue(p.CategoryCode, out var catId))
                {
                    catId = catLookup.Values.First();
                }

                if (!uomLookup.TryGetValue(p.BaseUom, out var uomId))
                {
                    uomId = uomLookup["PCS"];
                }

                var product = db.Products.FirstOrDefault(x => x.TenantId == tenantId && x.ProductCode == p.Code);
                if (product == null)
                {
                    product = Product.Create(
                        tenantId,
                        p.Code,
                        p.Name,
                        catId,
                        unitOfMeasure: p.BaseUom,
                        standardCost: 10m,
                        sellingPrice: 15m,
                        reorderLevel: 5m,
                        reorderQuantity: 20m,
                        isManufacturable: p.Manufacturable,
                        isPurchasable: p.Purchasable,
                        isSellable: p.Sellable,
                        canConsumeInProduction: p.CanConsumeInProduction,
                        isStockTracked: p.Type == ProductType.StockItem,
                        productType: p.Type,
                        trackingMode: p.Tracking,
                        unitOfMeasureId: uomId,
                        valuationMethod: p.Valuation,
                        canConsumeInMaintenance: p.CanConsumeInMaintenance);

                    db.Products.Add(product);
                    db.SaveChanges();
                }

                // 9. Product Packaging
                foreach (var pkg in p.Packagings)
                {
                    if (uomLookup.TryGetValue(pkg.PkgUom, out var pkgUomId))
                    {
                        string uniquePkgCode = $"{p.Code}-{pkg.PkgCode}";
                        if (!db.ProductPackagings.Any(x => x.TenantId == tenantId && x.PackagingCode == uniquePkgCode))
                        {
                            var packaging = ProductPackaging.Create(
                                tenantId,
                                product.ProductId,
                                uniquePkgCode,
                                pkg.PkgName,
                                pkgUomId,
                                pkg.Multiplier,
                                purchaseAllowed: true,
                                salesAllowed: true,
                                companyId: company.Id);

                            db.ProductPackagings.Add(packaging);
                        }
                    }
                }
                db.SaveChanges();

                // 10. Barcode Identifiers
                if (p.Symbology.HasValue)
                {
                    if (!db.ProductIdentifiers.Any(x => x.TenantId == tenantId && x.ProductId == product.ProductId))
                    {
                        string barcodeVal = GenerateBarcode(prodIndex, p.Symbology.Value);
                        var idType = p.Symbology.Value switch
                        {
                            IdentifierSymbology.QRCode => IdentifierType.QRCode,
                            IdentifierSymbology.DataMatrix => IdentifierType.DataMatrix,
                            _ => IdentifierType.LinearBarcode
                        };

                        var identifier = ProductIdentifier.Create(
                            tenantId,
                            product.ProductId,
                            barcodeVal,
                            idType,
                            p.Symbology.Value,
                            IdentifierSource.Internal,
                            uomId,
                            isPrimary: true);

                        db.ProductIdentifiers.Add(identifier);
                        db.SaveChanges();
                    }
                }

                // 11. Product Batch for Batch tracking products
                if (p.Tracking == TrackingMode.Batch)
                {
                    string batchNum = $"BATCH-{p.Code}-001";
                    if (!db.ProductBatches.Any(x => x.TenantId == tenantId && x.ProductId == product.ProductId && x.BatchNumber == batchNum))
                    {
                        var batch = ProductBatch.Create(
                            tenantId,
                            product.ProductId,
                            batchNum,
                            manufacturingDate: DateTime.UtcNow.AddMonths(-2),
                            expiryDate: DateTime.UtcNow.AddYears(1));

                        db.ProductBatches.Add(batch);
                        db.SaveChanges();
                    }
                }

                prodIndex++;
            }
        }
    }

    private static void SeedSecurityUsers(SecurityDbContext db, DomainDbContext domainDb, List<TenantSpec> specs)
    {
        var passwordHash = new PasswordService().HashPassword("123");

        // Ensure Standard Roles
        var tenantAdminRole = GetOrCreateRole(db, "Tenant Admin", "Tenant Administrator");
        var purchasingOfficerRole = GetOrCreateRole(db, "Purchasing Officer", "Purchasing Officer with PO Submit authority");
        var purchasingManagerRole = GetOrCreateRole(db, "Purchasing Manager", "Purchasing Manager with PO Approval authority");
        var warehouseOperatorRole = GetOrCreateRole(db, "Warehouse Operator", "Warehouse & Inventory Operator");
        var salesOfficerRole = GetOrCreateRole(db, "Sales Officer", "Sales Order and Dispatch Officer");
        var viewerRole = GetOrCreateRole(db, "Viewer", "Read-Only Organization Viewer");

        foreach (var spec in specs)
        {
            var tenantId = TenantIds[spec.Prefix];
            var company = domainDb.Companies.First(x => x.TenantId == tenantId);
            var branch = domainDb.Branches.First(x => x.CompanyId == company.Id);

            // Core 6 accounts per tenant
            CreateUser(db, $"{spec.Prefix}.admin", $"{spec.Prefix}_admin@example.test", passwordHash, tenantId, tenantAdminRole, company.Id, branch.Id);
            CreateUser(db, $"{spec.Prefix}.purchase", $"{spec.Prefix}_purchase@example.test", passwordHash, tenantId, purchasingOfficerRole, company.Id, branch.Id);
            CreateUser(db, $"{spec.Prefix}.purchase.manager", $"{spec.Prefix}_purchase_manager@example.test", passwordHash, tenantId, purchasingManagerRole, company.Id, branch.Id);
            CreateUser(db, $"{spec.Prefix}.warehouse", $"{spec.Prefix}_warehouse@example.test", passwordHash, tenantId, warehouseOperatorRole, company.Id, branch.Id);
            CreateUser(db, $"{spec.Prefix}.sales", $"{spec.Prefix}_sales@example.test", passwordHash, tenantId, salesOfficerRole, company.Id, branch.Id);
            CreateUser(db, $"{spec.Prefix}.viewer", $"{spec.Prefix}_viewer@example.test", passwordHash, tenantId, viewerRole, company.Id, branch.Id);

            // Extra accounts per tenant spec
            foreach (var suffix in spec.ExtraUserSuffixes)
            {
                var role = suffix switch
                {
                    "customs" or "planner" => purchasingOfficerRole,
                    "logistics" or "technician" or "engineer" or "qc" or "pharmacist" => warehouseOperatorRole,
                    "counter" or "cashier" or "consultant" => salesOfficerRole,
                    _ => viewerRole
                };

                CreateUser(db, $"{spec.Prefix}.{suffix}", $"{spec.Prefix}_{suffix}@example.test", passwordHash, tenantId, role, company.Id, branch.Id);
            }
        }
    }

    private static Role GetOrCreateRole(SecurityDbContext db, string name, string desc)
    {
        var role = db.Roles.FirstOrDefault(x => x.Name == name);
        if (role == null)
        {
            role = new Role
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = desc,
                IsActive = true
            };
            db.Roles.Add(role);
            db.SaveChanges();
        }
        return role;
    }

    private static void CreateUser(
        SecurityDbContext db,
        string username,
        string email,
        string passwordHash,
        Guid tenantId,
        Role role,
        Guid companyId,
        Guid branchId)
    {
        var user = db.Users.FirstOrDefault(x => x.TenantId == tenantId && x.Username == username);
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Username = username,
                Email = email,
                PasswordHash = passwordHash,
                IsActive = true
            };
            db.Users.Add(user);
            db.SaveChanges();
        }

        var userRole = db.UserRoles.FirstOrDefault(x => x.UserId == user.Id && x.RoleId == role.Id);
        if (userRole == null)
        {
            userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = role.Id,
                Role = role
            };
            db.UserRoles.Add(userRole);
            db.SaveChanges();
        }

        bool scopeExists = db.UserRoleScopes.Any(x =>
            x.UserRoleId == userRole.Id &&
            x.CompanyId == companyId &&
            x.BranchId == branchId);

        if (!scopeExists)
        {
            db.UserRoleScopes.Add(new UserRoleScope
            {
                Id = Guid.NewGuid(),
                UserRoleId = userRole.Id,
                CompanyId = companyId,
                BranchId = branchId,
                IsActive = true
            });
            db.SaveChanges();
        }
    }

    public static string GenerateBarcode(int seed, IdentifierSymbology symbology)
    {
        switch (symbology)
        {
            case IdentifierSymbology.EAN13:
                string eanBase = $"479000{seed:D6}";
                int eanCheck = SymbologyValidator.CalculateModulo10CheckDigit(eanBase, isEan13: true);
                return $"{eanBase}{eanCheck}";

            case IdentifierSymbology.UPCA:
                string upcBase = $"012345{seed:D5}";
                int upcCheck = SymbologyValidator.CalculateModulo10CheckDigit(upcBase, isEan13: false);
                return $"{upcBase}{upcCheck}";

            case IdentifierSymbology.Code128:
            default:
                return $"BAR-{seed:D8}";
        }
    }

    public static List<TenantSpec> GetTenantSpecs()
    {
        return new List<TenantSpec>
        {
            // 1. Retail
            new(
                "retail", "Lanka Retail Mart", "Retail", "LKR", "retail.admin@example.test",
                Array.Empty<string>(),
                new[] { ("RICE_GRAINS", "Rice & Grains", "Staple food items"), ("APPAREL", "Apparel", "Clothing"), ("SERVICES", "Services", "Store services"), ("BEVERAGES", "Beverages", "Drinks"), ("ACCESSORIES", "Accessories", "Bags & extras") },
                new ProductSpec[]
                {
                    new("RET-001", "Basmati Rice 5 kg", ProductType.StockItem, "BAG", TrackingMode.Batch, ValuationMethod.FIFO, true, true, false, false, false, "RICE_GRAINS", IdentifierSymbology.EAN13, new[] { ("BALE", "Bale 5 Bags", "BALE", 5m) }),
                    new("RET-002", "Cotton T-Shirt Navy M", ProductType.StockItem, "PCS", TrackingMode.None, ValuationMethod.FIFO, true, true, false, false, false, "APPAREL", IdentifierSymbology.UPCA, new[] { ("PACK", "3-Pack T-Shirts", "PACK", 3m) }),
                    new("RET-003", "Garment Alteration Service", ProductType.Service, "JOB", TrackingMode.None, ValuationMethod.StandardCost, false, true, false, false, false, "SERVICES", null, Array.Empty<(string, string, string, decimal)>()),
                    new("RET-004", "Spring Water 500 ml", ProductType.StockItem, "BTL", TrackingMode.Batch, ValuationMethod.WeightedAverage, true, true, false, false, false, "BEVERAGES", IdentifierSymbology.EAN13, new[] { ("CASE", "Case 24 Bottles", "CASE", 24m) }),
                    new("RET-005", "Reusable Shopping Bag", ProductType.StockItem, "PCS", TrackingMode.None, ValuationMethod.FIFO, true, true, false, false, false, "ACCESSORIES", IdentifierSymbology.Code128, new[] { ("PACK", "Pack 10 Bags", "PACK", 10m) })
                }),

            // 2. Wholesale
            new(
                "wholesale", "Island Wholesale & Distribution", "Wholesale & Distribution", "LKR", "wholesale.admin@example.test",
                new[] { "logistics" },
                new[] { ("BEVERAGES", "Beverages", "Bulk beverages"), ("PACKAGING", "Packaging Materials", "Boxes & pallets"), ("SERVICES", "Logistics Services", "Freight services"), ("CONSUMABLES", "Warehouse Consumables", "Wraps & films") },
                new ProductSpec[]
                {
                    new("WHO-001", "Drinking Water 500 ml Bottle", ProductType.StockItem, "BTL", TrackingMode.Batch, ValuationMethod.FIFO, true, true, false, false, false, "BEVERAGES", IdentifierSymbology.EAN13, new[] { ("CARTON", "Carton 24 Bottles", "CARTON", 24m), ("PALLET", "Pallet 1728 Bottles", "PALLET", 1728m) }),
                    new("WHO-002", "Standard Wooden Pallet 1200x1000", ProductType.NonStockItem, "EA", TrackingMode.None, ValuationMethod.FIFO, true, false, false, false, false, "PACKAGING", IdentifierSymbology.Code128, Array.Empty<(string, string, string, decimal)>()),
                    new("WHO-003", "Delivery & Logistics Charge", ProductType.Service, "JOB", TrackingMode.None, ValuationMethod.StandardCost, false, true, false, false, false, "SERVICES", null, Array.Empty<(string, string, string, decimal)>()),
                    new("WHO-004", "Corrugated Storage Carton Large", ProductType.StockItem, "PCS", TrackingMode.None, ValuationMethod.WeightedAverage, true, true, false, false, false, "PACKAGING", IdentifierSymbology.UPCA, new[] { ("PACK", "Pack 25 Cartons", "PACK", 25m) }),
                    new("WHO-005", "Heavy Duty Stretch Film Roll", ProductType.StockItem, "ROLL", TrackingMode.None, ValuationMethod.FIFO, true, true, false, false, false, "CONSUMABLES", IdentifierSymbology.Code128, new[] { ("BOX", "Box 4 Rolls", "BOX", 4m) })
                }),

            // 3. ImpExp
            new(
                "impexp", "Ceylon Import Export", "Import & Export", "USD", "impexp.admin@example.test",
                new[] { "customs" },
                new[] { ("ELECTRONICS", "Consumer Electronics", "Imported tech"), ("TRADE_SRV", "Trade Services", "Customs clearance"), ("FREIGHT", "Freight & Shipping", "Port charges"), ("FOOD_COMM", "Imported Food Commodities", "Grains & spices"), ("ACCESSORIES", "Electronics Accessories", "Cables & adapters") },
                new ProductSpec[]
                {
                    new("IMP-001", "Universal Power Adapter Pro", ProductType.StockItem, "PCS", TrackingMode.Serial, ValuationMethod.FIFO, true, true, false, false, false, "ELECTRONICS", IdentifierSymbology.EAN13, new[] { ("BOX", "Box 10 Adapters", "BOX", 10m) }),
                    new("IMP-002", "Customs Clearance Handling Service", ProductType.Service, "JOB", TrackingMode.None, ValuationMethod.StandardCost, true, true, false, false, false, "TRADE_SRV", null, Array.Empty<(string, string, string, decimal)>()),
                    new("IMP-003", "Port Handling & Demurrage Charge", ProductType.NonStockItem, "EA", TrackingMode.None, ValuationMethod.StandardCost, true, false, false, false, false, "FREIGHT", null, Array.Empty<(string, string, string, decimal)>()),
                    new("IMP-004", "Imported Basmati Rice 20 kg Sack", ProductType.StockItem, "BAG", TrackingMode.Batch, ValuationMethod.WeightedAverage, true, true, false, false, false, "FOOD_COMM", IdentifierSymbology.EAN13, new[] { ("PALLET", "Pallet 50 Sacks", "PALLET", 50m) }),
                    new("IMP-005", "High-Speed USB-C Data Cable 1m", ProductType.StockItem, "PCS", TrackingMode.None, ValuationMethod.FIFO, true, true, false, false, false, "ACCESSORIES", IdentifierSymbology.UPCA, new[] { ("PACK", "Pack 5 Cables", "PACK", 5m) })
                }),

            // 4. Hardware
            new(
                "hardware", "BuildRight Hardware", "Hardware & Building Materials", "LKR", "hardware.admin@example.test",
                new[] { "counter" },
                new[] { ("CEMENT", "Cement & Masonry", "Building supplies"), ("STEEL", "Steel & Structural", "Rebar & beams"), ("TOOLS", "Hand Tools", "Hammers & saws"), ("SERVICES", "Hardware Services", "Key cutting"), ("TRANSPORT", "Transportation", "Delivery") },
                new ProductSpec[]
                {
                    new("HDW-001", "Portland Cement 50 kg Bag", ProductType.StockItem, "BAG", TrackingMode.Batch, ValuationMethod.FIFO, true, true, false, false, false, "CEMENT", IdentifierSymbology.Code128, new[] { ("PALLET", "Pallet 40 Bags", "PALLET", 40m) }),
                    new("HDW-002", "Deformed Steel Rebar 12 mm x 6 m", ProductType.StockItem, "MTR", TrackingMode.None, ValuationMethod.FIFO, true, true, false, false, false, "STEEL", IdentifierSymbology.Code128, Array.Empty<(string, string, string, decimal)>()),
                    new("HDW-003", "Professional Claw Hammer 16 oz", ProductType.StockItem, "PCS", TrackingMode.None, ValuationMethod.WeightedAverage, true, true, false, false, false, "TOOLS", IdentifierSymbology.UPCA, new[] { ("BOX", "Box 6 Hammers", "BOX", 6m) }),
                    new("HDW-004", "Key Duplication Service", ProductType.Service, "JOB", TrackingMode.None, ValuationMethod.StandardCost, false, true, false, false, false, "SERVICES", null, Array.Empty<(string, string, string, decimal)>()),
                    new("HDW-005", "Delivery Charge Flat Rate", ProductType.NonStockItem, "EA", TrackingMode.None, ValuationMethod.StandardCost, false, true, false, false, false, "TRANSPORT", null, Array.Empty<(string, string, string, decimal)>())
                }),

            // 5. Industrial
            new(
                "industrial", "IndustrialPro Supplies", "Industrial & Engineering Supplies", "USD", "industrial.admin@example.test",
                new[] { "technician" },
                new[] { ("LUBRICANTS", "Lubricants & Chemicals", "Oils & fluids"), ("MACHINERY", "Machinery & Spares", "CNC & motors"), ("SERVICES", "Engineering Services", "Maintenance"), ("ENV_FEES", "Environmental Charges", "Waste disposal"), ("PPE", "Safety & PPE", "Gloves & shields") },
                new ProductSpec[]
                {
                    new("IND-001", "Industrial Gear Oil ISO VG 220", ProductType.StockItem, "LTR", TrackingMode.Batch, ValuationMethod.FIFO, true, true, false, false, false, "LUBRICANTS", IdentifierSymbology.Code128, new[] { ("DRUM", "Drum 208 LTR", "DRUM", 208m), ("CAN", "Can 20 LTR", "CAN", 20m) }),
                    new("IND-002", "CNC Machine Spindle Drive 15 kW", ProductType.StockItem, "PCS", TrackingMode.Serial, ValuationMethod.FIFO, true, true, false, false, false, "MACHINERY", IdentifierSymbology.EAN13, Array.Empty<(string, string, string, decimal)>()),
                    new("IND-003", "Preventive Equipment Maintenance Service", ProductType.Service, "HRS", TrackingMode.None, ValuationMethod.StandardCost, true, true, false, false, false, "SERVICES", null, Array.Empty<(string, string, string, decimal)>()),
                    new("IND-004", "Hazardous Material Disposal Charge", ProductType.NonStockItem, "EA", TrackingMode.None, ValuationMethod.StandardCost, true, false, false, false, false, "ENV_FEES", null, Array.Empty<(string, string, string, decimal)>()),
                    new("IND-005", "Nitrile Safety Gloves Heavy Duty L", ProductType.StockItem, "PAIR", TrackingMode.None, ValuationMethod.WeightedAverage, true, true, false, false, false, "PPE", IdentifierSymbology.UPCA, new[] { ("PACK", "Pack 12 Pairs", "PACK", 12m), ("CARTON", "Carton 120 Pairs", "CARTON", 120m) })
                }),

            // 6. Traders
            new(
                "traders", "Lanka General Traders", "General Trading & FMCG", "LKR", "traders.admin@example.test",
                new[] { "cashier" },
                new[] { ("STATIONERY", "Stationery & Office", "Paper & supplies"), ("WRITING", "Writing Instruments", "Pens & pencils"), ("SERVICES", "Document Services", "Printing & binding"), ("HOMEWARE", "Homeware & Gifts", "Ceramics & mugs"), ("FACILITIES", "Facility Expenses", "Waste management") },
                new ProductSpec[]
                {
                    new("TRD-001", "A4 Copy Paper 80 gsm 500 Sheets", ProductType.StockItem, "PACK", TrackingMode.None, ValuationMethod.FIFO, true, true, false, false, false, "STATIONERY", IdentifierSymbology.EAN13, new[] { ("BOX", "Box 5 Packs", "BOX", 5m) }),
                    new("TRD-002", "Ballpoint Pen Blue 0.7 mm", ProductType.StockItem, "PCS", TrackingMode.None, ValuationMethod.FIFO, true, true, false, false, false, "WRITING", IdentifierSymbology.UPCA, new[] { ("BOX", "Box 50 Pens", "BOX", 50m) }),
                    new("TRD-003", "Document Lamination Service A4", ProductType.Service, "JOB", TrackingMode.None, ValuationMethod.StandardCost, false, true, false, false, false, "SERVICES", null, Array.Empty<(string, string, string, decimal)>()),
                    new("TRD-004", "Ceramic Coffee Mug Plain White", ProductType.StockItem, "PCS", TrackingMode.None, ValuationMethod.WeightedAverage, true, true, false, false, false, "HOMEWARE", IdentifierSymbology.EAN13, new[] { ("BOX", "Box 6 Mugs", "BOX", 6m) }),
                    new("TRD-005", "Office Waste Disposal Charge", ProductType.NonStockItem, "EA", TrackingMode.None, ValuationMethod.StandardCost, true, false, false, false, false, "FACILITIES", null, Array.Empty<(string, string, string, decimal)>())
                }),

            // 7. Services
            new(
                "services", "CareFix Services", "Services & Maintenance Contracting", "LKR", "services.admin@example.test",
                new[] { "engineer" },
                new[] { ("HVAC_SPARES", "HVAC Spare Parts", "Boards & motors"), ("MAINT_SRV", "Maintenance Services", "AC servicing"), ("REFRIGERANTS", "Consumables & Refrigerants", "Gas & pipes"), ("PROF_FEES", "Professional Fees", "Inspection fees"), ("SUBCONTRACT", "Subcontracting", "Duct works") },
                new ProductSpec[]
                {
                    new("CFX-001", "AC Outdoor Inverter Board", ProductType.StockItem, "PCS", TrackingMode.Serial, ValuationMethod.FIFO, true, true, false, false, true, "HVAC_SPARES", IdentifierSymbology.Code128, Array.Empty<(string, string, string, decimal)>()),
                    new("CFX-002", "Split AC Full Service Labour", ProductType.Service, "JOB", TrackingMode.None, ValuationMethod.StandardCost, false, true, false, false, false, "MAINT_SRV", null, Array.Empty<(string, string, string, decimal)>()),
                    new("CFX-003", "Refrigerant R32 Gas 1 kg", ProductType.StockItem, "KG", TrackingMode.Batch, ValuationMethod.WeightedAverage, true, true, false, false, true, "REFRIGERANTS", IdentifierSymbology.Code128, Array.Empty<(string, string, string, decimal)>()),
                    new("CFX-004", "Call-Out Inspection Fee", ProductType.Service, "JOB", TrackingMode.None, ValuationMethod.StandardCost, false, true, false, false, false, "PROF_FEES", null, Array.Empty<(string, string, string, decimal)>()),
                    new("CFX-005", "Subcontracted Duct Fabrication", ProductType.NonStockItem, "EA", TrackingMode.None, ValuationMethod.StandardCost, true, false, false, false, false, "SUBCONTRACT", null, Array.Empty<(string, string, string, decimal)>())
                }),

            // 8. Manufacturing
            new(
                "manufacturing", "Ceylon Manufacturing Works", "Manufacturing", "USD", "manufacturing.admin@example.test",
                new[] { "planner" },
                new[] { ("RAW_STEEL", "Raw Materials Steel", "Sheets & coils"), ("FASTENERS", "Fasteners & Hardware", "Screws & bolts"), ("FINISHED_GOODS", "Finished Goods", "Enclosures & racks"), ("OUTSIDE_PROC", "Outside Processing", "Coating & plating"), ("TOOLING", "Tooling & Maintenance", "Sharpening & inserts") },
                new ProductSpec[]
                {
                    new("MFG-001", "Cold Rolled Steel Sheet 1.2 mm", ProductType.StockItem, "SHEET", TrackingMode.Batch, ValuationMethod.FIFO, true, true, false, true, false, "RAW_STEEL", IdentifierSymbology.Code128, Array.Empty<(string, string, string, decimal)>()),
                    new("MFG-002", "Industrial Assembly Screw M6x20", ProductType.StockItem, "PCS", TrackingMode.None, ValuationMethod.WeightedAverage, true, true, false, true, false, "FASTENERS", IdentifierSymbology.UPCA, new[] { ("PACK", "Pack 100 Screws", "PACK", 100m), ("BOX", "Box 1000 Screws", "BOX", 1000m) }),
                    new("MFG-003", "Steel Enclosure Cabinet 600x400", ProductType.StockItem, "PCS", TrackingMode.Serial, ValuationMethod.FIFO, false, true, true, false, false, "FINISHED_GOODS", IdentifierSymbology.EAN13, Array.Empty<(string, string, string, decimal)>()),
                    new("MFG-004", "Powder Coating Service Subcontract", ProductType.Service, "JOB", TrackingMode.None, ValuationMethod.StandardCost, true, false, false, false, false, "OUTSIDE_PROC", null, Array.Empty<(string, string, string, decimal)>()),
                    new("MFG-005", "CNC Tool Sharpening Maintenance Part", ProductType.StockItem, "PCS", TrackingMode.None, ValuationMethod.FIFO, true, false, false, false, true, "TOOLING", IdentifierSymbology.Code128, Array.Empty<(string, string, string, decimal)>())
                }),

            // 9. Food
            new(
                "food", "FreshHarvest Foods", "Food Processing & Dairy", "LKR", "food.admin@example.test",
                new[] { "qc" },
                new[] { ("DAIRY", "Dairy Products", "Milk & cream"), ("DESSERTS", "Frozen Desserts", "Ice cream & tubs"), ("LOGISTICS", "Cold-Chain Logistics", "Reefer transport"), ("INGREDIENTS", "Food Ingredients", "Sugar & flavourings"), ("QA_SRV", "Quality Assurance", "Lab testing") },
                new ProductSpec[]
                {
                    new("FOOD-001", "Pasteurised Fresh Milk 1 L", ProductType.StockItem, "BTL", TrackingMode.Batch, ValuationMethod.FIFO, true, true, false, false, false, "DAIRY", IdentifierSymbology.EAN13, new[] { ("CRATE", "Crate 12 Bottles", "BOX", 12m) }),
                    new("FOOD-002", "Vanilla Ice Cream Tub 2 L", ProductType.StockItem, "TUB", TrackingMode.Batch, ValuationMethod.FIFO, true, true, false, false, false, "DESSERTS", IdentifierSymbology.EAN13, new[] { ("CARTON", "Carton 6 Tubs", "CARTON", 6m) }),
                    new("FOOD-003", "Cold-Chain Reefer Delivery Surcharge", ProductType.NonStockItem, "EA", TrackingMode.None, ValuationMethod.StandardCost, false, true, false, false, false, "LOGISTICS", null, Array.Empty<(string, string, string, decimal)>()),
                    new("FOOD-004", "Refined Granulated Sugar 50 kg", ProductType.StockItem, "BAG", TrackingMode.Batch, ValuationMethod.WeightedAverage, true, true, false, true, false, "INGREDIENTS", IdentifierSymbology.Code128, new[] { ("PALLET", "Pallet 20 Bags", "PALLET", 20m) }),
                    new("FOOD-005", "Food Quality Lab Testing Service", ProductType.Service, "JOB", TrackingMode.None, ValuationMethod.StandardCost, true, false, false, false, false, "QA_SRV", null, Array.Empty<(string, string, string, decimal)>())
                }),

            // 10. Solutions
            new(
                "solutions", "TechBridge Solutions", "Software & IT Services", "USD", "solutions.admin@example.test",
                new[] { "consultant" },
                new[] { ("IT_INFRA", "IT Infrastructure", "Servers & switches"), ("LICENSES", "Software Licenses", "SaaS & subscriptions"), ("CONSULTING", "Consulting Services", "ERP implementation"), ("DEVELOPMENT", "Development Services", "API & coding"), ("NETWORKING", "Networking Accessories", "Cables & patch cords") },
                new ProductSpec[]
                {
                    new("SOL-001", "Rackmount Server 2U Enterprise", ProductType.StockItem, "PCS", TrackingMode.Serial, ValuationMethod.FIFO, true, true, false, false, false, "IT_INFRA", IdentifierSymbology.EAN13, Array.Empty<(string, string, string, decimal)>()),
                    new("SOL-002", "ApexERP User Subscription 1-Year", ProductType.NonStockItem, "EA", TrackingMode.None, ValuationMethod.StandardCost, false, true, false, false, false, "LICENSES", null, Array.Empty<(string, string, string, decimal)>()),
                    new("SOL-003", "ERP Implementation & Migration", ProductType.Service, "DAY", TrackingMode.None, ValuationMethod.StandardCost, false, true, false, false, false, "CONSULTING", null, Array.Empty<(string, string, string, decimal)>()),
                    new("SOL-004", "Custom API Integration Service", ProductType.Service, "HRS", TrackingMode.None, ValuationMethod.StandardCost, false, true, false, false, false, "DEVELOPMENT", null, Array.Empty<(string, string, string, decimal)>()),
                    new("SOL-005", "Cat6 Patch Cord 2 m Molded", ProductType.StockItem, "PCS", TrackingMode.None, ValuationMethod.WeightedAverage, true, true, false, false, false, "NETWORKING", IdentifierSymbology.UPCA, new[] { ("PACK", "Pack 10 Cords", "PACK", 10m) })
                }),

            // 11. Pharma
            new(
                "pharma", "MediCare Pharmacy Demo", "Healthcare & Pharmaceuticals", "LKR", "pharma.admin@example.test",
                new[] { "pharmacist" },
                new[] { ("OTC_MEDS", "Over-the-Counter Medicines", "Pain & fever relief"), ("RX_ANTIBIOTICS", "Prescription Antibiotics", "Capsules & suspensions"), ("DEVICES", "Medical Devices", "Monitors & meters"), ("CLINICAL_SRV", "Clinical Services", "Screening & tests"), ("COMPLIANCE", "Regulatory Compliance", "Waste incineration") },
                new ProductSpec[]
                {
                    new("MED-001", "Paracetamol Tablets 500 mg", ProductType.StockItem, "TABLET", TrackingMode.Batch, ValuationMethod.FIFO, true, true, false, false, false, "OTC_MEDS", IdentifierSymbology.EAN13, new[] { ("BOX", "Box 100 Tablets", "BOX", 100m) }),
                    new("MED-002", "Amoxicillin Capsules 250 mg", ProductType.StockItem, "CAPSULE", TrackingMode.Batch, ValuationMethod.FIFO, true, true, false, false, false, "RX_ANTIBIOTICS", IdentifierSymbology.EAN13, new[] { ("BOX", "Box 50 Capsules", "BOX", 50m) }),
                    new("MED-003", "Digital Blood Pressure Monitor", ProductType.StockItem, "PCS", TrackingMode.Serial, ValuationMethod.FIFO, true, true, false, false, false, "DEVICES", IdentifierSymbology.UPCA, Array.Empty<(string, string, string, decimal)>()),
                    new("MED-004", "Blood Sugar Screening Service", ProductType.Service, "JOB", TrackingMode.None, ValuationMethod.StandardCost, false, true, false, false, false, "CLINICAL_SRV", null, Array.Empty<(string, string, string, decimal)>()),
                    new("MED-005", "Clinical Waste Incineration Fee", ProductType.NonStockItem, "EA", TrackingMode.None, ValuationMethod.StandardCost, true, false, false, false, false, "COMPLIANCE", null, Array.Empty<(string, string, string, decimal)>())
                })
        };
    }
}
