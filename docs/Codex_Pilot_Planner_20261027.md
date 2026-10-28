# Limited pilot planning kernel — 2026-10-27

## Delivered locally

`InventoryPilotPlanner.Build` is a side-effect-free planning kernel. It takes an explicit manifest, a captured source dataset and existing CRM snapshot/movement states. It returns immutable before/after write intents and deterministic manifest/plan hashes. No count-based or arbitrary reconstruction of the historical 220-item list is used.

- Bound to WH-MAIN, source warehouse 1, a specified fiscal ID and date interval.
- Initial pilot requires identical KG units; it does not infer or activate conversions.
- Requires exactly one source stock and existing target snapshot per manifest item.
- Filters outside-manifest records and reports their counts.
- Preserves CRM Reserved and Incoming even if the source supplies other values.
- Counts provenance and timestamp updates even when OnHand is unchanged.
- Handles exact movement keys globally, rejecting collisions in another warehouse/material.
- Handles only unambiguous legacy Receipt keys; uncertain legacy movement provenance stops the plan.
- Keeps receipt and delivery with the same numeric ID distinct.
- Plans status, cancellation and other movement updates, as well as inserts.
- Does not touch catalog entries, recommendations or global checkpoints.

## Validation

Targeted local test run: **9 passed, 0 failed** (8 planner tests plus the live-entry guard test). Includes out-of-scope filtering, preservation of CRM quantities, missing/duplicate stock, unit mismatch, fiscal/date mismatch, legacy ambiguity, key collision, stable hashes, baseline change detection and replay with no further writes.

No production or local SQL Server connection was used. Existing build warnings concern Windows identity platform annotations and an unused telephony event. `git diff --check` also reports pre-existing whitespace in the already-modified generic sync file; unrelated edits were not reformatted.

## Explicitly not completed

This is not an enabled sync route. It is not registered in DI or exposed through an API, and it does not write to a database. The generic live-sync guard remains enabled in local source.

Still required before deployment:
1. A verified capture reader that pins the actual source fiscal period and uses the same input contract; source FiscalYearId in the current contract is caller-supplied metadata, not independently proven.
2. The identified/approved item manifest. No complete historical manifest was found.
3. A transactional SQL Server executor consuming these intents, revalidating mapping/unit and baseline versions, recording actual inserted IDs and before/after audit states. Hashes detect input changes but do not themselves implement concurrency locking or execution authorization.
4. Conditional rollback and isolated SQL Server tests for failures, concurrent changes, cancellation and duplicate runs.
5. UI/report validation for the limited scope before any deployment.

No installation, config change, migration, production sync or Sepidar change occurred.
