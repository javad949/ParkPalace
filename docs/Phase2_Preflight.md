# ParkPalace Procurement CRM — Phase 2 Preflight & Inventory

**Phase:** 2 (Foundation, Architecture, Identity, React Frontend, Tests)  
**Status:** Preflight Completed  
**Author:** Lead Architect & Implementation Engineer  

---

## 1. Development & Build Tools Inventory

| Tool / Runtime | Installed Version | Status / Notes |
| :--- | :--- | :--- |
| **.NET SDK** | .NET 8.0 LTS (`8.0.426`) | Active / Supported LTS. Targets `net8.0`. |
| **ASP.NET Core Runtimes** | `Microsoft.AspNetCore.App 8.0.32`, `10.0.11` | Present on host machine. |
| **Node.js** | `v24.11.1` | Installed, supports modern ESM, Vite, TypeScript. |
| **npm** | `11.6.2` | Clean CI builds (`npm ci`, `npm run build`). |
| **PowerShell** | `pwsh 7+` & Windows PowerShell 5.1 | Available for local and remote operations. |
| **Package Manager** | `winget v1.29.380` | Available. |

---

## 2. Infrastructure & Hosting State (Read-Only Verified)

| Component | Host / IP | Configuration State | Security & Deployment Rule |
| :--- | :--- | :--- | :--- |
| **Production Server (PDC)** | `pdc.sahar.loc` (`192.168.1.250`) | Windows Server 2022 Datacenter, Active Directory DC. IIS 10 with site `CRM` bound to Port `8888`. | **Constraint:** Do not deploy automatically to Domain Controller without separate explicit approval. Dedicated development/staging pipeline first. |
| **CRM Host Database** | `192.168.1.250` | SQL Server 2022 (RTM) 16.0.1000.6. Hosts system DBs and `Powerbi`. | Isolated database `ParkPalaceCRM_DB` to be provisioned solely for CRM data. |
| **Sepidar ERP Server** | `Sepidar.sahar.loc` (`192.168.1.242:62753`) | SQL Server 2019 (`SEPIDAR\SEPIDAR`). Databases `Sepidar01`, `Sepidar02`. | **STRICT READ-ONLY:** No migrations, no writes, no DDL, no mutating stored procedures. Adapter is configured disabled-by-default in Phase 2. |
| **VoIP Server (Issabel)** | `192.168.1.241` | Linux / Asterisk PBX. SSH port `49022`. | Untouched in Phase 2; scheduled for Phase 6. |

---

## 3. Solution & Repository Architecture

The repository will be structured as a clean, modular monolith:

```
ParkPlace/
├── src/
│   ├── ParkPalace.Domain/            # Pure entities, domain events, enums, value objects (no external dependencies)
│   ├── ParkPalace.Application/       # CQRS/Services, DTOs, interfaces, validation, authorization policies
│   ├── ParkPalace.Infrastructure/    # EF Core CRM DbContext, AD Windows Auth service, audit logging, Sepidar read-only placeholder
│   ├── ParkPalace.Api/               # ASP.NET Core Web API, controllers, middleware, OpenAPI/Swagger, health check
│   ├── ParkPalace.Worker/            # Background service host placeholder for scheduled tasks
│   └── parkpalace-web/               # React 18 + TypeScript + Vite, Tailwind/CSS RTL, Jalali dates
├── tests/
│   ├── ParkPalace.UnitTests/         # Domain & authorization policy tests
│   └── ParkPalace.IntegrationTests/  # WebApplicationFactory integration tests, health, me, security checks
├── docs/                             # Architecture, SRS, discovery and implementation reports
└── scripts/                          # DB provisioning, migration scripts, dev environment helpers
```

---

## 4. Key Phase 2 Deliverables & Safety Guards

1. **Database Isolation:** CRM database migrations strictly target `ParkPalaceCRM_DB`. No connection to Sepidar is opened during migrations or tests.
2. **Identity & Auth:**
   * In Production/IIS: Windows Negotiate Authentication (Kerberos/NTLM).
   * In Development/Testing: A dedicated `DevAuth` mock provider allowing configurable testing of roles without requiring active domain join.
3. **Role-Based Access Control (RBAC):**
   * Predefined roles: `Procurement`, `Manager`, `Warehouse`, `Finance`, `Admin`.
   * Enforced server-side via authorization policies; never trusting client claims.
4. **Zero-Secret Policy:** No plaintext passwords or connection strings with passwords in git or logs.
