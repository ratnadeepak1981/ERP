using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Brand;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.MasterData.PaymentTerm;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.TaxCode;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Features.MasterData.Customer;
using ERP.Domain.Features.MasterData.Supplier;
using ERP.Domain.Features.MasterData.Warehouse;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;
using Security.Core.Models;
using Security.Infrastructure.Persistence;
using Security.Services;

namespace ERP.SecurityTests;

public static class TestSeeder
{
    public static void SeedAll(
        SaaSDbContext platformDb,
        SecurityDbContext securityDb,
        DomainDbContext domainDb)
    {
        SeedPlatform(platformDb);
        SeedDomain(domainDb);
        SeedSecurity(securityDb);
    }

    private static void SeedPlatform(SaaSDbContext db)
    {
        if (!db.Tenants.Any(x => x.Id == TestConstants.TenantAlphaId))
        {
            db.Tenants.Add(new Tenant
            {
                Id = TestConstants.TenantAlphaId,
                Name = "Tenant Alpha",
                Code = "ALPHA",
                IsActive = true
            });
        }

        if (!db.Tenants.Any(x => x.Id == TestConstants.TenantBetaId))
        {
            db.Tenants.Add(new Tenant
            {
                Id = TestConstants.TenantBetaId,
                Name = "Tenant Beta",
                Code = "BETA",
                IsActive = true
            });
        }

        var freePlanId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var now = DateTime.UtcNow;

        if (!db.Subscriptions.Any(x => x.TenantId == TestConstants.TenantAlphaId))
        {
            db.Subscriptions.Add(new Subscription
            {
                Id = Guid.NewGuid(),
                TenantId = TestConstants.TenantAlphaId,
                SubscriptionPlanId = freePlanId,
                SubscriptionName = "Tenant Alpha Free Subscription",
                SubscriptionType = "FREE",
                Status = SubscriptionStatus.Active,
                StartDate = now.AddMonths(-1),
                CurrentPeriodStart = now.AddMonths(-1),
                CurrentPeriodEnd = now.AddYears(1),
                BillingAnchorDate = now.AddMonths(-1),
                IsActive = true
            });
        }

        if (!db.Subscriptions.Any(x => x.TenantId == TestConstants.TenantBetaId))
        {
            db.Subscriptions.Add(new Subscription
            {
                Id = Guid.NewGuid(),
                TenantId = TestConstants.TenantBetaId,
                SubscriptionPlanId = freePlanId,
                SubscriptionName = "Tenant Beta Free Subscription",
                SubscriptionType = "FREE",
                Status = SubscriptionStatus.Active,
                StartDate = now.AddMonths(-1),
                CurrentPeriodStart = now.AddMonths(-1),
                CurrentPeriodEnd = now.AddYears(1),
                BillingAnchorDate = now.AddMonths(-1),
                IsActive = true
            });
        }

        if (!db.TenantConfigurations.Any(x => x.TenantId == TestConstants.TenantAlphaId))
        {
            db.TenantConfigurations.Add(TenantConfiguration.CreateDefault(TestConstants.TenantAlphaId));
        }

        if (!db.TenantConfigurations.Any(x => x.TenantId == TestConstants.TenantBetaId))
        {
            db.TenantConfigurations.Add(TenantConfiguration.CreateDefault(TestConstants.TenantBetaId));
        }

        db.SaveChanges();
    }

