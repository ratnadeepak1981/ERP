# ApexERP Retail Test Pack — Products and Units of Measure (UOM)

## 1. Product Definitions

| Field | Product 1 (Rice) | Product 2 (Cooking Oil) | Product 3 (White Sugar) |
| :--- | :--- | :--- | :--- |
| **Product Code** | `RET-001` | `RET-002` | `RET-003` |
| **Product Name** | `Samba Rice 5kg Pack` | `Pure Vegetable Cooking Oil 1L` | `Refined White Sugar 1kg` |
| **Product ID (DB)** | `F87964B3-D6FF-4BCB-8149-CF2DA37A8C81` | `0C501F09-8C7C-4C81-A6B8-C29BC391C1FA` | `3BEE70B8-B506-407E-A04B-2B20A17BECAE` |
| **Category** | `Staple Foods` | `Cooking Essentials` | `Baking & Sweeteners` |
| **Valuation Method** | `FIFO` | `FIFO` | `FIFO` |
| **Tracking Mode** | `Batch` | `Batch` | `Batch` |
| **Base UOM** | `BAG` (Bag 5kg) | `BTL` (Bottle 1L) | `PKT` (Packet 1kg) |
| **Base UOM ID** | `688B7704-5BF3-49F4-9388-75F402094BE8` | `8F45A4F9-8F79-4A1C-98A6-6C64F860CAAE` | `13D4FF0C-3135-4B80-9937-2E8553255D50` |
| **Purchase Price (LKR)** | `1,500.00` | `850.00` | `320.00` |
| **Selling Price (LKR)** | `1,850.00` | `1,050.00` | `400.00` |
| **Tax Rate** | `0%` (Zero-rated staple) | `18%` (Standard VAT) | `0%` (Zero-rated staple) |
| **Tax ID** | `799A8BC2-D354-4731-9721-EF51B06C1C14` (`TAX-EXEMPT`) | `22BAAA18-0504-4BBE-A893-AA5C9D78B2E0` (`VAT-18`) | `799A8BC2-D354-4731-9721-EF51B06C1C14` (`TAX-EXEMPT`) |

---

## 2. Unit of Measure Conversions

| Product Code | Base UOM | Packaging UOM | Conversion Factor | Formula |
| :--- | :--- | :--- | :--- | :--- |
| `RET-001` | `BAG` | `BOX` (Box of 4 Bags) | `4.0000` | `1 BOX = 4 BAG` |
| `RET-002` | `BTL` | `CTN` (Carton of 12 Bottles) | `12.0000` | `1 CTN = 12 BTL` |
| `RET-003` | `PKT` | `BDL` (Bundle of 10 Packets) | `10.0000` | `1 BDL = 10 PKT` |

---

## 3. Barcodes & Packaging Identifiers

| Product Code | Base Barcode | Outer Pack Barcode | Outer Pack Quantity |
| :--- | :--- | :--- | :--- |
| `RET-001` | `4792001001018` | `14792001001015` | 4 Bags |
| `RET-002` | `4792001002015` | `14792001002012` | 12 Bottles |
| `RET-003` | `4792001003012` | `14792001003019` | 10 Packets |
