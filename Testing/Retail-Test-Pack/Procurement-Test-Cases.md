# ApexERP Retail Test Pack — Procurement Test Cases (Gate 6)

## 1. Purchase Requisition (PR) Workflow Test Cases

### TC-RET-PR-01: Purchase Requisition Creation (Draft)
- **Actor:** `retail.po` (Procurement Officer)
- **Role Code:** `PROCUREMENT_OFFICER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Permissions Verified:** `PURCHASE_REQUISITION.VIEW`, `PURCHASE_REQUISITION.CREATE`
- **Allowed Actions:** View PR list, Create Draft, Save Draft
- **Restricted Actions:** Approve, Reject (Denied HTTP 403)
- **Endpoint:** `POST /api/companies/{companyId}/branches/{branchId}/purchase-requisitions`
- **Payload:**
  ```json
  {
    "companyId": "30C46526-3161-455C-B362-227E8BD0A34A",
    "branchId": "5CFABB17-CBE0-43C5-99AB-72769D8DECF1",
    "requisitionDate": "2026-11-11T00:00:00Z",
    "requiredDate": "2026-11-18T00:00:00Z",
    "department": "Retail Store Operations",
    "priority": "Medium",
    "remarks": "Quarterly inventory replenishment for retail staple food and cooking essentials."
  }
  ```
- **Expected Outcome:** **HTTP 201 Created**. Requisition created in `Draft` status with generated `RequisitionNumber` (e.g. `PR-RET-2026-001`).
- **Next Step:** Add line items to PR (Step PR-02).

---

### TC-RET-PR-02: Purchase Requisition Line Item Addition (At Least 2 Items)
- **Actor:** `retail.po` (Procurement Officer)
- **Role Code:** `PROCUREMENT_OFFICER`
- **Permissions Verified:** `PURCHASE_REQUISITION.EDIT`
- **Endpoint:** `POST /api/companies/{companyId}/branches/{branchId}/purchase-requisitions/{requisitionId}/items`
- **Lines Added (3 Distinct Retail SKUs — Meets 'At Least 2 Items' Requirement):**
  | Line | Item Code | Description | Requested Qty | UOM | Est. Unit Price (LKR) | Line Total (LKR) |
  | :---: | :--- | :--- | :---: | :---: | :---: | :---: |
  | 1 | `RET-001` | Samba Rice 5kg Pack | 100 | BAG | 1,500.00 | 150,000.00 |
  | 2 | `RET-002` | Cooking Oil 1L | 80 | BTL | 850.00 | 68,000.00 |
  | 3 | `RET-003` | White Sugar 1kg | 60 | PKT | 320.00 | 19,200.00 |
- **Expected Outcome:** **HTTP 200 OK**. Total requested quantity = 240 units. Header total estimated amount = `237,200.00 LKR`. Status remains `Draft`.
- **Next Step:** Submit PR (Step PR-03).

---

### TC-RET-PR-03: Purchase Requisition Submission
- **Actor:** `retail.po` (Procurement Officer)
- **Role Code:** `PROCUREMENT_OFFICER`
- **Permissions Verified:** `PURCHASE_REQUISITION.SUBMIT`
- **Endpoint:** `POST /api/companies/{companyId}/branches/{branchId}/purchase-requisitions/{requisitionId}/submit`
- **Expected Outcome:** **HTTP 200 OK**. Requisition status transitions to `Submitted`. Submitter audit trail captures `retail.po` and timestamp. Lines lock against editing.
- **Segregation of Duties (SoD) Check:** `retail.po` cannot approve the submitted PR (`Approve` button hidden/disabled; API call returns HTTP 403 Forbidden).
- **Next Responsible User:** `retail.approver` (Procurement Manager).

---

### TC-RET-PR-04: Unauthorized Requisition Approval Attempt (Negative SoD Test)
- **Actor:** `retail.po` (Requester)
- **Role Code:** `PROCUREMENT_OFFICER`
- **Action:** Attempt to call `POST /api/companies/{companyId}/branches/{branchId}/purchase-requisitions/{requisitionId}/approve`
- **Expected Outcome:** **HTTP 403 Forbidden**. Self-approval is strictly prevented.
- **Next Responsible User:** `retail.approver` (Procurement Manager).

---

### TC-RET-PR-05: Authorized Requisition Approval (Positive Test)
- **Actor:** `retail.approver` (Procurement Manager)
- **Role Code:** `PROCUREMENT_MANAGER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Permissions Verified:** `PURCHASE_REQUISITION.VIEW`, `PURCHASE_REQUISITION.APPROVE`
- **Allowed Actions:** View, Approve, Reject
- **Endpoint:** `POST /api/companies/{companyId}/branches/{branchId}/purchase-requisitions/{requisitionId}/approve`
- **Payload:**
  ```json
  {
    "remarks": "Approved for Purchase Order conversion under FY26 retail replenishment budget."
  }
  ```
- **Expected Outcome:** **HTTP 200 OK**. Requisition status transitions to `Approved`. Approval audit log captures `ApprovedBy: retail.approver`, approval timestamp, and remarks. The PR is now available for PO conversion.
- **Next Responsible User:** `retail.po` (Procurement Officer).

---

