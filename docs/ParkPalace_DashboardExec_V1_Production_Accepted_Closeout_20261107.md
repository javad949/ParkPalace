<div dir="rtl">

# Closeout پذیرش Production — `dashboard-exec-v1`

**تاریخ:** ۱۴۰۵/۰۸/۱۶  
**وضعیت:** **TrackClosed** — ProductionAccepted + E3–E10 + تلفن Admin (T2–T4 + مدیرعامل) + تأیید نهایی سبز  
**محدوده:** نصب API/Web بستهٔ پذیرفته‌شده روی Production؛ بدون flip `deploy_allowed`؛ بدون تغییر RolePermissions / سه مجوز تلفنی Admin

---

## خلاصه

| مورد | نتیجه |
|------|--------|
| Execute | **`ProdDashboardExecV1ProductionExecutePassed`** |
| پنجره | `2026-11-07-dashboard-exec` |
| Post-cutover verify | **`ProdDashboardExecV1ProductionPostCutoverVerified`** |
| Api.dll | `64E20E1397B26026FBD5800050F55078D40EC33742ADDB69B7029A4CDB902134` |
| Health / Me | 200 روی `crm.sahar.loc`؛ Sepidar Active (Read-Only) |
| Live config | Sepidar CS حاضر؛ Telephony=`IssabelARI` |
| `deploy_allowed` | **false** |
| Admin سه `Calls.*` | شمار=**3** (دست‌نخورده؛ کار جدا) |
| Finance `PartyLedgerView` | 1 |

---

## شواهد

| نوع | مسیر |
|-----|------|
| Execute (محلی) | `docs/runs/prod_dashboard_exec_v1_production_execute_20261107_140104.json` |
| Execute (CRM) | `D:\ParkPlaceCRM\docs\runs\prod_dashboard_exec_v1_production_execute_20261107_140033.json` |
| Post verify | `docs/runs/prod_dashboard_exec_v1_production_post_cutover_verify_20261107_140446.json` |
| SQL bak | `D:\ParkPlaceCRM\backups\prod-dashboard-exec-v1\cutover_20261107_140033_3cad48d5\ParkPalaceCRM_COPY_ONLY_20261107_140033_3cad48d5.bak` |
| Bak SHA256 | `134323C31207832F798A1D317BA2B39281F454FC33A6A22B99778AC2C0CFF931` |

---

## چک‌لیست روز اجرا — بخش C

| # | مورد | وضعیت |
|---|------|--------|
| C1 | Evidence Execute ثبت شد | ✅ |
| C2 | `deploy_allowed=false` | ✅ |
| C3 | Admin `Calls.*` دست‌نخورده | ✅ |
| C4 | Rollback | N/A (موفق) |

| Id | آزمون | وضعیت |
|----|--------|--------|
| E1 | Health | ✅ (رانر + post-verify) |
| E2 | Me | ✅ (رانر + post-verify) |
| E3 | Admin → executive-dashboard؛ بدون PartyLedgerView؛ کارت‌های مالی بدون رقم | ✅ |
| E4 | Admin → finance/dashboard → 403 | ✅ |
| E5 | هدر `FakeRoleNotAssigned` روی finance → 403 | ✅ |
| E6 | Finance + PartyLedgerView مجاز (dashboard + executive) | ✅ |
| E7 | ED-06…08 با وضعیت Unverified / نیازمند اعتبارسنجی | ✅ (ED-09/10 در پاسخ حاضر نبودند) |
| E8 | ED-11…15 Blocked و بدون رقم | ✅ |
| E9 | Drill ops (هدف روی کارت‌ها + `/api/v1/procurement/orders` و `/api/v1/calls`) | ✅ |
| E10 | فهرست تماس‌ها 200؛ سه مجوز شنود Admin همچنان شمار=3 | ✅ |

شواهد E3–E10: `docs/runs/prod_dashboard_exec_v1_production_e3e10_20261107_141845.json`  
Gate: **`ProdDashboardExecV1ProductionE3E10Passed`**  
برای E6–E8 نقش Finance فقط موقتاً روی `UserRoles` کاربر `CRM\Administrator` افزوده و در همان اجرا حذف شد؛ `RolePermissions` و سه `Calls.*` Admin دست نخورده؛ پس از پاکسازی نقش‌ها دوباره فقط `Admin`.

---

## پیگیری رانر (موفقیت Execute را نقض نمی‌کند؛ قبل از استفادهٔ دوباره الزامی)

| # | ایراد | وضعیت کد محلی |
|---|------|----------------|
| R1 | `BACKUP … INIT` می‌توانست فایل `.bak` موجود را بازنویسی کند | **اصلاح شد:** `Invoke-SqlCopyOnlyBackup` اگر مسیر هدف از قبل وجود داشته باشد پرتاب می‌کند |
| R2 | `Test-IsProductionLivePath` API و Web را غیرمتمایز قبول می‌کرد (جابه‌جایی مسیر ممکن بود) | **اصلاح شد:** `Test-IsProductionApiPath` / `Test-IsProductionWebPath` + `Assert-ProductionApiAndWebPaths` در نسخهٔ Production |

آزمون آفلاین: `tests/Test-CrmProdCutoverDashboardExecV1CommonFixes.ps1`

---

## مسیر تلفنی Admin (جدا — اکنون بستهٔ T2–T4)

سند: `docs/ParkPalace_Admin_Telephony_Sensitive_Permissions_Decision_20261107.md`  
Gate: **`ProdAdminTelephonyT2T3T4Passed`** — `docs/runs/prod_admin_telephony_t2t3t4_20261107_142613.json`

- `SAHAR\j.asg` ← نقش `Manager`  
- `SAHAR\n.asghari` (مدیر عامل) ← نقش `Manager`  
- سه `Calls.*` حساس از نقش **Admin** حذف شد  
- پس از حذف: Admin stream ضبط 403؛ Manager 200  

---

## تأیید نهایی مسیر (۱۴۰۵/۰۸/۱۶)

| مورد | نتیجه |
|------|--------|
| Api.dll | `64E20E13…` |
| Health | 200 + Sepidar Active RO |
| Admin سه `Calls.*` | 0 |
| Manager سه `Calls.*` | 3 |
| `j.asg` / `n.asghari` | `Admin,Manager` |

شواهد: `docs/runs/prod_dashboard_exec_v1_track_closed_20261107_143700.json`  
Gate: **`ProdDashboardExecV1TrackClosed`**

## برچسب Git

`dashboard-exec-v1-production-accepted`

</div>
