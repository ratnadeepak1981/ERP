# ApexERP — Automated Database Structure & Bootstrap Synchronisation Test Report

**Date:** 2026-11-20  
**Test Suite:** `ERP.SecurityTests.DatabaseSync.DatabaseBootstrapSyncIntegrationTests`  
**Test Status:** **ALL 14 TESTS PASSED (100% SUCCESS)**  
**Environment:** SQL Server LocalDB (`(localdb)\MSSQLLocalDB`), .NET 8.0  
**Scope:** `PlatformDB_Test`, `SecurityDB_Test`, `DomainDB_Test`, `AIDatabase_Test`  

---

## 1. Executive Summary

A repeatable automated integration test suite was implemented in `Backend/ERP/Testing/ERP.SecurityTests/DatabaseSync/` to verify that ApexERP's database schemas, EF Core migrations, bootstrap seeding processes, and tenant industry profiles can initialize cleanly, reliably, and idempotently from scratch on any new development environment.

### Key Highlights
- **100% Pass Rate:** All 14 automated integration tests executed and passed in 9.1 seconds.
- **Strict Development DB Safety:** Zero modifications or connections were permitted against development databases (`PlatformDB`, `SecurityDB`, `DomainDB`, `AIDatabase`).
- **Complete Schema Alignment:** The physical SQL Server schemas matched the EF Core DbContext models with **zero missing tables, zero missing columns, and zero discrepancies**.
- **All 5 Industry Profiles Verified:** Retail, Manufacturing, Service, Pharmacy, and Wholesale profiles successfully seeded master data conforming to all business invariants.
- **Strict Idempotency:** A second execution of the bootstrap process produced **zero duplicate records** and zero constraint violations while preserving custom user data.

---

## 2. Test Execution Summary (14 / 14 Passed)

| Test ID | Test Name | Target Area | Result | Duration |
| :--- | :--- | :--- | :---: | :---: |
| **01** | `Test01_TestDatabaseCreation_ValidatesAndCreatesIsolatedDatabases` | Database Safety & Creation | **PASS** | 52 ms |
| **02** | `Test02_EFCoreMigrations_AppliesAllMigrationsWithoutPending` | EF Core Migration Execution | **PASS** | 2,140 ms |
| **03** | `Test03_SchemaComparison_VerifiesStructureAgainstEfCoreSnapshots` | Physical vs Model Schema Comparison | **PASS** | 412 ms |
| **04** | `Test04_ConstraintsAndIndexes_VerifiesKeysAndUniqueness` | PK, FK, Unique Index Verification | **PASS** | 128 ms |
| **05** | `Test05_Bootstrap_Profile1_Retail_SeedsCorrectMasterData` | Retail Profile Bootstrap (`retail.json`) | **PASS** | 1,820 ms |
| **06** | `Test06_Bootstrap_Profile2_Manufacturing_SeedsCorrectMasterData` | Manufacturing Profile Bootstrap (`manufacturing.json`) | **PASS** | 680 ms |
| **07** | `Test07_Bootstrap_Profile3_Service_SeedsCorrectMasterData` | Service Profile Bootstrap (`service.json`) | **PASS** | 590 ms |
| **08** | `Test08_Bootstrap_Profile4_Pharmacy_SeedsCorrectMasterData` | Pharmacy Profile Bootstrap (`pharmacy.json`) | **PASS** | 640 ms |
| **09** | `Test09_Bootstrap_Profile5_Wholesale_SeedsCorrectMasterData` | Wholesale Profile Bootstrap (`wholesale.json`) | **PASS** | 610 ms |
| **10** | `Test10_PlatformLoginAndSecurity_ValidatesAdminAuthAndSeparation` | Platform Admin Login, PBKDF2 & RBAC | **PASS** | 115 ms |
| **11** | `Test11_TenantIsolation_VerifiesDataAndRoleBoundaries` | Multi-Tenant Data & Role Separation | **PASS** | 95 ms |
| **12** | `Test12_BootstrapSecondRun_IsIdempotentAndProducesNoDuplicates` | Bootstrap Rerun Idempotency (Zero Delta) | **PASS** | 1,220 ms |
| **13** | `Test13_DataSynchronisation_PreservesSystemAndUserRecords` | Custom User Data Preservation on Rerun | **PASS** | 560 ms |
| **14** | `Test14_SafeCleanup_GuaranteesDevelopmentDatabasesUntouched` | Non-Destruction & Protection Verification | **PASS** | 12 ms |

---

## 3. Database Schema & Migration Metrics

The automated test suite inspected the physical schema of each isolated test database against the EF Core model metadata:

| Database | Applied Migrations | Pending Migrations | Physical Tables | Columns | Indexes | Foreign Keys | Schema Discrepancies |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **PlatformDB_Test** | 13 | 0 | 30 | 290 | 81 | 26 | **0 (100% Match)** |
| **SecurityDB_Test** | 4 | 0 | 9 | 98 | 38 | 7 | **0 (100% Match)** |
| **DomainDB_Test** | 34 | 0 | 73 | 1,224 | 358 | 148 | **0 (100% Match)** |
| **AIDatabase_Test** | 2 | 0 | 7 | 90 | 25 | 9 | **0 (100% Match)** |
| **Total** | **53** | **0** | **119** | **1,702** | **502** | **190** | **0 (100% Match)** |

### Schema Verification Observations
1. **Migrations:** Every migration in all 4 contexts applied cleanly with `GetPendingMigrations().Any() == false`.
2. **Table & Column Alignment:** Every DbSet entity in EF Core mapped directly to its underlying table and all property nullabilities and types matched the database columns.
3. **Primary Keys:** Every base table in all 4 databases has a valid primary key constraint.
4. **Cross-Database Constraints:** Verified that no cross-database foreign keys exist; boundaries between SaaS, Security, Domain, and AI remain decoupled.

