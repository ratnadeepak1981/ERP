# ApexERP Retail Test Pack — Automated Test Results

## 1. Test Execution Metadata

- **Test Suite:** `ERP.SecurityTests.Retail.RetailIndustryEndToEndIntegrationTests`
- **Assembly:** `Backend\ERP\Testing\ERP.SecurityTests\bin\Debug\net8.0\ERP.SecurityTests.dll`
- **Framework:** .NET 8.0, xUnit 2.8.2
- **Test Server:** ASP.NET Core `WebApplicationFactory<Program>` in-memory integration host
- **Target Databases:** `(localdb)\MSSQLLocalDB` (`DomainDB_Test`, `SecurityDB_Test`, `PlatformDB_Test`)
- **Execution Timestamp:** 2026-11-09 15:55:20 UTC+05:30
- **Duration:** 17.7 seconds
- **Overall Result:** **PASSED (100%)**

---

## 2. Scenario Results Breakdown

| Scenario # | Scenario Description | Expected Outcome | Actual Outcome | Status |
| :---: | :--- | :--- | :--- | :---: |
| **01** | PO Creation & Submission (`retail.po`) | HTTP 201 Created, Status: Submitted | HTTP 201 Created | **PASS** |
| **02** | Unauthorized PO Approval (`retail.po`) | HTTP 403 Forbidden | HTTP 403 Forbidden | **PASS** |
| **03** | Authorized PO Approval (`retail.approver`) | HTTP 200 OK, Status: Approved | HTTP 200 OK | **PASS** |
| **04** | GRN Inbound Split & Confirmation (`retail.grn`) | HTTP 200 OK, 240 units in Dock | HTTP 200 OK | **PASS** |
| **05** | Putaway to Main Storage (`retail.store`) | HTTP 200 OK, Dock cleared, Rack populated | HTTP 200 OK | **PASS** |
| **06** | Miscellaneous Stock Receipt (`retail.store`) | HTTP 200 OK, +35 units in Rack | HTTP 200 OK | **PASS** |
| **07** | Internal Use Stock Issue (`retail.store`) | HTTP 200 OK, -15 units from Rack | HTTP 200 OK | **PASS** |
| **08** | Inter-Warehouse Transfer WH1->WH2 (`retail.store`) | HTTP 200 OK, 40 units moved to WH2 | HTTP 200 OK | **PASS** |
| **09** | Sales Orders Creation & Confirmation (`retail.sales`)| HTTP 201 Created, Status: Confirmed | HTTP 201 Created | **PASS** |
| **10** | Sales Shipments Dispatch (`retail.sales`) | HTTP 200 OK, -75 units dispatched | HTTP 200 OK | **PASS** |
| **11** | FIFO Layer & Balance Reconciliation | All batch and location balances match exactly | All assertions passed | **PASS** |
| **12** | Stock Count Variance Posting (`retail.store`) | Shortage (-2) and Overage (+5) posted | HTTP 200 OK | **PASS** |
| **13** | RBAC Read & Write Denial (`retail.viewer`) | Read allowed (200), Writes blocked (403)| HTTP 200 / 403 as expected | **PASS** |

---

## 3. Automated Console Output Excerpt

```text
Test run for Z:\ERP\Backend\ERP\Testing\ERP.SecurityTests\bin\Debug\net8.0\ERP.SecurityTests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.14.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

[xUnit.net 00:00:15.92]   Finished:    ERP.SecurityTests
  Passed ERP.SecurityTests.Retail.RetailIndustryEndToEndIntegrationTests.CompleteRetailEndToEndWorkflow_AllScenariosPass [13 s]

Test Run Successful.
Total tests: 1
     Passed: 1
 Total time: 17.7061 Seconds
```
