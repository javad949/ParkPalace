# Local takeover audit — 2026-10-27

Scope: local source and release archive only. No remote scripts executed, no production connection, deployment, migration or sync.

## Verified
- Local API v1.0.7 ZIP SHA256: F7C14200C8E8307429F523F87DF60E7D849B521608C3A15B12B67EA9B9BE25E9.
- API DLL in that ZIP: E298994E2D918520D421935B4EE0C3C4D0E47B638969B536E92A7FAEEB8CBD8B.
- Infrastructure DLL in that ZIP: 97EFA3EF7EA8C42D9DC2646B5FFBAC819B725F0E1DAA79AC5FA85638AC5B2445.
- These match the previously supplied release report, not an independently observed current server.
- API csproj still declares 1.0.6 / 1.0.6+sepidar-safe-sync-v106. Package name alone is not assembly version evidence.

## Findings from source
- IInventorySyncService.TriggerSyncAsync accepts no pilot item manifest.
- InventorySyncService creates or remaps WH-MAIN, processes the full item catalog and can insert materials, categories and mappings. It is not an inventory-only 220-item path.
- Writes are saved at several stages; there is no explicit outer transaction in this service. On failure it records Failed, rather than undoing already saved stages.
- It invokes GenerateAllRecommendationsAsync for the warehouse after sync.
- ExecuteDryRunAsync duplicates decision logic and omits SourceSnapshotKey/provenance and timestamp changes from its update count. Incremental execution uses a checkpoint while preview reads movements with no sinceDate.
- scratch/execute_fresh_live_dryrun.ps1 performs its own SQL and classification loops; it does not call ExecuteDryRunAsync. Its statistics are not proof of the production execution plan.
- No historical 220-item manifest was found in the local project file search. This does not establish that none exists in other Antigravity artifacts. No arbitrary item has been excluded to reconstruct it.

## Local change
Added an entry guard to the generic sync: live-connected adapters and the concrete live adapter are rejected before the lock, job insert or adapter query. This is temporary fail-closed protection, NOT a completed pilot implementation. Fixture execution remains available. No installed server behavior has changed.

## Required next implementation
1. Recover an evidenced historical manifest, or prepare a newly identified candidate manifest for explicit scope acceptance.
2. Build a shared immutable preview/execution plan bound to warehouse, fiscal period and manifest, with no catalog writes.
3. Preserve CRM-owned fields; constrain movement identity and checkpoints to scope; record all planned mutations and conditional rollback evidence.
4. Validate scoped execution and failure behavior in an isolated database before requesting deployment.

Do not run the existing remote scratch scripts: several contain embedded credentials. Do not copy those values into reports or tests.
## Validation completed
- Targeted local test LivePilotEntryGuardTests passed: 1 passed, 0 failed. It calls the live entry twice without credentials and asserts rejection before tracked writes or job creation. No production or external database is used.
- Build emitted existing unused-event and Windows-platform warnings; no build error.
- Also inspected the property names and aggregate counters in the historical Antigravity scratch/dry_run_output.json under brain 1b80eddf-04be-40f7-b281-a3a265da5b09. It contains aggregate AllowedItems=220 and SampleBlockedItems, but no complete allowed-item manifest. Those counts cannot identify the exact historical 220 items.