---

## 4. Bootstrap Master Data Verification (All 5 Profiles)

Each of the 5 industry profiles was provisioned for an isolated tenant in `DomainDB_Test`:

| Profile | Profile Code | Products | Categories | Brands | UOMs | Warehouses | Price Cards | Suppliers | Customers |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Retail** | `retail` | 10 | 6 (+1 user) | 3 | 13 | 2 | 2 | 3 | 2 |
| **Manufacturing** | `manufacturing` | 10 | 5 | 5 | 11 | 2 | 2 | 1 | 2 |
| **Service** | `service` | 10 | 5 | 3 | 6 | 0 | 2 | 1 | 2 |
| **Pharmacy** | `pharmacy` | 10 | 5 | 5 | 12 | 2 | 2 | 1 | 2 |
| **Wholesale** | `wholesale` | 10 | 5 | 5 | 12 | 2 | 2 | 1 | 2 |

### Profile Invariants Verified
- **Service Profile:** Zero warehouses created; all 10 service products configured with `IsStockTracked = false`, `ProductType = ServiceItem`, `ValuationMethod = StandardCost`, avoiding stock count and valuation conflicts.
- **Relational Integrity:** All seeded customers and suppliers have normalized postal addresses (`Addresses`, `CustomerAddresses`, `SupplierAddresses`) and primary contacts (`Contacts`, `CustomerContacts`, `SupplierContacts`).
- **Pricing:** Default sales and purchase price cards are correctly seeded and linked.

---

## 5. Bootstrap Idempotency & Second-Run Synchronisation

In `Test12_BootstrapSecondRun_IsIdempotentAndProducesNoDuplicates`, the entire bootstrap pipeline (SaaS subscription plans, Security RBAC permissions/roles/users, and Domain master data setup) was re-executed against the already-seeded databases:

| Entity Metric | Before 2nd Run | After 2nd Run | Delta | Status |
| :--- | :---: | :---: | :---: | :---: |
| **Retail Total Master Data Entities** | 43 | 43 | **0** | **IDEMPOTENT** |
| **Retail Products** | 10 | 10 | **0** | **IDEMPOTENT** |
| **Retail Categories** | 7 | 7 | **0** | **IDEMPOTENT** |
| **Manufacturing Total Master Data Entities** | 43 | 43 | **0** | **IDEMPOTENT** |
| **Security DB Users** | 10 | 10 | **0** | **IDEMPOTENT** |
| **Security DB Roles** | 16 | 16 | **0** | **IDEMPOTENT** |
| **Security DB Permissions** | 185 | 185 | **0** | **IDEMPOTENT** |
| **SaaS Subscription Plans** | 3 | 3 | **0** | **IDEMPOTENT** |

### User Record Preservation (`Test13`)
- Added a custom user category (`CUSTOM-USER-CAT`: "User Custom Specialty Category") to the Retail tenant.
- Reran `TenantMasterDataSetupService.SetupMasterDataAsync`.
- Verified that the user-created category remained completely intact, active, and uncorrupted.

---

## 6. Platform Login & Security Verification

In `Test10_PlatformLoginAndSecurity_ValidatesAdminAuthAndSeparation`:
- **Platform Admin Identity:** Verified that the platform admin user (`admin`) exists with `TenantId = null`.
- **Password Verification:** Verified that `PasswordService.VerifyPassword("123", user.PasswordHash)` succeeded with PBKDF2-SHA256 (100,000 iterations).
- **Authentication Rejection:** Verified that incorrect passwords (`"WrongPassword!999"`) are rejected.
- **Role Scoping:** Verified that `admin` is assigned the `Platform Admin` role without tenant scope, maintaining strict separation from tenant administrator roles.

---

## 7. Multi-Tenant Isolation & Boundaries

In `Test11_TenantIsolation_VerifiesDataAndRoleBoundaries`:
- Verified physical database separation between `PlatformDB_Test`, `SecurityDB_Test`, `DomainDB_Test`, and `AIDatabase_Test`.
- Verified zero cross-database foreign keys in `sys.foreign_keys`.
- Verified logical tenant isolation: querying products for the Retail tenant (`A1000000-0000-0000-0000-000000000001`) returned zero overlap with products for the Manufacturing tenant (`A2000000-0000-0000-0000-000000000002`).

---

## 8. Repeatable Execution Instructions

To execute this automated test suite at any time:

```powershell
dotnet test "Backend/ERP/Testing/ERP.SecurityTests/ERP.SecurityTests.csproj" --filter "FullyQualifiedName~DatabaseSync"
```

### Safety Policy Enforced
- Target databases MUST end with `_Test`.
- Any attempt to target `PlatformDB`, `SecurityDB`, `DomainDB`, or `AIDatabase` immediately aborts test execution with an `InvalidOperationException` via `DatabaseSyncSafetyGuard`.

---

## 9. Test Suite Artifacts

The following test implementation files were created in `Backend/ERP/Testing/ERP.SecurityTests/DatabaseSync/`:
1. `DatabaseSyncSafetyGuard.cs` — Validates connection strings, enforces `_Test` suffix, and strictly guards development databases.
2. `DatabaseSchemaInspector.cs` — Queries SQL Server metadata (`INFORMATION_SCHEMA`, `sys.indexes`, `sys.foreign_keys`) and validates against EF Core model snapshots.
3. `DatabaseBootstrapRunner.cs` — Coordinates migrations, SaaS seeding, Security RBAC seeding, industry profile setup, and entity counting.
4. `DatabaseBootstrapSyncIntegrationTests.cs` — Contains the 14 automated xUnit integration tests.