    private static void SeedDomain(DomainDbContext db)
    {
        // 1. Companies
        if (!db.Companies.Any(x => x.Id == TestConstants.CompanyAId))
        {
            db.Companies.Add(new Company
            {
                Id = TestConstants.CompanyAId,
                TenantId = TestConstants.TenantAlphaId,
                Name = "Company A",
                Code = "COMP_A",
                IsActive = true
            });
        }

        if (!db.Companies.Any(x => x.Id == TestConstants.CompanyBId))
        {
            db.Companies.Add(new Company
            {
                Id = TestConstants.CompanyBId,
                TenantId = TestConstants.TenantAlphaId,
                Name = "Company B",
                Code = "COMP_B",
                IsActive = true
            });
        }

        if (!db.Companies.Any(x => x.Id == TestConstants.CompanyCId))
        {
            db.Companies.Add(new Company
            {
                Id = TestConstants.CompanyCId,
                TenantId = TestConstants.TenantBetaId,
                Name = "Company C",
                Code = "COMP_C",
                IsActive = true
            });
        }

        db.SaveChanges();

        // 2. Branches
        var branches = new[]
        {
            new Branch { Id = TestConstants.BranchA1Id, CompanyId = TestConstants.CompanyAId, Name = "Branch A1", Code = "BR_A1", IsActive = true },
            new Branch { Id = TestConstants.BranchA2Id, CompanyId = TestConstants.CompanyAId, Name = "Branch A2", Code = "BR_A2", IsActive = true },
            new Branch { Id = TestConstants.BranchB1Id, CompanyId = TestConstants.CompanyBId, Name = "Branch B1", Code = "BR_B1", IsActive = true },
            new Branch { Id = TestConstants.BranchB2Id, CompanyId = TestConstants.CompanyBId, Name = "Branch B2", Code = "BR_B2", IsActive = true },
            new Branch { Id = TestConstants.BranchC1Id, CompanyId = TestConstants.CompanyCId, Name = "Branch C1", Code = "BR_C1", IsActive = true }
        };

        foreach (var b in branches)
        {
            if (!db.Branches.Any(x => x.Id == b.Id))
            {
                db.Branches.Add(b);
            }
        }

        db.SaveChanges();

        // 3. Currencies (LKR, USD, EUR, GBP, INR)
        var tenants = new[] { TestConstants.TenantAlphaId, TestConstants.TenantBetaId };
        var currenciesToSeed = new (string Code, string Name, string Symbol, int Decimals)[]
        {
            ("LKR", "Sri Lankan Rupee", "Rs", 2),
            ("USD", "US Dollar", "$", 2),
            ("EUR", "Euro", "€", 2),
            ("GBP", "British Pound", "£", 2),
            ("INR", "Indian Rupee", "₹", 2)
        };

        foreach (var tId in tenants)
        {
            foreach (var c in currenciesToSeed)
            {
                if (!db.Currencies.Any(x => x.TenantId == tId && x.CurrencyCode == c.Code))
                {
                    db.Currencies.Add(Currency.Create(tId, c.Code, c.Name, c.Symbol, c.Decimals));
                }
            }
        }
        db.SaveChanges();

        // 4. Units of Measure (21 standard units)
        var standardUoms = new (string Code, string Name, string Description)[]
        {
            ("PCS", "Pieces", "Individual count unit"),
            ("EA", "Each", "Each individual item"),
            ("BOX", "Box", "Packaging box"),
            ("PACK", "Pack", "Standard bundle or pack"),
            ("CARTON", "Carton", "Shipping carton"),
            ("BOTTLE", "Bottle", "Bottled container"),
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
            ("SET", "Set", "Multi-piece assembly or set")
        };

        foreach (var tId in tenants)
        {
            foreach (var u in standardUoms)
            {
                if (!db.UnitsOfMeasure.Any(x => x.TenantId == tId && x.Code == u.Code))
                {
                    db.UnitsOfMeasure.Add(UnitOfMeasure.Create(tId, u.Code, u.Name, u.Description));
                }
            }
        }
        db.SaveChanges();

        // 5. UOM Conversions
        var conversionRules = new (string FromCode, string ToCode, decimal Factor)[]
        {
            ("BOX", "PCS", 10m),
            ("PACK", "PCS", 6m),
            ("CARTON", "BOX", 12m),
            ("KG", "G", 1000m),
            ("MTR", "CM", 100m),
            ("CM", "MM", 10m),
            ("LTR", "ML", 1000m),
            ("PAIR", "PCS", 2m)
        };

        foreach (var tId in tenants)
        {
            var uomLookup = db.UnitsOfMeasure
                .Where(x => x.TenantId == tId)
                .ToDictionary(x => x.Code, x => x.UnitOfMeasureId);

            foreach (var cr in conversionRules)
            {
                if (uomLookup.TryGetValue(cr.FromCode, out var fromId) &&
                    uomLookup.TryGetValue(cr.ToCode, out var toId))
                {
                    if (!db.UnitOfMeasureConversions.Any(x => x.TenantId == tId && x.FromUnitId == fromId && x.ToUnitId == toId))
                    {
                        db.UnitOfMeasureConversions.Add(UnitOfMeasureConversion.Create(tId, fromId, toId, cr.Factor));
                    }
                }
            }
        }
        db.SaveChanges();

        // 6. Categories (Backwards compatible + 10 industry profiles)
        if (!db.Categories.Any(x => x.CategoryId == TestConstants.CategoryAlphaId))
        {
            db.Categories.Add(Category.Create(TestConstants.TenantAlphaId, "CAT_ALPHA", "Category Alpha", null, TestConstants.CategoryAlphaId));
        }

        if (!db.Categories.Any(x => x.CategoryId == TestConstants.CategoryBetaId))
        {
            db.Categories.Add(Category.Create(TestConstants.TenantBetaId, "CAT_BETA", "Category Beta", null, TestConstants.CategoryBetaId));
        }

        var industryCategories = new (string Code, string Name, string Description)[]
        {
            ("GROCERY", "Groceries & FMCG", "Retail profile category"),
            ("HOUSEHOLD", "Household Goods", "Retail & wholesale goods"),
            ("PACKAGING", "Packaging Materials", "Wholesale & distribution category"),
            ("IMPORTED_GOODS", "Imported General Merchandise", "Import & export category"),
            ("HARDWARE", "Hardware & Building Materials", "Hardware & tools profile category"),
            ("SERVICES", "Maintenance & Services", "Services profile category"),
            ("RAW_MATERIALS", "Raw Materials", "Manufacturing profile category"),
            ("MEDICINES", "Pharmaceuticals & Healthcare", "Pharmacy profile category"),
            ("APPAREL", "Apparel & Safety PPE", "Apparel & safety profile category"),
            ("GENERAL_TRADING", "General Trading Items", "General trading profile category"),
            ("ELECTRONICS", "Electronics & Components", "Electronics industry profile category")
        };

        foreach (var tId in tenants)
        {
            foreach (var c in industryCategories)
            {
                if (!db.Categories.Any(x => x.TenantId == tId && x.CategoryCode == c.Code))
                {
                    db.Categories.Add(Category.Create(tId, c.Code, c.Name, c.Description));
                }
            }
        }
        db.SaveChanges();

        // 7. Brands
        var standardBrands = new (string Code, string Name, string Description)[]
        {
            ("ROYALHARVEST", "Royal Harvest", "Premium agricultural and food brand"),
            ("LUMENPRO", "LumenPro", "Industrial lighting and electrical brand"),
            ("MOTIONPRO", "MotionPro", "Automotive and mechanical parts brand"),
            ("TECHNOVA", "TechNova", "Electronics and computing hardware brand"),
            ("BUILDWELL", "BuildWell", "Construction and structural materials brand"),
            ("SAFEGRIP", "SafeGrip", "PPE and industrial safety gear brand"),
            ("MEDICAREX", "MedicareX", "Pharmaceutical and medical supplies brand"),
            ("OFFICEWISE", "OfficeWise", "Stationery and general office supplies brand")
        };

        foreach (var tId in tenants)
        {
            foreach (var b in standardBrands)
            {
                if (!db.Brands.Any(x => x.TenantId == tId && x.BrandCode == b.Code))
                {
                    db.Brands.Add(Brand.Create(tId, b.Code, b.Name, b.Description));
                }
            }
        }
        db.SaveChanges();

        // 8. Payment Terms
        var standardPaymentTerms = new (string Code, string Name, int Days, string Description)[]
        {
            ("COD", "Cash on Delivery", 0, "Immediate payment upon delivery"),
            ("NET7", "Net 7 Days", 7, "Payment due within 7 calendar days"),
            ("NET15", "Net 15 Days", 15, "Payment due within 15 calendar days"),
            ("NET30", "Net 30 Days", 30, "Payment due within 30 calendar days"),
            ("NET45", "Net 45 Days", 45, "Payment due within 45 calendar days"),
            ("NET60", "Net 60 Days", 60, "Payment due within 60 calendar days")
        };

        foreach (var tId in tenants)
        {
            foreach (var pt in standardPaymentTerms)
            {
                if (!db.PaymentTerms.Any(x => x.TenantId == tId && x.TermCode == pt.Code))
                {
                    db.PaymentTerms.Add(PaymentTerm.Create(tId, pt.Code, pt.Name, pt.Days, pt.Description));
                }
            }
        }
        db.SaveChanges();

        // 9. Tax Codes
        var standardTaxCodes = new (string Code, string Name, decimal Rate, TaxApplicability Applicability, string Description)[]
        {
            ("TAX-STD", "Standard VAT 18%", 18m, TaxApplicability.Both, "Standard national value added tax rate"),
            ("TAX-RED", "Reduced Rate 8%", 8m, TaxApplicability.Both, "Reduced concessionary tax rate"),
            ("TAX-ZERO", "Zero Rated 0%", 0m, TaxApplicability.Both, "Zero rated export or essential goods"),
            ("TAX-EXEMPT", "Exempt 0%", 0m, TaxApplicability.Both, "Exempt items outside tax scope")
        };

        foreach (var tId in tenants)
        {
            foreach (var tc in standardTaxCodes)
            {
                if (!db.TaxCodes.Any(x => x.TenantId == tId && x.Code == tc.Code))
                {
                    db.TaxCodes.Add(TaxCode.Create(tId, tc.Code, tc.Name, tc.Rate, tc.Applicability, description: tc.Description));
                }
            }
        }
        db.SaveChanges();

        // 10. Warehouses & Locations
        if (!db.Warehouses.Any(x => x.WarehouseId == TestConstants.WarehouseAlphaMainId))
        {
            db.Warehouses.Add(Warehouse.Create(
                TestConstants.TenantAlphaId,
                "WH_MAIN",
                "Main Warehouse Alpha",
                "Central distribution warehouse for Tenant Alpha",
                companyId: TestConstants.CompanyAId,
                branchId: TestConstants.BranchA1Id,
                warehouseId: TestConstants.WarehouseAlphaMainId));
            db.SaveChanges();
        }

        var whAlpha = db.Warehouses.First(x => x.WarehouseId == TestConstants.WarehouseAlphaMainId);
        var alphaLocations = new (string Code, string Name)[]
        {
            ("LOC_RCV", "Receiving Dock"),
            ("LOC_BIN1", "Storage Bin A-01"),
            ("LOC_BIN2", "Storage Bin A-02"),
            ("LOC_DISP", "Dispatch Area")
        };

        foreach (var loc in alphaLocations)
        {
            if (!db.WarehouseLocations.Any(x => x.TenantId == whAlpha.TenantId && x.WarehouseId == whAlpha.WarehouseId && x.LocationCode == loc.Code))
            {
                db.WarehouseLocations.Add(WarehouseLocation.Create(
                    whAlpha.TenantId,
                    whAlpha.WarehouseId,
                    null,
                    loc.Code,
                    loc.Name));
            }
        }

        if (!db.Warehouses.Any(x => x.WarehouseId == TestConstants.WarehouseBetaMainId))
        {
            db.Warehouses.Add(Warehouse.Create(
                TestConstants.TenantBetaId,
                "WH_BETA",
                "Main Warehouse Beta",
                "Central warehouse for Tenant Beta",
                companyId: TestConstants.CompanyCId,
                branchId: TestConstants.BranchC1Id,
                warehouseId: TestConstants.WarehouseBetaMainId));
            db.SaveChanges();
        }

        var whBeta = db.Warehouses.First(x => x.WarehouseId == TestConstants.WarehouseBetaMainId);
        var betaLocations = new (string Code, string Name)[]
        {
            ("LOC_RCV_B", "Receiving Dock Beta"),
            ("LOC_BIN1_B", "Storage Bin B-01")
        };

        foreach (var loc in betaLocations)
        {
            if (!db.WarehouseLocations.Any(x => x.TenantId == whBeta.TenantId && x.WarehouseId == whBeta.WarehouseId && x.LocationCode == loc.Code))
            {
                db.WarehouseLocations.Add(WarehouseLocation.Create(
                    whBeta.TenantId,
                    whBeta.WarehouseId,
                    null,
                    loc.Code,
                    loc.Name));
            }
        }
        db.SaveChanges();

        // 11. Suppliers
        if (!db.Suppliers.Any(x => x.SupplierId == TestConstants.SupplierAlphaId))
        {
            db.Suppliers.Add(Supplier.Create(
                TestConstants.TenantAlphaId,
                "SUPP_ALPHA",
                "Apex Supplies Alpha",
                contactPerson: "Alpha Rep",
                email: "supp_a@tenant.local",
                paymentTerms: "NET30",
                supplierId: TestConstants.SupplierAlphaId));
        }

        if (!db.Suppliers.Any(x => x.SupplierId == TestConstants.SupplierBetaId))
        {
            db.Suppliers.Add(Supplier.Create(
                TestConstants.TenantBetaId,
                "SUPP_BETA",
                "Apex Supplies Beta",
                contactPerson: "Beta Rep",
                email: "supp_b@tenant.local",
                paymentTerms: "NET30",
                supplierId: TestConstants.SupplierBetaId));
        }
        db.SaveChanges();

        // 12. Customers
        if (!db.Customers.Any(x => x.CustomerId == TestConstants.CustomerAlphaId))
        {
            db.Customers.Add(Customer.Create(
                TestConstants.TenantAlphaId,
                "CUST_ALPHA",
                "Customer Alpha",
                customerId: TestConstants.CustomerAlphaId));
        }

        if (!db.Customers.Any(x => x.CustomerId == TestConstants.CustomerBetaId))
        {
            db.Customers.Add(Customer.Create(
                TestConstants.TenantBetaId,
                "CUST_BETA",
                "Customer Beta",
                customerId: TestConstants.CustomerBetaId));
        }
        db.SaveChanges();

        // 13. Products & Base UOM Alignment
        var uomPcsAlpha = db.UnitsOfMeasure.FirstOrDefault(x => x.TenantId == TestConstants.TenantAlphaId && x.Code == "PCS");
        var uomPcsBeta = db.UnitsOfMeasure.FirstOrDefault(x => x.TenantId == TestConstants.TenantBetaId && x.Code == "PCS");

        // PROD_ALPHA
        var existingProdAlpha = db.Products.FirstOrDefault(x => x.ProductId == TestConstants.ProductAlphaId);
        if (existingProdAlpha == null)
        {
            db.Products.Add(Product.Create(
                TestConstants.TenantAlphaId,
                "PROD_ALPHA",
                "Product Alpha",
                TestConstants.CategoryAlphaId,
                "PCS",
                null,
                ValuationMethod.FIFO,
                standardCost: 10m,
                sellingPrice: 20m,
                reorderLevel: 10m,
                reorderQuantity: 50m,
                isManufacturable: false,
                isPurchasable: true,
                isSellable: true,
                productId: TestConstants.ProductAlphaId,
                unitOfMeasureId: uomPcsAlpha?.UnitOfMeasureId));
        }
        else if (existingProdAlpha.UnitOfMeasureId == null && uomPcsAlpha != null)
        {
            existingProdAlpha.Update(
                existingProdAlpha.ProductName,
                existingProdAlpha.CategoryId,
                existingProdAlpha.UnitOfMeasure,
                existingProdAlpha.Description,
                existingProdAlpha.ValuationMethod,
                existingProdAlpha.StandardCost,
                existingProdAlpha.SellingPrice,
                existingProdAlpha.ReorderLevel,
                existingProdAlpha.ReorderQuantity,
                existingProdAlpha.IsManufacturable,
                existingProdAlpha.IsPurchasable,
                existingProdAlpha.IsSellable,
                existingProdAlpha.CanConsumeInProduction,
                existingProdAlpha.CanConsumeInMaintenance,
                existingProdAlpha.IsStockTracked,
                existingProdAlpha.ProductType,
                existingProdAlpha.TrackingMode,
                uomPcsAlpha.UnitOfMeasureId,
                existingProdAlpha.MinStockQuantity,
                existingProdAlpha.MaxStockQuantity,
                existingProdAlpha.SafetyStockQuantity,
                existingProdAlpha.LeadTimeDays);
        }

        // PROD_BETA
        var existingProdBeta = db.Products.FirstOrDefault(x => x.ProductId == TestConstants.ProductBetaId);
        if (existingProdBeta == null)
        {
            db.Products.Add(Product.Create(
                TestConstants.TenantBetaId,
                "PROD_BETA",
                "Product Beta",
                TestConstants.CategoryBetaId,
                "PCS",
                null,
                ValuationMethod.FIFO,
                standardCost: 15m,
                sellingPrice: 30m,
                reorderLevel: 5m,
                reorderQuantity: 25m,
                isManufacturable: false,
                isPurchasable: true,
                isSellable: true,
                productId: TestConstants.ProductBetaId,
                unitOfMeasureId: uomPcsBeta?.UnitOfMeasureId));
        }
        else if (existingProdBeta.UnitOfMeasureId == null && uomPcsBeta != null)
        {
            existingProdBeta.Update(
                existingProdBeta.ProductName,
                existingProdBeta.CategoryId,
                existingProdBeta.UnitOfMeasure,
                existingProdBeta.Description,
                existingProdBeta.ValuationMethod,
                existingProdBeta.StandardCost,
                existingProdBeta.SellingPrice,
                existingProdBeta.ReorderLevel,
                existingProdBeta.ReorderQuantity,
                existingProdBeta.IsManufacturable,
                existingProdBeta.IsPurchasable,
                existingProdBeta.IsSellable,
                existingProdBeta.CanConsumeInProduction,
                existingProdBeta.CanConsumeInMaintenance,
                existingProdBeta.IsStockTracked,
                existingProdBeta.ProductType,
                existingProdBeta.TrackingMode,
                uomPcsBeta.UnitOfMeasureId,
                existingProdBeta.MinStockQuantity,
                existingProdBeta.MaxStockQuantity,
                existingProdBeta.SafetyStockQuantity,
                existingProdBeta.LeadTimeDays);
        }
        db.SaveChanges();

        // 14. Representative Industry Demo Products for Tenant Alpha
        var alphaUomLookup = db.UnitsOfMeasure
            .Where(x => x.TenantId == TestConstants.TenantAlphaId)
            .ToDictionary(x => x.Code, x => x.UnitOfMeasureId);
        var alphaCatLookup = db.Categories
            .Where(x => x.TenantId == TestConstants.TenantAlphaId)
            .ToDictionary(x => x.CategoryCode, x => x.CategoryId);

        var demoProducts = new[]
        {
            new {
                Code = "GROC_RICE_01", Name = "Basmati Rice 5kg", Cat = "GROCERY", Uom = "BAG",
                Type = ProductType.StockItem, Tracked = true, Mode = TrackingMode.None,
                Cost = 12m, Price = 18m, CanProduce = false
            },
            new {
                Code = "PKG_BOX_01", Name = "Corrugated Box Large", Cat = "PACKAGING", Uom = "BOX",
                Type = ProductType.StockItem, Tracked = true, Mode = TrackingMode.None,
                Cost = 2.5m, Price = 4.5m, CanProduce = false
            },
            new {
                Code = "IMP_SPICE_01", Name = "Ceylon Cinnamon Sticks", Cat = "IMPORTED_GOODS", Uom = "KG",
                Type = ProductType.StockItem, Tracked = true, Mode = TrackingMode.None,
                Cost = 15m, Price = 25m, CanProduce = false
            },
            new {
                Code = "HDW_HAMMER_01", Name = "Claw Hammer Steel 16oz", Cat = "HARDWARE", Uom = "PCS",
                Type = ProductType.StockItem, Tracked = true, Mode = TrackingMode.None,
                Cost = 8m, Price = 14m, CanProduce = false
            },
            new {
                Code = "SRV_MAINT_01", Name = "Equipment Annual Maintenance", Cat = "SERVICES", Uom = "HRS",
                Type = ProductType.Service, Tracked = false, Mode = TrackingMode.None,
                Cost = 50m, Price = 85m, CanProduce = false
            },
            new {
                Code = "RAW_STEEL_01", Name = "Cold Rolled Steel Sheet", Cat = "RAW_MATERIALS", Uom = "KG",
                Type = ProductType.StockItem, Tracked = true, Mode = TrackingMode.None,
                Cost = 30m, Price = 45m, CanProduce = true
            },
            new {
                Code = "MED_PARA_01", Name = "Paracetamol 500mg 100s", Cat = "MEDICINES", Uom = "PACK",
                Type = ProductType.StockItem, Tracked = true, Mode = TrackingMode.Batch,
                Cost = 4m, Price = 7.5m, CanProduce = false
            },
            new {
                Code = "PPE_GLOVE_01", Name = "Nitrile Safety Gloves", Cat = "APPAREL", Uom = "PAIR",
                Type = ProductType.StockItem, Tracked = true, Mode = TrackingMode.None,
                Cost = 1.2m, Price = 2.5m, CanProduce = false
            },
            new {
                Code = "GEN_PEN_01", Name = "Ballpoint Pen Blue", Cat = "GENERAL_TRADING", Uom = "PCS",
                Type = ProductType.StockItem, Tracked = true, Mode = TrackingMode.None,
                Cost = 0.4m, Price = 0.9m, CanProduce = false
            },
            new {
                Code = "ELEC_SENSOR_01", Name = "Micro Temp Sensor I2C", Cat = "ELECTRONICS", Uom = "PCS",
                Type = ProductType.StockItem, Tracked = true, Mode = TrackingMode.Serial,
                Cost = 6.5m, Price = 12m, CanProduce = false
            }
        };

        foreach (var dp in demoProducts)
        {
            if (!db.Products.Any(x => x.TenantId == TestConstants.TenantAlphaId && x.ProductCode == dp.Code))
            {
                if (alphaCatLookup.TryGetValue(dp.Cat, out var catId) &&
                    alphaUomLookup.TryGetValue(dp.Uom, out var uomId))
                {
                    db.Products.Add(Product.Create(
                        TestConstants.TenantAlphaId,
                        dp.Code,
                        dp.Name,
                        catId,
                        unitOfMeasure: dp.Uom,
                        standardCost: dp.Cost,
                        sellingPrice: dp.Price,
                        reorderLevel: 10m,
                        reorderQuantity: 50m,
                        isManufacturable: false,
                        isPurchasable: true,
                        isSellable: true,
                        canConsumeInProduction: dp.CanProduce,
                        isStockTracked: dp.Tracked,
                        productType: dp.Type,
                        trackingMode: dp.Mode,
                        unitOfMeasureId: uomId));
                }
            }
        }
        db.SaveChanges();

        // 15. Product Warehouse Settings & Supplier Product Prices
        var prodAlpha = db.Products.FirstOrDefault(x => x.ProductId == TestConstants.ProductAlphaId);
        if (prodAlpha != null)
        {
            if (!db.ProductWarehouseSettings.Any(x => x.TenantId == TestConstants.TenantAlphaId && x.ProductId == prodAlpha.ProductId && x.WarehouseId == whAlpha.WarehouseId))
            {
                db.ProductWarehouseSettings.Add(ProductWarehouseSetting.Create(
                    TestConstants.TenantAlphaId,
                    prodAlpha.ProductId,
                    whAlpha.WarehouseId,
                    reorderLevel: 10m,
                    reorderQuantity: 50m,
                    minStockQuantity: 5m,
                    maxStockQuantity: 200m,
                    safetyStockQuantity: 5m,
                    leadTimeDays: 7));
            }

            if (!db.SupplierProductPrices.Any(x => x.TenantId == TestConstants.TenantAlphaId && x.SupplierId == TestConstants.SupplierAlphaId && x.ProductId == prodAlpha.ProductId))
            {
                db.SupplierProductPrices.Add(global::Domain.Features.MasterData.Supplier.SupplierProductPrice.Create(
                    TestConstants.TenantAlphaId,
                    TestConstants.SupplierAlphaId,
                    prodAlpha.ProductId,
                    unitPrice: 25m,
                    effectiveFrom: DateTime.UtcNow.AddMonths(-1),
                    currency: "USD",
                    unitOfMeasureId: prodAlpha.UnitOfMeasureId));
            }
        }
        db.SaveChanges();
    }

