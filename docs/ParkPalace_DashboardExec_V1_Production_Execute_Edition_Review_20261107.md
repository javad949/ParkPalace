<div dir="rtl">

# بازبینی نسخهٔ اجرایی Production — `dashboard-exec-v1`

**تاریخ:** ۱۴۰۵/۰۸/۱۶  
**وضعیت:** **Execute انجام شد و موفق بود** — Gate `ProdDashboardExecV1ProductionExecutePassed`  
**پنجره:** `2026-11-07-dashboard-exec`  
**`deploy_allowed`:** false می‌ماند  
**Admin Calls.*:** خارج از محدوده (بدون تغییر)

---

## نتیجهٔ Execute

| فیلد | مقدار |
|------|--------|
| Gate | **`ProdDashboardExecV1ProductionExecutePassed`** |
| Succeeded | true |
| RolledBack | false |
| ProductionMutated | true |
| ApiDll SHA پس از نصب | `64E20E1397B26026FBD5800050F55078D40EC33742ADDB69B7029A4CDB902134` |
| Health | `https://crm.sahar.loc/api/health` → 200، Sepidar Active (Read-Only)، بدون TLS bypass |
| Me | `https://crm.sahar.loc/api/me` → 200 |
| SQL COPY_ONLY bak | `D:\ParkPlaceCRM\backups\prod-dashboard-exec-v1\cutover_20261107_140033_3cad48d5\ParkPalaceCRM_COPY_ONLY_20261107_140033_3cad48d5.bak` |
| Bak SHA256 | `134323C31207832F798A1D317BA2B39281F454FC33A6A22B99778AC2C0CFF931` |

شواهد محلی: `docs/runs/prod_dashboard_exec_v1_production_execute_20261107_140104.json`  
شواهد روی CRM: `D:\ParkPlaceCRM\docs\runs\prod_dashboard_exec_v1_production_execute_20261107_140033.json`

---

## موانع common (پیش از Execute — بسته)

| # | مانع | شواهد |
|---|------|--------|
| 1 | BACKUP `, SKIP, STATS` | `ProdDashboardExecV1IsolatedSqlBackupPassed` |
| 2 | Health دامنه بدون TrustAll | `ProdDashboardExecV1CommonBlockersCleared` |
| 3 | Stop تأییدشده + RollbackVerified | IsolatedFailure با `RollbackVerified=true` |

---

## خارج از این cutover

- Flip کردن `deploy_allowed`
- حذف/تغییر سه مجوز `Calls.RecordingsListen` / `Calls.Recordings.Listen` / `Calls.Supervise` روی Admin

</div>
