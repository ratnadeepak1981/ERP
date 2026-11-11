# ApexERP Retail Test Pack — Approval Test Cases

## 1. Segregation of Duties (SoD) Verification

### Test Case 1: Unauthorized Approval Attempt (Negative Test)
- **Test Identifier:** `TC-RET-APPR-01`
- **Actor:** `retail.po` (Creator of the Purchase Order)
- **Role Code:** `ProcurementOfficer`
- **Action:** Attempt to call `POST /api/companies/{companyId}/branches/{branchId}/purchase-orders/{id}/approve`
- **Expected Outcome:** **HTTP 403 Forbidden**.
- **Security Invariant:** A standard procurement officer role does not possess `Procurement.Orders.Approve`. Self-approval of purchase requisitions or purchase orders is strictly prevented.

---

### Test Case 2: Authorized Management Approval (Positive Test)
- **Test Identifier:** `TC-RET-APPR-02`
- **Actor:** `retail.approver` (Procurement Manager / Financial Controller)
- **Role Code:** `ProcurementManager`
- **Permissions Verified:** `Procurement.Orders.Approve`, `Procurement.Orders.View`
- **Endpoint:** `POST /api/companies/{companyId}/branches/{branchId}/purchase-orders/{id}/approve`
- **Payload:**
  ```json
  {
    "ApprovedBy": "retail.approver",
    "Comments": "Approved under FY26 Retail replenishment budget"
  }
  ```
- **Expected Outcome:** **HTTP 200 OK**.
- **Resulting PO Status:** `Approved` (or `Confirmed`).
- **Post-Approval State:**
  - PO becomes eligible for Goods Receipt Note (GRN) generation.
  - Line items lock against unapproved modifications.
