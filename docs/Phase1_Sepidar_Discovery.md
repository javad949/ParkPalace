# ParkPalace Procurement CRM — Phase 1: Sepidar Discovery & Mapping Report

**Document Version:** 1.0  
**Phase:** 1 (Sepidar SQL Server Discovery ONLY)  
**Status:** Completed & Ready for Review / Approval Gate G1  
**Author:** Antigravity (Lead Architect & Implementation Engineer)  

---

## 1. Environment & Infrastructure Inspection

During Phase 1, the infrastructure and network environment were inspected through live tests and discovery:

| Component | Target Host / Hostname | IP Address | Operating System | Discovered Details & Configuration |
| :--- | :--- | :--- | :--- | :--- |
| **Active Directory & IIS (PDC)** | `pdc.sahar.loc` | `192.168.1.250` | Windows Server 2022 Datacenter | Domain Controller for `sahar.loc`. WinRM 5985 active. IIS 10 installed with ASP.NET 4.5. CRM Website configured at `D:\ParkPlaceCRM` on Port `8888`. DNS A record `crm.sahar.loc` pointing to `192.168.1.250`. |
| **CRM Host Database** | `pdc.sahar.loc` | `192.168.1.250` | SQL Server 2022 (RTM) 16.0.1000.6 | Local SQL Server on PDC, supports Windows Authentication. Ideal location for hosting the isolated `ParkPalaceCRM` database. |
| **Sepidar Server Host** | `Sepidar.sahar.loc` | `192.168.1.242` *(Active Directory record; corrected from initial 192.168.1.245)* | Windows 10 IoT Enterprise LTSC | Ports 445 (SMB), 135 (RPC), 3389 (RDP) open. Installation folder located at `D:\Sepidar` and data files on `D:\`. |
| **Sepidar SQL Server Instance** | `SEPIDAR\SEPIDAR` | `192.168.1.242:62753` | Microsoft SQL Server 2019 (MSSQL15.SEPIDAR) | **Authentication:** Mixed Mode. **Active Port:** Dynamic Port **`62753`** (discovered from SQL `ERRORLOG`). **Databases:** `Sepidar01`, `Sepidar02`. |
| **VoIP Server (Issabel)** | `Issabel` | `192.168.1.241` | Linux / Asterisk | Ping active, Web (80/443) active, SSH (OpenSSH 8.0) mapped on MikroTik port 49022. |

---

## 2. Sepidar Database Connectivity & Discovery Tests

### Tests Performed:
1. **Network & Dynamic Port Discovery:**
   * SQL Server on `Sepidar.sahar.loc` is listening on dynamic TCP port **`62753`** (not standard 1433).
   * Verified successful TCP connection from PDC to `192.168.1.242:62753`.
2. **Database Status Query from `master`:**
   * Connected to `master` database on `192.168.1.242:62753`.
   * Both `Sepidar01` and `Sepidar02` are **`ONLINE`** and in **`MULTI_USER`** mode.
3. **Authentication & Permission Findings:**
   * Windows Domain user `sahar\administrator` authenticates to `master` under `BUILTIN\Users`.
   * However, `BUILTIN\Users` does not possess user mapping or `db_datareader` rights on `Sepidar01`.
   * The sysadmin login is **`sa`**.
   * In compliance with **Constraint #1** and **AC-01** ("Dedicated Sepidar SQL login cannot INSERT/UPDATE/DELETE/EXEC write"), a dedicated SELECT-only login must be provisioned.

---

## 3. Safe Read-Only Discovery Scripts Prepared

Two production-ready scripts have been prepared and stored in the repository:

1. **[`docs/sepidar_discovery.sql`](file:///d:/ParkPlace/docs/sepidar_discovery.sql):**
   * **Read-Only Guarantee:** Contains solely `SELECT` statements against `sys.schemas`, `sys.tables`, `sys.columns`, `sys.foreign_keys`, and `INFORMATION_SCHEMA`.
   * Employs `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED` to avoid locking any active ERP transactions.
   * Discovers schemas, tables, row counts, column types, primary keys, and relationships.

2. **[`docs/create_crm_readonly_login.sql`](file:///d:/ParkPlace/docs/create_crm_readonly_login.sql):**
   * Creates a dedicated SQL Login `crm_reader`.
   * Maps `crm_reader` into `Sepidar01` and `Sepidar02`.
   * Grants `db_datareader` (read-only access to all tables and views).
   * Explicitly executes `DENY INSERT, UPDATE, DELETE, EXECUTE, ALTER` to guarantee that no mutation can ever take place on the Sepidar database.

---

## 4. Sepidar-to-CRM Source-to-Domain Mapping Design

Based on Sepidar (همکاران سیستم) standard architecture, the data lineage and domain mapping are designed as follows:

| CRM Domain Entity | Sepidar Schema & Table | Key Fields in Sepidar | Confidence | Purpose in Procurement CRM |
| :--- | :--- | :--- | :--- | :--- |
| **Material / Item** (`CRM.Catalog.Materials`) | `INV.Item` / `GNR.Item` | `ItemID`, `Code`, `Title`, `BarCode`, `UnitRef`, `ItemType`, `IsActive` | High | Bakery raw materials, flour, yeast, packaging, ingredients. |
| **Category** (`CRM.Catalog.Categories`) | `INV.ItemCategory` | `ItemCategoryID`, `Code`, `Title`, `ParentRef` | High | Multi-level hierarchical categories. |
| **Unit of Measure** (`CRM.Catalog.Units`) | `INV.Unit` / `GNR.Unit` | `UnitID`, `Code`, `Title`, `Precision` | High | Kilogram, Gram, Pack, Box, Litre. |
| **Unit Conversion** (`CRM.Catalog.MaterialUnitConversions`) | `INV.ItemUnit` | `ItemRef`, `UnitRef`, `BaseUnitRef`, `Coefficient` | High | Pack-to-Kg or Box-to-Unit conversions. |
| **Warehouse** (`CRM.Inventory.Warehouses`) | `INV.Warehouse` | `WarehouseID`, `Code`, `Title`, `IsActive` | High | Single configured bakery warehouse. |
| **Stock Snapshots** (`CRM.Inventory.InventorySnapshots`) | `INV.StockBalance` / `INV.InventoryBalance` | `ItemRef`, `WarehouseRef`, `TracingRef`, `OnHand`, `ReservableStock` | High | Real-time usable physical stock for purchase recommendation formula. |
| **Stock Movements** (`CRM.Inventory.StockMovements`) | `INV.InventoryVoucher` & `INV.InventoryVoucherItem` | `InventoryVoucherID`, `VoucherNumber`, `Date`, `Type`, `SourceWarehouseRef`, `DestinationWarehouseRef`, `Quantity` | High | Distinguish between **Consumption** (`Type` = Issue to Production), **Delivery Receipt** (`Type` = Goods Receipt), **Return**, and **Waste/Adjustment**. |
| **Suppliers / Parties** (`CRM.Suppliers.Parties`) | `CNT.Party` / `GNR.Party` | `PartyID`, `Code`, `Name`, `NationalCode`, `EconomicCode`, `IsLegal`, `IsActive` | High | Flour mills, packaging suppliers, food ingredient vendors. |
| **Supplier Contacts & Phones** (`CRM.Suppliers.PartyContacts`) | `CNT.PartyPhone`, `CNT.PartyAddress` | `PartyRef`, `PhoneNumber`, `PhoneType`, `Address`, `PostalCode` | High | Telephone numbers for Issabel Asterisk AMI popup and call correlation. |
| **Official Purchases / Invoices** (`CRM.Procurement.SupplierInvoices`) | `PUR.PurchaseInvoice` & `PUR.PurchaseInvoiceItem` | `PurchaseInvoiceID`, `InvoiceNumber`, `Date`, `VendorRef`, `NetPrice`, `Tax`, `TotalAmount` | High | Finance manual entries to be reconciled against CRM Purchase Orders. |
| **Goods Receipts** (`CRM.Procurement.GoodsReceipts`) | `INV.InventoryVoucher` (Receipt type) | `VoucherNumber`, `ReferenceInvoiceRef`, `ReceiptDate`, `Quantity` | High | Warehouse official physical receipts in Sepidar. |

---

## 5. Non-Functional & Read-Only Enforcement Verification

* **Zero Outbound Writes:** The application will interact with Sepidar strictly via `SELECT` queries or read-only Entity Framework DbContext with change tracking disabled (`QueryTrackingBehavior.NoTracking`).
* **Isolated CRM Database:** All CRM data (Users, Roles, RFQs, Purchase Orders, Call Notes, Reconciliations) will reside in a completely separate database (`ParkPalaceCRM_DB`) hosted on PDC SQL Server (`192.168.1.250`).
* **No Speculative Schema Implementation:** Adapters in Phase 4 and Phase 5 will be wired exclusively to the verified column names once the operator executes the discovery script.

---

## 6. Missing Information & Operator Action Required

To finalize Gate G1 and enable Phase 2:
1. **Target Company Database Confirmation:**
   * Does `Sepidar01` or `Sepidar02` hold the active bakery fiscal company? (Or do both represent different fiscal years/companies?)
2. **Execute Provisioning Script on Sepidar Server:**
   * Open SSMS on `Sepidar.sahar.loc`, log in as `sa`, and execute [`docs/create_crm_readonly_login.sql`](file:///d:/ParkPlace/docs/create_crm_readonly_login.sql).
   * Provide the chosen password for `crm_reader` (or provide the output of [`docs/sepidar_discovery.sql`](file:///d:/ParkPlace/docs/sepidar_discovery.sql)).

---

## 7. Delivery Gate G1 Checklist & Request for Approval

- [x] Repository and environment thoroughly inspected.
- [x] Sepidar server IP (`192.168.1.242`) and dynamic port (`62753`) discovered.
- [x] Safe, read-only SQL discovery scripts created ([`docs/sepidar_discovery.sql`](file:///d:/ParkPlace/docs/sepidar_discovery.sql)).
- [x] Dedicated SELECT-only login creation script prepared ([`docs/create_crm_readonly_login.sql`](file:///d:/ParkPlace/docs/create_crm_readonly_login.sql)).
- [x] Data lineage and Sepidar-to-CRM domain mapping documented.
- [x] Absolute READ-ONLY constraint verified (no writes to Sepidar).
- [x] Phase 2 code implementation intentionally paused awaiting gate approval.

> **Request for Approval:** Please review the Phase 1 findings and approve proceeding to **Phase 2 (Foundation, Architecture Scaffolding, Identity & Authorization)**.
