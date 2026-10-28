# Read-only Sepidar reader, Manifest extractor & Manual Preview Tool — 2026-10-27

## Scope and Delivered Components

This phase delivered the complete read-only source reader, proposed manifest extraction engine, and manual preview tool for the limited transactional inventory pilot:

1. **`SepidarPilotReader`** (`src/ParkPalace.Infrastructure/Services/SepidarPilotReader.cs`):
   - Strictly read-only connection to Sepidar database.
   - Dynamic fiscal year verification: queries `FMK.FiscalYear` by title (e.g. `1405`) and verifies `FiscalYearId`, `StartDate`, and `EndDate`. Does **not** assume or hardcode ID 10.
   - Warehouse verification against `INV.Stock` for `StockID = 1` and `IsActive = 1`.
   - Point-in-time consistent capture of `INV.ItemStockSummary` and `INV.InventoryReceiptItem` / `INV.InventoryDeliveryItem` constrained to the verified fiscal period `[StartDate, EndDate)`.
   - Rejects with `PILOT_MOVEMENT_DATE_OUT_OF_BOUNDS`, `PILOT_NEGATIVE_QUANTITY`, or unverified fiscal year; declares preview unexecutable if consistency is violated.

2. **`PilotManifestExtractor`** (`src/ParkPalace.Infrastructure/Services/PilotManifestExtractor.cs`):
   - Derives the candidate manifest exclusively from active CRM mappings and materials with compatible `KG` base units.
   - The historical count 220 is treated strictly as an observation, **never as a quota**; no arbitrary inclusions or exclusions are performed.
   - Incomplete Sepidar categories do not prevent entry into the pilot (warnings recorded).
   - Incompatible units (e.g. `COUNT`, `L`, `PACK`), inactive mappings, inactive materials, duplicate mappings/keys, and missing/mismatched snapshot baselines are excluded with explicit, categorized reasons.
   - Generates deterministic `ManifestHash` via SHA-256.

3. **`InventoryPilotPreviewTool`** (`src/ParkPalace.Infrastructure/Services/InventoryPilotPreviewTool.cs`):
   - Manual preview tool operating strictly in **Preview** mode.
   - Guarantees **zero writes** to CRM or Sepidar (pure queries with `AsNoTracking`).
   - Does **not** expose or provide an `Apply` execution path in this phase.
   - Preserves CRM-owned `ReservedQuantity` and `IncomingQuantity`.
   - Emits structured JSON (`docs/pilot_preview_output_20261027.json`) and formatted Markdown report (`docs/pilot_preview_report_20261027.md`).

4. **Manual PowerShell Runner Script** (`scripts/Invoke-InventoryPilotPreview.ps1`):
   - Provides on-demand execution of the preview tool without embedded credentials or secrets.

---

## Validation & Test Evidence

### 1. Unit Tests (`ParkPalace.UnitTests`)
**18 passed, 0 failed** across all pilot unit test suites:
- `PilotManifestExtractorTests` (6 tests):
  - Valid item extraction with deterministic hash.
  - Exclusion of non-KG units (`UNIT_NOT_KG`).
  - Exclusion of inactive mappings and materials (`MAPPING_INACTIVE`, `MATERIAL_INACTIVE`).
  - Exclusion of missing or unit-mismatched snapshot baselines.
  - Category incompleteness tolerance without exclusion.
  - Exclusion of duplicate source IDs across mappings.
- `InventoryPilotPreviewToolTests` (3 tests):
  - Inconsistent reader rejection without writes or mutations.
  - Consistent preview calculation with CRM-owned field preservation.
  - Exclusion of out-of-manifest items and movements from planner.
- `InventoryPilotPlannerTests` (8 tests):
  - Baseline filtering, stable hashing, movement key collision detection.
- `LivePilotEntryGuardTests` (1 test):
  - Generic sync live-entry fail-closed protection.

### 2. Isolated SQL Server Integration Tests (`InventoryPilotSqlServerTests`)
**9 passed, 0 failed** on isolated, dynamically created and destroyed localhost test databases (`PP_CodexPilot_*`):
- `ExecuteAndRollback_PreserveUserEdits_AndRejectDuplicateRun` (Passed)
- `RollbackWithoutLaterEdits_RestoresSnapshotAndDeletesOnlyOwnMovement` (Passed)
- `ChangedBaselineSincePreview_RejectsWithoutWriting` (Passed)
- `FailureAfterInventorySave_RollsBackInventoryAndJournalTogether` (Passed)
- `TrulyConcurrentSessions_OneWins_SecondRejectsOrAborts_LeavingZeroCorruption` (Passed - verifies Serializable isolation and single winner without corruption)
- `CancellationDuringExecution_AbortsTransaction_LeavesZeroChangesOrJournal` (Passed - verifies clean abort on cancellation)
- `CancellationDuringRollback_AbortsTransaction_LeavesDataConsistentForRetry` (Passed - verifies rollback abort and retry idempotence)
- `SepidarPilotReader_OnSqlServer_ValidatesFiscalYearAndConsistentCapture` (Passed - verified with dynamic fiscal year ID 15, stock balances, movements and negative/out-of-bounds rejection)
- `InventoryPilotPreviewTool_OnSqlServer_RunsPreviewWithoutWriting` (Passed - verified end-to-end preview generation and verified 0 database writes/audit events)

---

## Safety and Permissions Boundary Compliance

- No remote hosts or live production databases were contacted.
- No secrets or credentials were printed, logged, or hardcoded.
- No IIS, DNS, firewall, or production database migrations occurred.
- No `Apply` route or production API exposure was added.
