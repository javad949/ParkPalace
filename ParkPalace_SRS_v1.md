# ParkPalace Procurement CRM — Software Requirements Specification
Version: 1.0 | Status: Baseline for implementation, subject to schema discovery

## 1. Purpose and scope
An internal Persian RTL procurement CRM for a bakery, with supplier management, multi-level materials catalog, procurement lifecycle, inbound/outbound call tracking and recording links, invoices, stock insights, and read-only reconciliation with Sepidar. Users: Procurement, Manager, Warehouse, Finance, System Administrator.

## 2. Binding decisions
- Deployment: on-premises LAN, IIS, ASP.NET Core, React/TypeScript, dedicated SQL Server CRM database.
- Sepidar: one warehouse; SQL Server READ ONLY. No direct or indirect writes from app, jobs, scripts, tests or migrations to Sepidar. Use a dedicated SELECT-only login and separate connection string. Schema must be discovered, never guessed.
- Procurement can directly issue purchase orders, without manager approval. Manager has oversight, and exceptions are logged.
- Finance manually enters official accounting/warehouse documents into Sepidar. CRM reads them and reconciles, but never posts into Sepidar.
- Telephony: Issabel/Asterisk; AD authentication; Persian Jalali UI, UTC persistence.

## 3. Roles and security
Procurement: supplier/contact/catalog operations, RFQs, purchase orders, call notes, invoice attachments, follow-up tasks. Warehouse: stock view, receive/inspect deliveries, discrepancies. Finance: invoice verification, document matching, payment status and Sepidar link. Manager: reports, audit and read visibility. Admin: users, role-permission mapping, settings, integration diagnostics. Implement action-level RBAC and record-level authorization, not hardcoded role checks. Domain identity via Windows Authentication (Kerberos/Negotiate) in domain environments; LDAPS only if needed. Never store AD passwords. Audit all sensitive mutations. Encrypt transport and protect stored files. Apply retention and lawful notice/access rules for recordings and personal information.

## 4. Functional requirements
FR-01 Supplier parties: individual and legal entity, unique internal ID, multiple phones, addresses, postal codes, national/legal IDs with validation and access control, company representatives/visitors with employment periods, merge duplicates, soft deactivate; no destructive deletion with history.
FR-02 Materials: arbitrary-depth categories, material CRUD, Sepidar external-code mappings, unit of measure and conversions, brand/quality/specification, perishability/shelf-life, supplier-material relationship, supplier search by material, price quote history with validity, currency and unit.
FR-03 Sepidar inbound: scheduled and manual read-only synchronization of catalog, single-warehouse stock, movement and official documents where discoverable. Source timestamps, sync watermarks, diagnostics, idempotent upserts into CRM; distinguish consumption from transfer, return, adjustment and waste. If source data lacks semantics, mark unsupported and do not fabricate.
FR-04 Purchase recommendation: selectable 7/14/30-day consumption average, stock available, incoming confirmed POs, reservations, safety stock, supplier lead time, review period, MOQ, pack rounding, shelf life. Formula baseline: max(0, average_daily_usage*(lead_days+review_days)+safety_stock-(usable_stock+confirmed_incoming-reservations)). Display inputs, staleness and explanation; recommendation is advisory and manually editable.
FR-05 Procurement: purchase request, RFQ, quotations, compare vendors, direct issue PO by procurement, immutable snapshots of unit/price/tax/terms on confirmed PO, status transitions, partial delivery, goods receipt and inspection, shortages/returns, supplier invoice and attachments, three-way match PO/receipt/invoice, exceptions and overrides with reason/audit, payments recorded by finance as status/links.
FR-06 Telephony: AMI real-time inbound/outbound/ringing/answered/missed events; popup known contact/company with multi-match selector, unknown number -> individual/legal contact or miscellaneous call; notes and follow-up; reconcile finalized CDR/CEL, link recordings by call/linked identifiers (not just number/time), playback with authorization, unavailable recordings shown explicitly.
FR-07 Documents: upload PDF/images/audio within size/type constraints, malware scanning or quarantine, checksums, access authorization, no executable uploads, immutable reference to supplier/order/invoice, safe download, retention and backups.
FR-08 Dashboards: daily suggested buys, overdue orders, partial deliveries, supplier price trends, unanswered calls, tasks, invoices pending reconciliation, sync status; filtering/export restricted by permissions.
FR-09 Reconciliation: Finance enters Sepidar documents manually; inbound reconciliation by confirmed official ID when available or ranked candidates by supplier, document number/date, amount, line items, unit conversions and tolerances. Never silently auto-match ambiguous candidates; manual confirmation, exceptions, reversible linkage and audit trail.

