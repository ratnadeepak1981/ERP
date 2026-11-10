# ApexERP — Retail Industry Portable Test Pack

## 1. Overview

This test pack defines the authoritative, repeatable and portable test suite for **ApexERP Industry 1 — Retail**.
It ensures that automated integration tests and manual browser execution share the **identical identities, master data codes, quantities, batch numbers, transaction scenarios, and expected outcomes**, while maintaining clean execution separation.

Future industry testing (Industries 2–11) will reuse this exact structure and methodology.

## 2. Directory Structure

- `README.md` — This file. Master index and navigation.
- `Test-Environment.md` — Database configurations, connection strings, safety guards, and isolation topology.
- `Users-and-Roles.md` — Canonical test accounts, login email aliases, password policies, and scope assignments.
- `Permissions-Matrix.md` — Gate 1 verified matrix mapping responsibilities to actual roles, permission codes, and screens.
- `Master-Data.md` — Shared master data definitions (Company, Branch, Currency, Taxes, Suppliers, Customers).
- `Products-and-UOM.md` — Retail item definitions (RET-001, RET-002, RET-003), UOM conversion ratios, and tracking modes.
- `Warehouse-and-Locations.md` — Warehouse A, Warehouse B, zones, staging, and storage bins.
- `Batch-Register.md` — Authoritative batch numbers, manufacturing dates, and expiration dates.
- `Procurement-Test-Cases.md` — Purchase Order creation, item lines, tax calculations, and submission.
- `Approval-Test-Cases.md` — Independent PO approval workflows, rejection checks, and self-approval prevention.
- `GRN-and-Putaway-Test-Cases.md` — Goods Receipt batch allocation, receipt posting, and warehouse putaway.
- `Inventory-Test-Cases.md` — Stock Issue, Stock Receipt, Item Transfer (WH-A to WH-B), and Stock Count.
- `Sales-Test-Cases.md` — Sales Orders, confirmation, allocation, and shipment dispatch.
- `Expected-Stock-Balances.md` — Mathematical baseline and reconciled inventory quantities across warehouses and batches.
- `Transaction-Register.md` — Master audit log of all automated and manual transaction references.
- `Automated-Test-Results.md` — Automated test execution logs, pass/fail metrics, and execution history.
- `Manual-Test-Guide.md` — Step-by-step user manual with exact UI screens, inputs, clicks, and validations.
- `Defect-Register.md` — Formal defect tracking register (`RET-DEF-xxx`) with root cause analysis and severity.
- `Regression-Test-History.md` — Audit log of fixes, re-tests, and verification runs.