### TC-RET-PR-06: Purchase Requisition Rejection Scenario (Alternative Path)
- **Actor:** `retail.approver` (Procurement Manager)
- **Role Code:** `PROCUREMENT_MANAGER`
- **Permissions Verified:** `PURCHASE_REQUISITION.APPROVE`
- **Endpoint:** `POST /api/companies/{companyId}/branches/{branchId}/purchase-requisitions/{requisitionId}/reject`
- **Payload:**
  ```json
  {
    "remarks": "Budget exhausted for current cycle. Revise requested quantities."
  }
  ```
- **Expected Outcome:** **HTTP 200 OK**. Requisition status transitions to `Rejected`. Line items become locked. Cannot be converted to Purchase Order.

---

### TC-RET-PR-07: Purchase Requisition Cancellation Scenario (Alternative Path)
- **Actor:** `retail.po` (Procurement Officer)
- **Role Code:** `PROCUREMENT_OFFICER`
- **Permissions Verified:** `PURCHASE_REQUISITION.CANCEL`
- **Endpoint:** `POST /api/companies/{companyId}/branches/{branchId}/purchase-requisitions/{requisitionId}/cancel`
- **Payload:**
  ```json
  {
    "remarks": "Requisition superseded by emergency regional bulk contract."
  }
  ```
- **Expected Outcome:** **HTTP 200 OK**. Requisition status transitions to `Cancelled`. Cannot be submitted or converted.

---

## 2. Purchase Order (PO) Workflow Test Cases

### TC-RET-PO-01: Purchase Order Creation from Approved PR
- **Actor:** `retail.po` (Procurement Officer)
- **Role Code:** `PROCUREMENT_OFFICER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Permissions Verified:** `PURCHASE_ORDER.VIEW`, `PURCHASE_ORDER.CREATE`, `SUPPLIER.VIEW`, `PRODUCT.VIEW`
- **Allowed Actions:** View, Create Draft PO, Link Approved PR, Pre-populate Lines, Save Draft
- **Restricted Actions:** Approve PO (Denied HTTP 403)
- **Endpoint:** `POST /api/companies/{companyId}/branches/{branchId}/purchase-orders`
- **Source Requisition:** `PR-RET-2026-001` (from Step TC-RET-PR-05)

#### Header Inputs
- **Supplier:** `Lanka Retail Distributors (Pvt) Ltd` (`SUP-RET-001` / `5E348635-4309-4B52-8700-1B2F04EE5A77`)
- **Order Number:** `PO-RET-2026-001`
- **Order Date:** `2026-11-11T00:00:00Z`
- **Expected Delivery Date:** `2026-11-18T00:00:00Z`
- **Currency:** `LKR`
- **Payment Term:** `Net 30 Days` (`6BDACD4B-A480-4D88-AC01-B1143899723A`)
- **Warehouse:** `RETAIL-WH1` (`Central Retail Warehouse` / `68462A1E-4A4B-4830-9461-129EEC9E8B80`)

#### Line Items (Pre-populated & Linked from PR Lines)
| Line | Item Code | Description | Ordered Qty | Max Allowed (PR Balance) | UOM | Unit Price (LKR) | Tax Code | Tax Rate | Line Subtotal | Line Tax | Total Line |
| :---: | :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| 1 | `RET-001` | Samba Rice 5kg Pack | 100 | 100 | BAG | 1,500.00 | TAX-EXEMPT | 0% | 150,000.00 | 0.00 | 150,000.00 |
| 2 | `RET-002` | Cooking Oil 1L | 80 | 80 | BTL | 850.00 | VAT-18 | 18% | 68,000.00 | 12,240.00 | 80,240.00 |
| 3 | `RET-003` | White Sugar 1kg | 60 | 60 | PKT | 320.00 | TAX-EXEMPT | 0% | 19,200.00 | 0.00 | 19,200.00 |

#### Over-Ordering Validation Check
- **Negative Assertion:** Attempting to set Ordered Qty > PR remaining balance (e.g. 105 BAG on Line 1) must be rejected with validation error: *"Ordered quantity cannot exceed remaining requisition quantity (100)"*.

#### Financial Totals
- **SubTotal:** `237,200.00 LKR`
- **TaxAmount:** `12,240.00 LKR` (18% on Line 2 `RET-002`: `80 * 850 = 68,000 * 18% = 12,240.00`)
- **TotalAmount:** `249,440.00 LKR`
- **Status:** `Draft`
- **Next Step:** Submit PO (Step TC-RET-PO-02).

---

### TC-RET-PO-02: Purchase Order Submission
- **Actor:** `retail.po` (Procurement Officer)
- **Role Code:** `PROCUREMENT_OFFICER`
- **Permissions Verified:** `PURCHASE_ORDER.SUBMIT`
- **Endpoint:** `POST /api/companies/{companyId}/branches/{branchId}/purchase-orders/{id}/submit`
- **Expected Outcome:** **HTTP 200 OK**. PO status transitions to `Submitted`. Lines lock against further modifications.
- **Segregation of Duties (SoD) Check:** `retail.po` cannot approve the submitted PO (`Approve` button hidden/disabled; API call returns HTTP 403 Forbidden).
- **Next Responsible User:** `retail.approver` (Procurement Manager).
