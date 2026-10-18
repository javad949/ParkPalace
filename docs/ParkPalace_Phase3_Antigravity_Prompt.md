# ParkPalace Procurement CRM — Phase 3 Execution Brief for Antigravity

## Authorization and scope
Gate G2 is conditionally approved based on the user-provided build and test transcript, not independent verification of the local repository. Begin Phase 3 (Catalog & Suppliers) only. Do not begin Phase 4, alter Sepidar, or deploy to PDC.

## First: resolve G2 carry-over
1. Inspect `D:\ParkPlace`, existing Phase 1/2 documents, source, migrations, tests, and report. Do not overwrite work.
2. Confirm an independent **development/test** CRM SQL Server connection; apply the existing EF Core migration and verify schema, seeding, and repeatability. The reported health endpoint showed `databaseConnected: false`. If no safe development SQL instance or credentials are available, report a blocker; do not silently claim PASS.
3. AD Negotiate/Kerberos is implemented but not proven against a real domain user. Keep production AD verification marked NOT TESTED until a controlled environment is available. Do not make production DC changes.
4. Ensure development authentication bypass is disabled by default outside Development and cannot be enabled in production via a header.

## Architecture constraints
- Existing .NET 8 modular monolith: Domain, Application, Infrastructure, Api, Worker; React/TypeScript/Vite frontend.
- SQL Server database `ParkPalaceCRM_DB` is independent from Sepidar.
- All Sepidar access remains DISABLED in Phase 3; no Sepidar connection required, no DDL/DML/EXEC against Sepidar. Preserve the placeholder adapter and define future mapping contracts only.
- The Phase 1 Sepidar table/column names were hypotheses, not verified schema. Never implement queries against guessed names.
- No secrets in code, logs, documentation, or commits. Use environment configuration/secure secret storage.
- Use EF Core migrations only for the CRM database. Avoid `EnsureCreated` in production.
- Dates stored in UTC; Persian dates displayed in UI. Monetary amounts use decimal and explicit currency (IRR/Toman distinction).
- Audit create/update/disable and important association changes; use optimistic concurrency and validation.

## Domain model: Catalog
Implement coherent entities with stable IDs, timestamps, IsActive/soft-delete where applicable, concurrency tokens, and audit:
- `Category`: Id, ParentId nullable, Name, Code, Description, SortOrder, IsActive. Unlimited-depth tree; forbid self-parent, cycles, and invalid reassignment. Decide and document handling of inactive parents/children.
- `Material`: Id, InternalCode unique, Name, CategoryId, BaseUnitId, Description, Specifications, Brand optional, MinStock optional, SafetyStock optional, LeadTimeDays optional, IsActive. Do not assume inventory data is available yet.
- `UnitOfMeasure`: Id, Code, Name, Precision, Dimension/UnitType.
- `MaterialUnitConversion`: MaterialId, FromUnitId, ToUnitId, Factor decimal > 0; enforce dimensional consistency and round-trip rules. Base unit canonical. Do not use floating point.
- `ExternalItemMapping`: MaterialId, SourceSystem, SourceCompany/Database identifier, ExternalItemId, ExternalItemCode, LastSeenUtc; unique within source context. Create schema only, no live Sepidar reads.
- Optional material tags/specification attributes only if necessary and consistent with existing SRS; do not overengineer.

## Domain model: Suppliers
- `Party`: Id, PartyType (Individual/LegalEntity), DisplayName, IsActive, Notes, Audit fields.
- Individual profile: FirstName, LastName, ShopName optional, NationalId optional, etc.
- Legal profile: LegalName, NationalIdentifier optional, RegistrationNumber optional, EconomicCode optional, etc.
- `PartyContact`: Id, PartyId, PersonName, Position/Role, IsPrimary, IsActive; allow multiple representatives/visitors per legal entity. Consider contacts with multiple organization relationships if SRS requires it; explain decision.
- `PartyPhone`: Id, PartyId and optional ContactId, PhoneType, OriginalValue, NormalizedValue, IsPrimary. Normalize Iranian mobile and landline numbers; don't assume number is globally unique because shared office numbers exist.
- `PartyAddress`: Id, PartyId, AddressType, Province, City, AddressText, PostalCode, IsPrimary.
- `SupplierMaterial`: PartyId, MaterialId, IsPreferred, Status, DefaultLeadTimeDays optional, MinimumOrderQty optional, PurchaseUnitId optional, Notes. Unique active relationship; support many-to-many.
- `SupplierPriceHistory`: SupplierMaterialId, Price decimal, Currency, UnitId, EffectiveFrom, EffectiveTo optional, RecordedAt, Source/Note. Append history rather than overwrite; validate dates and prevent unintended overlapping active quotations per defined business policy.
- `PartyDocument`: metadata, secure storage reference, type, size, content type, upload user/time. Never expose arbitrary filesystem paths or allow executable uploads. Files may be deferred with explicit NOT IMPLEMENTED status if outside approved phase scope.
- Prevent obvious duplicates using normalized IDs and suggestions; don't force uniqueness on names or shared phone numbers. For records with transactional references, deactivate rather than hard delete.
- Treat national IDs, phone numbers and addresses as sensitive; minimize access and avoid logging full values.

