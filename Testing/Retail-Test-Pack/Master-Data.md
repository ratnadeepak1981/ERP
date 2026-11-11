# ApexERP — Retail Test Pack: Master Data Inspection & Baseline Register (Gate 2A)

## 1. Retail Master Data Inventory (Actual Discovered Records)

Inspection performed against database instances `DomainDB_Test`, `SecurityDB_Test`, and `PlatformDB_Test` for Tenant ID `10000000-0000-0000-0000-000000000001` (`RETAIL` / `Lanka Retail Mart`).

| Category | Table | Existing Record Count | Identifiers & Codes Found | Active Status | Required Relationships |
|---|---|---:|---|---|---|
| **Tenant** | `PlatformDB_Test.Tenants` | 1 | `10000000-0000-0000-0000-000000000001` (`RETAIL`, Lanka Retail Mart) | Active (`1`) | Root tenant container |
| **Tenant Config** | `PlatformDB_Test.TenantConfigurations` | 1 | `TenantId = 10000000-0000-0000-0000-000000000001` | Active (`SpecialUomConversionEnabled = 1`) | Configures UOM conversion behavior |
| **Company** | `DomainDB_Test.Companies` | 1 | ID `30C46526-3161-455C-B362-227E8BD0A34A` (`RETAIL`, Lanka Retail Mart) | Active (`1`) | Base currency: `LKR` (`0D62DCEC-6CC5-4B74-BF75-05CD4A2358A8`) |
| **Branch** | `DomainDB_Test.Branches` | 1 | ID `5CFABB17-CBE0-43C5-99AB-72769D8DECF1` (`MAIN`, Lanka Retail Mart Main Branch) | Active (`1`) | Owned by Company `RETAIL` |
| **Warehouse** | `DomainDB_Test.Warehouses` | 1 | ID `68462A1E-4A4B-4830-9461-129EEC9E8B80` (`RETAIL-WH1`, Central Warehouse) | Active (`1`) | Company: `RETAIL`, Branch: `MAIN` |
| **Warehouse Zones** | `DomainDB_Test.WarehouseZones` | 0 | None | N/A | None configured |
| **Warehouse Locations** | `DomainDB_Test.WarehouseLocations` | 2 | `2E98C77C-E710-4A51-A569-2730C3A6EE78` (`LOC-REC-01`, Receiving Dock)<br>`A93F6649-FFFE-43EA-87BD-004379E0D35F` (`LOC-STK-01`, General Storage Bin) | Active (`1`) | Owned by `RETAIL-WH1`, Zone: `NULL` |
| **Categories** | `DomainDB_Test.Categories` | 5 | `ACCESSORIES`, `APPAREL`, `BEVERAGES`, `RICE_GRAINS`, `SERVICES` | Active (`1`) | Tenant-owned |
| **Brands** | `DomainDB_Test.Brands` | 0 | None | N/A | None configured |
| **Products** | `DomainDB_Test.Products` | 5 | `RET-001` (Basmati Rice 5 kg, BAG, FIFO, Batch)<br>`RET-002` (Cotton T-Shirt Navy M, PCS, FIFO, None)<br>`RET-003` (Garment Alteration Service, JOB, StdCost, None)<br>`RET-004` (Spring Water 500 ml, BTL, WAvg, Batch)<br>`RET-005` (Reusable Shopping Bag, PCS, FIFO, None) | Active (`1`) | Category and UOM assigned |
| **UOMs** | `DomainDB_Test.UnitsOfMeasure` | 33 | `BAG`, `BOTTLE`, `BALE`, `CASE`, `PACK`, `KG`, `LTR`, `PCS`, `EA`, `BOX`, `TIN`, `CAN`, etc. | Active (`1`) | Standard UOM library |
| **UOM Conversions** | `DomainDB_Test.UnitOfMeasureConversions` | 9 | `KG`->`G` (1000), `MTR`->`CM` (100), `CM`->`MM` (10), `CARTON`->`BOX` (12), `DOZEN`->`PCS` (12), `PACK`->`PCS` (6), `PAIR`->`PCS` (2), `LTR`->`ML` (1000), `BOX`->`PCS` (10) | Active (`1`) | Standard conversions |
| **Product Packagings** | `DomainDB_Test.ProductPackagings` | 7 | `RET-001`: `BALE` (5 Bags), `RET-001-BALE` (5 Bags)<br>`RET-002`: `PACK` (3-Pack), `RET-002-PACK`<br>`RET-004`: `CASE` (24 Bottles), `RET-004-CASE`<br>`RET-005`: `RET-005-PACK` (10 Bags) | Active (`1`) | Linked to Product and PackagingUomId |
| **Product Barcodes** | `DomainDB_Test.ProductIdentifiers` | 4 | `RET-001`: `4790000000013` (EAN13)<br>`RET-002`: `012345000027` (UPCA)<br>`RET-004`: `4790000000044` (EAN13)<br>`RET-005`: `BAR-00000005` (Code128) | Active (`1`) | Linked to Product |
| **Suppliers** | `DomainDB_Test.Suppliers` | 0 | **None** | N/A | **MISSING** |
| **Supplier Prices** | `DomainDB_Test.SupplierProductPrices` | 0 | **None** | N/A | **MISSING** |
| **Customers** | `DomainDB_Test.Customers` | 0 | **None** | N/A | **MISSING** |
| **Currencies** | `DomainDB_Test.Currencies` | 6 | `LKR` (Base, Symbol: Rs, Decimals: 2)<br>`USD`, `EUR`, `GBP`, `INR` (Active)<br>`ZZZ` (Inactive) | 5 Active, 1 Inactive | Tenant-owned |
| **Exchange Rates** | `DomainDB_Test.ExchangeRates` | 0 | **None** | N/A | **MISSING** |
| **Tax Codes** | `DomainDB_Test.TaxCodes` | 0 | **None** | N/A | **MISSING** |
| **Payment Terms** | `DomainDB_Test.PaymentTerms` | 0 | **None** | N/A | **MISSING** |
| **Price Cards** | `DomainDB_Test.PriceCards` | 0 | **None** | N/A | **MISSING** |
| **Price Card Lines** | `DomainDB_Test.PriceCardLines` | 0 | **None** | N/A | **MISSING** |
| **Product Batches** | `DomainDB_Test.ProductBatches` | 2 | `BATCH-RET-001-001` (RET-001, Mfg: 2026-08-29, Exp: 2027-10-29)<br>`BATCH-RET-004-001` (RET-004, Mfg: 2026-08-29, Exp: 2027-10-29) | Active (`1`) | Linked to Product |
| **Inventory Balances** | `DomainDB_Test.InventoryBalances` | 1 | RET-001 in `RETAIL-WH1` / `LOC-REC-01` (`QuantityOnHand = 0.0000`, `QuantityAllocated = 0.0000`) | N/A | Clean zero baseline |
| **Users** | `SecurityDB_Test.Users` | 6 | `retail.admin`, `retail.purchase`, `retail.purchase.manager`, `retail.sales`, `retail.viewer`, `retail.warehouse` | Active (`1`) | Password `123` verified |