    private static void SeedSecurity(SecurityDbContext db)
    {
        // Ensure Roles
        var tenantAdminRole = db.Roles.FirstOrDefault(x => x.Name == "Tenant Admin")
            ?? new Role { Id = Guid.NewGuid(), Name = "Tenant Admin", Description = "Tenant Administrator", IsActive = true };

        var tenantUserRole = db.Roles.FirstOrDefault(x => x.Name == "Tenant User")
            ?? new Role { Id = Guid.NewGuid(), Name = "Tenant User", Description = "General Tenant User", IsActive = true };

        var branchUserRole = db.Roles.FirstOrDefault(x => x.Name == "Branch User")
            ?? new Role { Id = Guid.NewGuid(), Name = "Branch User", Description = "Branch User", IsActive = true };

        var viewerRole = db.Roles.FirstOrDefault(x => x.Name == "Viewer")
            ?? new Role { Id = Guid.NewGuid(), Name = "Viewer", Description = "Read-Only Viewer", IsActive = true };

        if (tenantAdminRole.Id != Guid.Empty && !db.Roles.Any(x => x.Id == tenantAdminRole.Id)) db.Roles.Add(tenantAdminRole);
        if (tenantUserRole.Id != Guid.Empty && !db.Roles.Any(x => x.Id == tenantUserRole.Id)) db.Roles.Add(tenantUserRole);
        if (branchUserRole.Id != Guid.Empty && !db.Roles.Any(x => x.Id == branchUserRole.Id)) db.Roles.Add(branchUserRole);
        if (viewerRole.Id != Guid.Empty && !db.Roles.Any(x => x.Id == viewerRole.Id)) db.Roles.Add(viewerRole);

        db.SaveChanges();

        // Seed Users
        SeedUserWithRole(db, TestConstants.PlatformAdminId, null, "platformadmin", "platform@platform.local");
        SeedUserWithRole(db, TestConstants.TenantAdminAlphaId, TestConstants.TenantAlphaId, "tenantadmin_a", "admin_a@tenant.local", tenantAdminRole);
        SeedUserWithRole(db, TestConstants.TenantAdminBetaId, TestConstants.TenantBetaId, "tenantadmin_b", "admin_b@tenant.local", tenantAdminRole);

        SeedUserWithRole(db, TestConstants.TenantUserAlphaId, TestConstants.TenantAlphaId, "tenantuser_a", "user_a@tenant.local", tenantUserRole);
        
        var compUserA = SeedUserWithRole(db, TestConstants.CompanyUserAId, TestConstants.TenantAlphaId, "compuser_a", "comp_a@tenant.local", branchUserRole);
        EnsureScope(db, compUserA, TestConstants.CompanyAId, null);

        var branchUserA1 = SeedUserWithRole(db, TestConstants.BranchUserA1Id, TestConstants.TenantAlphaId, "branchuser_a1", "branch_a1@tenant.local", branchUserRole);
        EnsureScope(db, branchUserA1, TestConstants.CompanyAId, TestConstants.BranchA1Id);

        var multiBranchUser = SeedUserWithRole(db, TestConstants.MultiBranchUserId, TestConstants.TenantAlphaId, "multibranch_user", "multi@tenant.local", branchUserRole);
        EnsureScope(db, multiBranchUser, TestConstants.CompanyAId, TestConstants.BranchA1Id);
        EnsureScope(db, multiBranchUser, TestConstants.CompanyAId, TestConstants.BranchA2Id);

        var mixedScopeUser = SeedUserWithRole(db, TestConstants.MixedScopeUserId, TestConstants.TenantAlphaId, "mixedscope_user", "mixed@tenant.local", branchUserRole);
        EnsureScope(db, mixedScopeUser, TestConstants.CompanyAId, TestConstants.BranchA1Id);
        EnsureScope(db, mixedScopeUser, TestConstants.CompanyBId, TestConstants.BranchB2Id);

        var readOnlyUser = SeedUserWithRole(db, TestConstants.ReadOnlyUserId, TestConstants.TenantAlphaId, "readonly_user", "readonly@tenant.local", viewerRole);
        EnsureScope(db, readOnlyUser, TestConstants.CompanyAId, TestConstants.BranchA1Id);

        var multiCompUser = SeedUserWithRole(db, TestConstants.MultiCompanyUserABId, TestConstants.TenantAlphaId, "multicomp_user_ab", "multicomp_ab@tenant.local", branchUserRole);
        EnsureScope(db, multiCompUser, TestConstants.CompanyAId, null);
        EnsureScope(db, multiCompUser, TestConstants.CompanyBId, null);

        db.SaveChanges();
    }

