# ApexERP Retail Test Pack — Regression Test History

## 1. Regression Run Log

| Run # | Date / Time | Test Suite | Trigger / Purpose | Target DB | Results | Notes |
| :---: | :---: | :--- | :--- | :---: | :---: | :--- |
| **01** | 2026-11-09 14:15 | `RetailIndustryEndToEndIntegrationTests` | Initial end-to-end integration execution | `_Test` | FAILED (Scenario 1) | Missing real DB supplier and user IDs in test harness. |
| **02** | 2026-11-09 14:40 | `RetailIndustryEndToEndIntegrationTests` | Re-test with DB master data alignment | `_Test` | FAILED (Scenario 4) | Batch date mismatch on `OIL-2026-002`. Fixed dates in test harness. |
| **03** | 2026-11-09 15:10 | `RetailIndustryEndToEndIntegrationTests` | Re-test after batch date fix | `_Test` | FAILED (Scenario 5) | `PutawayService` line matching collided on multiple lines with identical `ProductId`. |
| **04** | 2026-11-09 15:30 | `RetailIndustryEndToEndIntegrationTests` | Re-test after `PutawayService` line matching fix | `_Test` | FAILED (Scenario 8) | Leftover transactional records from prior aborted test runs. Added automated pre-test tenant cleanup. |
| **05** | 2026-11-09 15:45 | `RetailIndustryEndToEndIntegrationTests` | Re-test after pre-test tenant cleanup | `_Test` | FAILED (Scenario 12) | Stock count posting collision on idempotency key between write-off and write-in (`RET-DEF-002`). |
| **06** | 2026-11-09 15:55 | `RetailIndustryEndToEndIntegrationTests` | Re-test after `StockCountService` idempotency key fix | `_Test` | **PASSED (100%)** | All 13 scenarios passed cleanly in 17.7 seconds. |
| **07** | 2026-11-09 16:40 | `Gate 6 UI Proxy Execution Suite` | Gate 6 User Manual UI verification against live Angular server (`:4200`) and backend API (`:5182`) | `_Test` | **PASSED (100%)** | 13 of 13 manual test guide scenarios passed (13 PASS, 0 FAIL). Dock balance = 0, stock balances reconciled. |

---

## 2. Verification Sign-Off

- **Lead Integration Agent:** Antigravity AI Engineering Assistant
- **Automated Pass Rate:** 100% (13 of 13 Scenarios)
- **Gate 6 UI Execution Pass Rate:** 100% (13 of 13 Scenarios)
- **Status:** **Gate 6 Manual UI Verification Complete & Formally Verified**