## 5. Domain model (proposed, adapt after source discovery)
Identity: Users, Roles, Permissions, UserRoles, RolePermissions, AuditEvents.
Catalog: Categories(parent_id), Materials, Units, MaterialUnitConversions, MaterialExternalMappings, Warehouses(single configured), InventorySnapshots, StockMovements, StockPolicies.
Suppliers: Parties(type), PersonProfiles, OrganizationProfiles, PartyContacts, ContactAffiliations, Phones, Addresses, SupplierMaterials, SupplierQuotes, SupplierPriceHistory.
Communications: Calls, CallLegs, CallParticipants, CallNotes, CallRecordings, FollowUpTasks.
Procurement: PurchaseRequests/Lines, RFQs/Lines, Quotations/Lines, PurchaseOrders/Lines, GoodsReceipts/Lines, SupplierInvoices/Lines, InvoiceAttachments, PaymentReferences, MatchingCases, MatchingExceptions.
Integration: ExternalMappings, SepidarDocumentLinks, SyncRuns, SyncCheckpoints, SyncErrors, OutboxEvents (internal only).
All transactional entities have GUID PK, created/updated timestamps, actor IDs, rowversion for optimistic concurrency, status enums with validated transitions, and indexes on foreign keys/search fields. External Sepidar IDs must be namespaced by source and object type. Use decimal for quantities/prices and explicit IRR/Toman currency semantics. Store timestamps UTC and present Jalali locally.

## 6. API contract (indicative REST, versioned)
GET /api/v1/materials; POST /api/v1/materials; GET /api/v1/materials/{id}/suppliers; GET /api/v1/inventory/stock; GET /api/v1/procurement/recommendations; GET/POST /api/v1/parties; GET/POST /api/v1/contacts; GET/POST /api/v1/calls; POST /api/v1/calls/{id}/notes; GET /api/v1/calls/{id}/recording; GET/POST /api/v1/purchase-orders; POST /api/v1/purchase-orders/{id}/issue; POST /api/v1/goods-receipts; POST /api/v1/supplier-invoices; GET /api/v1/reconciliation/cases; POST /api/v1/reconciliation/cases/{id}/confirm; GET /api/v1/sync/status; POST /api/v1/sync/sepidar/run (admin only); GET /api/v1/reports/dashboard. Define DTOs, OpenAPI, validation, pagination, filtering, errors ProblemDetails, authorization and idempotency keys for mutation requests.

## 7. UI views
Login/session; role-aware dashboard; inventory with staleness and drilldown; category/material tree editor; supplier list/search/detail (contacts, products, quotations, invoices, calls); call popup/timeline/player; procurement recommendation worksheet; RFQ comparison; purchase order detail/status; goods receipt and discrepancy; invoice attachment/matching; tasks; reports; integration monitoring; admin RBAC. RTL responsive design, keyboard navigation, Persian and Latin digit search, Jalali dates and accessibility.

## 8. Non-functional requirements
NFR-01 No Sepidar write privileges. NFR-02 No production schema migrations without approval; migrations apply to CRM DB only. NFR-03 Background sync retries with backoff, watermarking and idempotency. NFR-04 Access-controlled audit log. NFR-05 Structured logs with redaction of secrets/PII. NFR-06 Backups of DB and recordings/docs with restore drill. NFR-07 No assumption that all recordings exist. NFR-08 Pagination and indexing for large catalogs/call histories. NFR-09 Configurable sync intervals and alerting for stale data. NFR-10 Health checks and integration status. NFR-11 Fail closed on AD/RBAC failures. NFR-12 Separate dev/test/prod configs and nonproduction synthetic data.

## 9. Acceptance criteria
AC-01 Dedicated Sepidar SQL login cannot INSERT/UPDATE/DELETE/EXEC write, including from integration test; no Sepidar migrations or writes in source code.
AC-02 Discover actual Sepidar tables/views, document data lineage and show sample stock/movement reconciliation for one warehouse before implementing adapter.
AC-03 Materials and arbitrary-depth categories CRUD, soft deletion safeguards and supplier-material search work.
AC-04 Party records support individual/legal types, multiple representatives/phones, duplicate detection, and supplier activity timeline.
AC-05 For known incoming number show matching party/contact; for multiple matches show selector; unknown can be converted or saved miscellaneous without losing history.
AC-06 Each call note links to correct call, recording where available, and user permission gates playback.
AC-07 Suggested quantity is reproducible from displayed inputs and handles zero usage, negative effective stock, missing lead time, MOQ/pack, stale data and perishables.
AC-08 Procurement can issue PO without manager approval, with logged actor/time and immutable issued line snapshots.
AC-09 Partial receipts and invoice price/quantity mismatches remain open and visible, with authorized exception handling.
AC-10 Finance can link manually entered Sepidar document to CRM PO/invoice; ambiguous matches require confirmation and are reversible.
AC-11 AD auth and action-based RBAC deny unauthorized access on both UI and API; audit all sensitive actions.
AC-12 Test suite includes unit, integration, contract, security, telephony simulator, read-only Sepidar mock, reconciliation fixtures, backup/restore and IIS smoke tests.

## 10. Open dependencies and risks
- Exact Sepidar schema, document semantics, SQL rights, source field definitions and supported official reconciliation keys must be discovered.
- Issabel AMI permissions, CDR/CEL availability, recording path, storage, PBX network and legal recording requirements need validation.
- AD domain and IIS authentication topology need confirmation.
- Business rules for tolerance thresholds, taxes, currencies, stock reservations, quality checks, returns, holidays and purchasing units need owner sign-off.
- Performance, RPO/RTO, file retention and backup storage sizing must be set before production.

## 11. Delivery gates
G1 read-only source inventory and mapping approved; G2 CRM foundation/AD/RBAC tested; G3 catalog/suppliers complete; G4 inventory/recommendation verified; G5 procurement/reconciliation verified; G6 calls/recordings verified; G7 dashboards and security verified; G8 deployment and restore drill approved. No production write or deploy without human approval.
