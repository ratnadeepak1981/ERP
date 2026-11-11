# ApexERP Retail Test Pack — Approval Test Cases (Gate 6)

## 1. Purchase Requisition (PR) Approval & Segregation of Duties

### TC-RET-APPR-PR-01: Unauthorized PR Approval Attempt (Negative Test)
- **Actor:** `retail.po` (Requester / Creator of the PR)
- **Role Code:** `PROCUREMENT_OFFICER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Action:** Attempt to call `POST /api/companies/{companyId}/branches/{branchId}/purchase-requisitions/{requisitionId}/approve`
- **Expected Outcome:** **HTTP 403 Forbidden**.
- **Security Invariant:** Standard Procurement Officer lacks `PURCHASE_REQUISITION.APPROVE`. Self-approval of purchase requisitions is strictly blocked at backend API and frontend UI layers.
- **Next Responsible User:** `retail.approver` (Procurement Manager).

---

### TC-RET-APPR-PR-02: Authorized Management PR Approval (Positive Test)
- **Actor:** `retail.approver` (Procurement Manager)
- **Role Code:** `PROCUREMENT_MANAGER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Permissions Verified:** `PURCHASE_REQUISITION.APPROVE`, `PURCHASE_REQUISITION.VIEW`
- **Endpoint:** `POST /api/companies/{companyId}/branches/{branchId}/purchase-requisitions/{requisitionId}/approve`
- **Payload:**
  ```json
  {
    "remarks": "Approved under FY26 Retail replenishment budget for PO conversion."
  }
  ```
- **Expected Outcome:** **HTTP 200 OK**.
- **Resulting PR Status:** `Approved`.
- **Post-Approval State:**
  - PR becomes eligible for Purchase Order creation and conversion.
  - Submitter (`retail.po`) and Approver (`retail.approver`) identities and timestamps are immutably logged in `ApprovalHistories`.
- **Next Responsible User:** `retail.po` (Procurement Officer).

---

### TC-RET-APPR-PR-03: Purchase Requisition Rejection Workflow
- **Actor:** `retail.approver` (Procurement Manager)
- **Role Code:** `PROCUREMENT_MANAGER`
- **Permissions Verified:** `PURCHASE_REQUISITION.APPROVE`
- **Endpoint:** `POST /api/companies/{companyId}/branches/{branchId}/purchase-requisitions/{requisitionId}/reject`
- **Payload:**
  ```json
  {
    "remarks": "Item unit rates exceed approved procurement budget ceiling."
  }
  ```
- **Expected Outcome:** **HTTP 200 OK**. PR transitions to `Rejected`. Line items lock permanently. Cannot be converted to Purchase Order.

---

## 2. Purchase Order (PO) Approval & Segregation of Duties

### TC-RET-APPR-PO-01: Unauthorized PO Approval Attempt (Negative Test)
- **Actor:** `retail.po` (Creator of the Purchase Order)
- **Role Code:** `PROCUREMENT_OFFICER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Action:** Attempt to call `POST /api/companies/{companyId}/branches/{branchId}/purchase-orders/{id}/approve`
- **Expected Outcome:** **HTTP 403 Forbidden**.
- **Security Invariant:** Standard Procurement Officer lacks `PURCHASE_ORDER.APPROVE`. The system prevents any creator from approving their own purchase order.
- **Next Responsible User:** `retail.approver` (Procurement Manager).

---

### TC-RET-APPR-PO-02: Authorized Management PO Approval (Positive Test)
- **Actor:** `retail.approver` (Procurement Manager)
- **Role Code:** `PROCUREMENT_MANAGER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Permissions Verified:** `PURCHASE_ORDER.APPROVE`, `PURCHASE_ORDER.VIEW`
- **Endpoint:** `POST /api/companies/{companyId}/branches/{branchId}/purchase-orders/{id}/approve`
- **Payload:**
  ```json
  {
    "approvedBy": "retail.approver",
    "comments": "Approved under FY26 Retail replenishment budget."
  }
  ```
- **Expected Outcome:** **HTTP 200 OK**.
- **Resulting PO Status:** `Approved`.
- **Post-Approval State:**
  - PO becomes eligible for Goods Receipt Note (GRN) generation.
  - PO line items lock against unapproved modifications.
  - Originating PR status transitions to `Ordered` / fulfillment tracking.
- **Next Responsible User:** `retail.grn` (Receiving Clerk / Procurement Officer).

---

## 3. Segregation of Duties (SoD) & Audit Invariants

1. **Distinct Creator and Approver:**
   - Requester/Creator: `retail.po` (`retail.po@retail.local`)
   - Independent Approver: `retail.approver` (`retail.approver@retail.local`)
   - Any attempt to bypass approval via direct database or API injection fails backend permission and tenant scope checks.
2. **Permanent Audit Trail:**
   - Every approval or rejection persists:
     - `ApprovedByUserId`: GUID of approver (`retail.approver`)
     - `ApprovedByUserName`: `retail.approver`
     - `ApprovalDate`: UTC timestamp
     - `ApprovalRemarks`: Explicit justification entered by approver
3. **Tenant Admin Emergency Oversight:**
   - In emergency business continuity situations, `retail.admin` (Tenant Admin) possesses universal tenant authority to approve transactions, with their distinct admin identity recorded in the audit log.