## API design
Versioned REST endpoints, DTOs, pagination, sorting, filtering, server-side validation, consistent problem details, authorization and OpenAPI:
- `/api/v1/categories` CRUD/list/tree and safe move/reparent
- `/api/v1/materials` CRUD/list/detail; `/api/v1/materials/{id}/suppliers`
- `/api/v1/units` list/admin CRUD; material unit conversions
- `/api/v1/suppliers` CRUD/list/detail; `/api/v1/suppliers/{id}/materials`
- supplier contacts, phones, addresses, documents metadata, and price history as nested resources
- association add/update/deactivate for supplier-material and query suppliers by material/category/availability
- Never return EF entities directly. Use validation, authorization, and no overposting.
- Return 400/401/403/404/409 appropriately; use concurrency conflict on stale edits.
- Define and test role permissions: Procurement can maintain catalog/suppliers; Manager can view/admin as granted; Warehouse can view catalog and limited edits only if explicitly authorized; Finance can view suppliers and prices as granted; SystemAdmin manages permissions. Enforce on API, not just UI.

## React UI (Persian RTL)
- Catalog: searchable hierarchical category tree, category editor/move, material list with filters, material detail/edit, unit conversion editor, supplier tab.
- Suppliers: searchable list with individual/legal filters, create/edit forms with conditional fields, multiple contacts/phones/addresses, supplied-material selector, price-history view.
- From material detail, click supplier to open supplier detail; from supplier detail, click material to open material detail.
- Reusable forms with loading, empty, error, validation, permission denied, confirmation, and success states. Accessible labels, keyboard navigation, responsive layouts.
- Persian labels and Jalali display, while API values and timestamps remain invariant.
- No mock data presented as production. Clearly label fixtures/dev mode.

## Testing
- Unit tests: category cycle prevention, reparenting, conversion precision and invalid factors, validation of person/legal fields, phone normalization, price history, soft-delete and duplicate logic.
- Integration tests against an isolated test CRM database: migrations, CRUD, filtering/pagination, supplier-material many-to-many, price history, RBAC 401/403, concurrency 409, audit behavior.
- Frontend build and basic component/E2E flows (create category/material/supplier, link them, search both directions).
- Security: SQL injection resistance, input validation, file-upload restrictions if implemented, no secrets, no Sepidar write or active adapter.
- Run `dotnet restore`, `dotnet build ParkPalace.sln`, `dotnet test ParkPalace.sln`, `npm ci` (when lockfile exists), `npm run build`, and report exact output. Do not fabricate results.

## Acceptance gate G3
G3-01 Unlimited category depth and no parent cycles.
G3-02 Material CRUD/search/deactivate with validations.
G3-03 Units and valid material-specific conversions.
G3-04 Individual and legal suppliers with correct fields.
G3-05 Multiple company contacts/visitors and phones/addresses.
G3-06 Many-to-many supplier/material linkage and reverse navigation.
G3-07 Supplier search by name, normalized phone and supplied material.
G3-08 Append-only price history with units/currency/effective dates.
G3-09 Server-side role enforcement and audit.
G3-10 Backend/frontend builds and meaningful unit/integration/UI tests.
G3-11 No Sepidar writes or live Sepidar dependency.
G3-12 CRM development database migration applied and verified.

## Deliverables and stop condition
Create/update source, migrations, tests, OpenAPI, README/setup instructions, and `docs/Phase3_Implementation_Report.md` with file paths, implemented features, test outputs, screenshot descriptions if available, security verification, limitations, G2 carry-over status, and each G3 criterion marked PASS/FAIL/NOT TESTED with evidence.

Implement in small verifiable increments. Do not claim 100% completion if tests are not run. Stop at Gate G3 for user review; do not proceed to Phase 4 or production deployment.