    private static UserRole SeedUserWithRole(
        SecurityDbContext db,
        Guid userId,
        Guid? tenantId,
        string username,
        string email,
        Role? role = null)
    {
        var passwordHash = new PasswordService().HashPassword("Password@123");
        var user = db.Users.FirstOrDefault(x => x.Id == userId);
        if (user == null)
        {
            user = new User
            {
                Id = userId,
                TenantId = tenantId,
                Username = username,
                Email = email,
                PasswordHash = passwordHash,
                IsActive = true
            };
            db.Users.Add(user);
            db.SaveChanges();
        }
        else if (user.PasswordHash != passwordHash)
        {
            user.PasswordHash = passwordHash;
            db.SaveChanges();
        }

        if (role == null) return null!;

        var userRole = db.UserRoles.FirstOrDefault(x => x.UserId == userId && x.RoleId == role.Id);
        if (userRole == null)
        {
            userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RoleId = role.Id,
                Role = role
            };
            db.UserRoles.Add(userRole);
            db.SaveChanges();
        }

        return userRole;
    }

    private static void EnsureScope(SecurityDbContext db, UserRole userRole, Guid? companyId, Guid? branchId)
    {
        if (userRole == null) return;

        bool exists = db.UserRoleScopes.Any(x =>
            x.UserRoleId == userRole.Id &&
            x.CompanyId == companyId &&
            x.BranchId == branchId);

        if (!exists)
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
}
