# ParkPalace CRM — Phase 2 Codex execution brief

## Authorization and scope
The operator approved Phase 2 only: .NET backend scaffold, independent CRM SQL Server database, AD authentication/authorization, React frontend, tests and deployment documentation. Do not proceed to Phase 3. Phase 1 report is `Phase1_Sepidar_Discovery.md`; read `ParkPalace_SRS_v1.md` and `ParkPalace_Codex_Master_Prompt.md` before changing files. Inspect existing `D:\ParkPlace` repository and preserve work.

## Non-negotiable safety boundaries
- Never mutate Sepidar databases, including migrations, DDL, DML, stored procedure execution or test fixture writes. Do not connect to Sepidar during Phase 2 unless absolutely necessary for a read-only connectivity check. Do not assume `Sepidar01` is active: Phase 1 lists `Sepidar01` and `Sepidar02` without verified company mapping. Use disabled-by-default configuration for the integration.
- Never request or print secrets in chat, logs, documentation or commits. Use environment variables/secret store and placeholder config; no real passwords. Do not grant app sysadmin or db_owner privileges in production.
- Do not deploy to the production domain controller automatically. Phase 1 reports PDC (`pdc.sahar.loc`, 192.168.1.250) as current IIS/SQL host. This is a security risk; prefer a dedicated domain-member VM. Prepare a deployment plan and seek explicit approval before any production deployment or configuration change.
- Do not alter AD, DNS, IIS, firewall, SQL Server instances or existing site bindings without explicit operator approval. Do not enable remote access or public exposure.

## Target architecture
- ASP.NET Core Web API on installed supported .NET LTS version (detect SDK/runtime first); layered modular monolith: Domain, Application, Infrastructure, Api, Worker (worker placeholder only).
- EF Core SQL Server for independent `ParkPalaceCRM_DB` with migrations targeting CRM only; optimistic concurrency and UTC timestamps; audit trail. Keep Sepidar adapter as interface and disabled placeholder.
- React + TypeScript + Vite frontend, RTL Persian, Jalali date presentation, accessible layout, route guards and API client; do not trust client authorization.
- Authentication: prefer IIS Windows Authentication (Negotiate/Kerberos) for domain-joined LAN users, with AD group-to-app-role mapping. Implement identity abstraction, optional LDAPS mode only behind explicit configuration; never store AD passwords. Test browser negotiation and CORS/credentials behavior. In development provide a strictly development-only mock identity provider.
- Roles: Procurement, Manager, Warehouse, Finance, Admin; permissions enforced server-side. Procurement may create/issue purchase orders without manager approval; manager retains visibility. No procurement implementation in this phase.
- HTTPS via internal trusted certificate in production; use existing port 8888 only after deployment approval. Avoid exposing application via HTTP in production.

## Required repository structure
`src/ParkPalace.Domain`, `src/ParkPalace.Application`, `src/ParkPalace.Infrastructure`, `src/ParkPalace.Api`, `src/ParkPalace.Worker`, `src/parkpalace-web`, `tests/ParkPalace.UnitTests`, `tests/ParkPalace.IntegrationTests`, `docs`, `scripts`.

## Implement in Phase 2
1. Inventory installed tools, repository, current IIS and SQL configuration; write `docs/Phase2_Preflight.md`. Do not change infrastructure.
2. Create solution, projects, DI registrations, health endpoint `/api/health`, OpenAPI in development, standardized errors, structured logging with secret redaction, configuration validation.
3. Define CRM-owned foundational entities: `AppUser` (AD immutable SID/object ID reference, display name, enabled), `Role`, `Permission`, `UserRole`, `RolePermission`, `AuditEvent`, `IntegrationSyncRun` (placeholder). Create CRM-only EF migrations and a reviewed SQL provisioning script. Avoid automatic migration on startup in production. Use least-privilege application DB account.
4. Implement Windows authentication and server-side authorization policies. Bootstrap role mappings via configuration, not hardcoded user identities. Expose `GET /api/me`, `GET /api/roles` (authorized), `GET /api/health`; never expose sensitive AD attributes unnecessarily.
5. React app: login/session state, unauthorized page, navigation shell and placeholders for Dashboard, Materials, Suppliers, Calls, Purchase Requests, Orders, Warehouse, Finance, Administration. Show integration status as `Not configured` rather than fake live data. Include responsive RTL styling and Jalali display utility.
6. Write automated unit tests for permission policies, app-user mapping, audit metadata and input validation; integration tests for health and protected endpoints (unauthenticated => 401; unauthorized => 403; authorized => 200), EF migration tests against isolated test CRM database, frontend build and minimal UI smoke test.
7. Document local development, AD setup prerequisites, IIS hosting bundle, Windows Authentication IIS settings, SPNs/Kerberos considerations, HTTPS, service account, backups, migration/rollback, firewall scope and release checklist. Document how to set secret values locally without committing them.

## Acceptance criteria / Gate G2
- `dotnet restore`, `dotnet build`, `dotnet test`, `npm ci`, `npm run build` succeed, or document precise environmental blockers without claiming success.
- CRM migrations only target `ParkPalaceCRM_DB`; no query/migration writes to Sepidar. Provide evidence of connection-target separation and SQL permissions.
- AuthN/AuthZ tested with a development mock; Windows AD end-to-end only marked passed if actually tested in domain environment.
- `/api/me` returns identity/role data for authorized session, never a password; unauthenticated requests denied.
- React shell renders RTL, routes protected, placeholders clearly labeled; no invented inventory data.
- No plaintext credentials, no public endpoint exposure, no production modifications without approval.
- Produce `docs/Phase2_Implementation_Report.md` with files changed, commands/results, unresolved risks, and explicit request for G2 approval. STOP after Phase 2.

## Execution instruction
Proceed step by step. First report preflight and risks, then implement code and tests in the repository. When credentials or privileged changes are required, stop and request the operator to configure them securely. Never fabricate completed tests or server access.