---

## 2. Existing vs. Missing Master Data Matrix

| Requirement | Specification Target | Currently in Database | Gap / Status |
|---|---|---|---|
| **Warehouse A** | `WH-RET-A` (Main Store Warehouse) | `RETAIL-WH1` (Central Warehouse) | **Mismatched Code/Name.** Can be aliased or second warehouse added. |
| **Warehouse B** | `WH-RET-B` (Secondary Warehouse for Transfer) | **None** | **MISSING.** Transfer scenario WH-A -> WH-B cannot execute. |
| **Locations in WH-A** | Inbound Dock & Storage Bins | `LOC-REC-01`, `LOC-STK-01` | **Present.** |
| **Locations in WH-B** | Storage Bins in WH-B | **None** | **MISSING.** |
| **Product RET-001** | Rice 5 kg (BAG, Batch, FIFO) | `RET-001` (Basmati Rice 5 kg, BAG, FIFO, Batch) | **MATCH.** |
| **Product RET-002** | Cooking Oil 1 L (BOTTLE, Batch, FIFO) | `RET-002` (Cotton T-Shirt Navy M, PCS, FIFO, None) | **CRITICAL MISMATCH.** Existing record is apparel, no batch tracking. |
| **Product RET-003** | Sugar 1 kg (BAG, Batch, FIFO) | `RET-003` (Garment Alteration Service, JOB, StdCost, None) | **CRITICAL MISMATCH.** Existing record is a non-stock service. |
| **Supplier** | Retail Primary Supplier | **None** | **MISSING.** PO creation blocked. |
| **Supplier Price List** | Contract price for RET-001, RET-002, RET-003 | **None** | **MISSING.** PO auto-pricing blocked. |
| **Customer** | Retail Primary Customer | **None** | **MISSING.** SO creation blocked. |
| **Tax Code** | Standard VAT / Retail Tax (e.g., VAT 0% or 10%) | **None** | **MISSING.** Tax calculation blocked. |
| **Payment Terms** | Net 30 / Immediate | **None** | **MISSING.** Mandatory for PO/SO creation. |
| **Sales Price Card** | Standard Retail Sales Price Card | **None** | **MISSING.** SO pricing resolution blocked. |
| **Test Users** | `retail.po`, `retail.approver`, `retail.grn`, `retail.store` | Names currently `retail.purchase`, `retail.purchase.manager`, `retail.warehouse` | **NAME & ROLE MISMATCH.** Roles lack `StandardRoleTemplates` permission claims. |
