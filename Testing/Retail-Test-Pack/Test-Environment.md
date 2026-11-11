# ApexERP — Retail Test Pack: Test Environment & Safety Architecture

## 1. Safety Architecture and Isolation Boundary

In accordance with ApexERP Safety Rules:
- **No Production Mutation:** Production databases and configurations are never modified.
- **Isolated Databases:**
  - Automated integration tests execute exclusively against dedicated test databases:
    - `PlatformDB_Test`
    - `SecurityDB_Test`
    - `DomainDB_Test`
    - `AIDatabase_Test`
  - Automated test fixtures enforce `DatabaseSafetyGuard.AssertSafeTestDatabase`, requiring all database names to terminate with `_Test`.
- **Tenant Scope Separation:**
  - Dedicated Industry 1 (Retail) Tenant ID: `10000000-0000-0000-0000-000000000001`.
  - Master Company: `Lanka Retail Mart` (or Tenant-specific Retail Company).
  - Primary Branch: `Main Store`.

## 2. Independent Execution Strategy (Automated vs. Manual Parity)

To fulfill the critical requirement that **manual and automated testing share identical identities, item codes, quantities, and scenarios without consuming or conflicting over the same mutable stock records**:

1. **Prefix Distinction for Document Numbers:**
   - **Automated Tests:**
     - PO: `PO-AUTO-RET-001`
     - GRN: `GRN-AUTO-001`
     - Stock Receipt: `REC-AUTO-001`
     - Stock Issue: `ISS-AUTO-001`
     - Transfer: `TRF-AUTO-001`
     - Sales Order: `SO-AUTO-001`, `SO-AUTO-002`
     - Dispatch: `DSP-AUTO-001`
   - **Manual Browser Testing:**
     - PO: `PO-MAN-RET-001`
     - GRN: `GRN-MAN-001`
     - Stock Receipt: `REC-MAN-001`
     - Stock Issue: `ISS-MAN-001`
     - Transfer: `TRF-MAN-001`
     - Sales Order: `SO-MAN-001`, `SO-MAN-002`
     - Dispatch: `DSP-MAN-001`

2. **Stock Independence:**
   - Automated tests run within an isolated test session context.
   - Manual tests run within the active browser session.
   - Because each test flow creates its own upstream purchase and goods receipt, both runs execute identical business transactions and expect identical mathematical balances, without depleting each other's stock.
