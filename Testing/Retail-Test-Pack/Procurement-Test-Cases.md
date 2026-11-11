# ApexERP Retail Test Pack — Procurement Test Cases

## 1. Test Case Summary

- **Test Identifier:** `TC-RET-PROC-01`
- **Actor:** `retail.po` (Purchase Officer / Operator)
- **Role Code:** `ProcurementOfficer`
- **Permissions Verified:** `Procurement.Orders.Create`, `Procurement.Orders.Submit`, `Procurement.Orders.View`
- **Endpoint:** `POST /api/companies/{companyId}/branches/{branchId}/purchase-orders`
- **Submission Endpoint:** `POST /api/companies/{companyId}/branches/{branchId}/purchase-orders/{id}/submit`

---

## 2. Test Input Payload

### Header
- **Company ID:** `30C46526-3161-455C-B362-227E8BD0A34A`
- **Branch ID:** `5CFABB17-CBE0-43C5-99AB-72769D8DECF1`
- **Supplier:** `Lanka Retail Distributors (Pvt) Ltd` (`SUP-RET-001` / `5E348635-4309-4B52-8700-1B2F04EE5A77`)
- **Order Number:** `PO-RET-2026-001` (or generated unique prefix `PO-RET-001-XXXX`)
- **Order Date:** Current Date (`DateTime.UtcNow`)
- **Expected Delivery Date:** `DateTime.UtcNow.AddDays(7)`
- **Currency:** `LKR`
- **Payment Term:** `Net 30 Days` (`6BDACD4B-A480-4D88-AC01-B1143899723A`)
- **Warehouse:** `RETAIL-WH1` (`68462A1E-4A4B-4830-9461-129EEC9E8B80`)

### Line Items
| Line | Item Code | Description | Ordered Qty | UOM | Unit Price (LKR) | Tax Code | Tax Rate | Line Subtotal | Line Tax | Total Line |
| :---: | :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| 1 | `RET-001` | Samba Rice 5kg Pack | 100 | BAG | 1,500.00 | TAX-EXEMPT | 0% | 150,000.00 | 0.00 | 150,000.00 |
| 2 | `RET-002` | Cooking Oil 1L | 80 | BTL | 850.00 | VAT-18 | 18% | 68,000.00 | 12,240.00 | 80,240.00 |
| 3 | `RET-003` | White Sugar 1kg | 60 | PKT | 320.00 | TAX-EXEMPT | 0% | 19,200.00 | 0.00 | 19,200.00 |

---

## 3. Financial Totals & Acceptance Criteria

- **SubTotal:** `237,200.00 LKR`
- **TaxAmount:** `12,240.00 LKR`
- **TotalAmount:** `249,440.00 LKR`
- **Initial Status:** `Draft`
- **Post-Submission Status:** `Submitted` (or `PendingApproval`)
- **Forbidden Actions:** User `retail.po` MUST NOT be able to call `/approve` (returns HTTP 403 Forbidden).
