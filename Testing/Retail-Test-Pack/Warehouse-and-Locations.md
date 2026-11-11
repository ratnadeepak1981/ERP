# ApexERP Retail Test Pack — Warehouses and Storage Locations

## 1. Warehouse Hierarchy

ApexERP Retail utilizes two distinct physical warehouses to validate both single-site replenishment and multi-site inter-warehouse stock movements.

```
Tenant: 10000000-0000-0000-0000-000000000001
└── Company: Colombo Retail Central (30C46526-3161-455C-B362-227E8BD0A34A)
    └── Branch: Main Retail Store (5CFABB17-CBE0-43C5-99AB-72769D8DECF1)
        ├── Warehouse A: Central Retail Warehouse (RETAIL-WH1)
        │   ├── Location 1: Receiving Dock (LOC-REC-01) [Staging / Inbound]
        │   └── Location 2: Main Pallet Rack Storage (LOC-STK-01) [Pick / Storage]
        │
        └── Warehouse B: Annex Retail Warehouse (RETAIL-WH2)
            └── Location 1: Primary Shelf Storage (LOC-WH2-01) [Pick / Storage]
```

---

## 2. Warehouse Details

| Warehouse Code | Warehouse Name | Warehouse ID (DB) | Role in Testing |
| :--- | :--- | :--- | :--- |
| `RETAIL-WH1` | `Central Retail Warehouse` | `68462A1E-4A4B-4830-9461-129EEC9E8B80` | Primary receiving, putaway, issue, dispatch, and physical counting site. |
| `RETAIL-WH2` | `Annex Retail Warehouse` | `68462A1E-4A4B-4830-9461-129EEC9E8B81` | Secondary receiving site for inter-warehouse inventory transfers. |

---

## 3. Storage Location Details

| Warehouse Code | Location Code | Location Name | Location ID (DB) | Type | Usage |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `RETAIL-WH1` | `LOC-REC-01` | `Receiving Dock 01` | `2E98C77C-E710-4A51-A569-2730C3A6EE78` | Inbound Dock | Staging location where GRNs land initial goods before putaway. |
| `RETAIL-WH1` | `LOC-STK-01` | `Main Rack A-01` | `A93F6649-FFFE-43EA-87BD-004379E0D35F` | Storage Bin | Dedicated picking and inventory storage location for WH1. |
| `RETAIL-WH2` | `LOC-WH2-01` | `WH2 Storage Rack 01` | `A93F6649-FFFE-43EA-87BD-004379E0D360` | Storage Bin | Destination storage bin for transfer shipments from WH1. |

---

## 4. Location Movement Rules

1. **GRN Posting:** Land stock into `LOC-REC-01` (Receiving Dock).
2. **Putaway Posting:** Relieves `LOC-REC-01` and deposits items into `LOC-STK-01`.
3. **Stock Issues & Sales Dispatches:** Deducted directly from `LOC-STK-01`.
4. **Item Transfer:** Deducts from `LOC-STK-01` in `RETAIL-WH1` and deposits into `LOC-WH2-01` in `RETAIL-WH2`.
