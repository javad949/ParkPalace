# Codex local CSV takeover — 2026-10-28

Scope: offline diagnostic preview only. No remote connection, deployment, database write, infrastructure change, backup operation or restore drill was performed. Restore remains deferred at the user request.

Existing Antigravity consistency and MissingMovementData corrections were retained and reviewed. Codex removed the CLI companion-directory discovery, rejected missing explicit files instead of falling back, and rejected ambiguous input matches. CSV mode now always uses an in-memory synthetic CRM baseline, irrespective of ambient connection strings.

The exact four input paths, SHA256 values and reported extraction times are in docs/runs/codex_csv_diagnostic_20261028/input_manifest.json. Extraction times cite historical run_summary.md files; filesystem modification times are separately labelled and are not extraction times. The runner scripts/Invoke-CodexOfflineCsvDiagnostic.ps1 validates hashes before invoking the offline CLI with explicit paths. Historical runs are intentionally combined for diagnostics, not represented as a common transactional capture.

Missing movement item rows remain unknown, not an empty complete ledger. Inclusion of item 360 does not prove the absence of unsupported movements. CRM metadata/reserved/incoming values are synthetic; item 369 negative stock remains unchanged. Apply remains unavailable.

Validation: 47 focused unit tests passed, including three additional regression tests for missing explicit inputs, ambiguity and recursive discovery. SQL integration tests were not rerun and are not claimed here. Release publish and offline diagnostic execution were checked. Release SHA256SUMS.txt includes nested files and excludes itself. Existing platform/event/analyzer warnings remain.

The earlier report dated 20261027_234822 is superseded for CSV consistency claims. Current output is under docs/runs/codex_offline_*; use the most recent run with the matching input_manifest.json and binary hash.

Remaining release work is source movement completeness, reliable source capture, and a separately authorized live pilot. These offline checks do not establish production readiness.