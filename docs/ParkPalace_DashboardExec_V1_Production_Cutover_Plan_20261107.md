<div dir="rtl">

# طرح cutover Production — `dashboard-exec-v1` (برنامهٔ قابل بازبینی)

**تاریخ بازنگری:** ۱۴۰۵/۰۸/۱۶  
**وضعیت سند:** برنامهٔ قابل بازبینی — **اجرا نشده** — **چراغ سبز cutover نیست**  
**محدوده:** پیش‌بررسی فقط‌خواندنی + برنامه. Production تغییر نکرده. `deploy_allowed=false` مانده است.

| مدرک | مسیر |
|------|------|
| انگشت‌نگاری بسته / IIS / Sepidar CS | `docs/runs/prod_dashboard_exec_v1_cutover_preflight_readonly_20261107_122324.json` |
| تکمیل SHA و شکل تنظیمات | `docs/runs/prod_dashboard_exec_v1_cutover_preflight_supplement_20261107.json` |
| Health احرازشده + مجوزهای دقیق Admin | `docs/runs/prod_dashboard_exec_v1_auth_health_admin_perms_readonly_20261107_123939.json` |
| اسکریپت پیش‌بررسی RO | `scripts/Invoke-CrmDashboardExecProdCutoverPreflightReadOnly.ps1` |
| رانر DryRun بازبینی (+ `-IsolatedFailureDrill` برای منطق اجرایی ایزوله) | `scripts/crm_prod_cutover_dashboard_exec_v1.ps1` |
| آزمون آفلاین کمک‌تابع‌ها | `tests/Test-CrmProdCutoverDashboardExecV1RunnerOffline.ps1` → `ProdDashboardExecV1RunnerOfflineReviewPassed` |
| DryRun روی CRM (بدون mutate) | `docs/runs/prod_dashboard_exec_v1_cutover_runner_review_20261107_125111.json` |
| drill ایزوله موفق | `docs/runs/prod_dashboard_exec_v1_isolated_success_install_20261107_133012.json` |
| drill ایزوله شکست+بازگشت | `docs/runs/prod_dashboard_exec_v1_isolated_failure_rollback_20261107_133013.json` |
| DryRun تازه | `docs/runs/prod_dashboard_exec_v1_cutover_dryrun_fresh_20261107.json` |
| بستهٔ تصمیم | `docs/ParkPalace_DashboardExec_V1_Cutover_Decision_Package_20261107.md` |
| تصمیم جدا — ۳ مجوز شنود Admin | `docs/ParkPalace_Admin_Telephony_Sensitive_Permissions_Decision_20261107.md` |
| چک‌لیست روز اجرا + مرور رانر | `docs/ParkPalace_DashboardExec_V1_Production_Cutover_DayOf_Checklist_20261107.md` |

**وضعیت اجرا:** فرمان Execute آماده نیست و این طرح + سند مجوزهای تلفنی Admin + چک‌لیست روز اجرا نیز به‌تنهایی مجوز Execute نیستند.  
رانر پیش‌فرض = **DryRun برای بازبینی**. منطق اجرایی (BACKUP/ZIP/swap/preserve/rollback) در کد هست و با `-IsolatedFailureDrill` ایزوله آزموده می‌شود؛ `-ExecuteCutover` Production قفل (`AllowProductionExecute=$false`)؛ `deploy_allowed=false`.  
**یادآوری بازگشت:** آزمون آفلاین جای اجرای واقعی Rollback در پنجرهٔ انتشار را نمی‌گیرد (جزئیات در چک‌لیست روز اجرا §B5).  
**نقش‌ها:** سه پیوند `Calls.*` Admin دست‌نخورده؛ تعیین نقش/افراد مجاز شنود جداگانه (جدول باز در سند تصمیم تلفنی).

---

## ۰) سه اصلاح اجباری پیش از هر فرمان اجرا

### ۰.۱ مسیر Health

| مورد | مقدار |
|------|--------|
| در کد | `HealthController`: `[Route("api/health")]` و `[Route("health")]` — **نه** `api/v1/health` |
| مسیر Production درست | `https://crm.sahar.loc/api/health` |
| مسیر اشتباه قبلی در پیش‌نویس طرح | `/api/v1/health` → ناشناس ۴۰۱ / با هویت ۴۰۴ (شاهد همان JSON احرازشده) |

### ۰.۲ Health احرازشدهٔ Production (ثبت‌شده)

از روی CRM با `crm\administrator` و همچنین ناشناس روی مسیر درست:

| Url | Auth | Status | databaseConnected | sepidarIntegrationStatus |
|-----|------|--------|-------------------|--------------------------|
| `https://crm.sahar.loc/api/health` | Anonymous | **200** | `true` | **`Active (Read-Only)`** |
| `https://crm.sahar.loc/api/health` | DefaultCredentials | **200** | `true` | **`Active (Read-Only)`** |
| `https://127.0.0.1/api/health` | Anonymous / DefaultCredentials | **200** | `true` | **`Active (Read-Only)`** |
| `https://crm.sahar.loc/api/v1/health` | Anonymous | 401 | — | مسیر نامعتبر |
| `https://crm.sahar.loc/api/v1/health` | DefaultCredentials | 404 | — | مسیر نامعتبر |

بدنهٔ نمونه (احرازشده):  
`{"status":"Healthy","databaseConnected":true,"sepidarIntegrationStatus":"Active (Read-Only)",...}`

نتیجه: اتصال CRM DB و سپیدار زندهٔ فقط‌خواندنی روی Production **شاهد دارد**. پروب قبلی ۴۰۱ به‌خاطر مسیر غلط `/api/v1/health` بود، نه به‌خاطر الزام هویت روی `/api/health`.

### ۰.۳ سه پیوند حساس Admin — کد دقیق (نه تقریبی)

لیست مرجع اسکریپت تفکیک: `scripts/20261106_SeparateTechnicalAdminPermissions.sql` (۱۲ کد).  
شمارش SQL فقط‌خواندنی روی Production: `AdminPermTotal=43`، `AdminSensitiveInList=3`، `Finance.PartyLedgerView` روی نقش Finance=`1`.

**کدهای دقیقاً باقی‌مانده روی نقش Admin:**

| # | Code | Category | اثر روی داشبورد اجرایی / مالی | اثر روی شنود / نظارت تماس |
|---|------|----------|-------------------------------|---------------------------|
| 1 | `Calls.RecordingsListen` | Telephony | **هیچ** روی رقم سپیدار یا ED-06…15؛ `Finance.PartyLedgerView` نیست | **بله** — گیت استریم ضبط در `CallsController.StreamRecording` با `Permissions.CallsRecordingsListen`؛ بدون این مجوز شنود فایل قطع است |
| 2 | `Calls.Recordings.Listen` | Telephony | **هیچ** روی داشبورد مالی | در ثابت‌های فعلی کد `Permissions.CallsRecordingsListen = "Calls.RecordingsListen"` است؛ این کد نقطه‌دار در مسیر استریم فعلی چک نمی‌شود ولی در DB به‌عنوان پیوند RolePermission مانده (هم‌خانوادهٔ لیست تفکیک) |
| 3 | `Calls.Supervise` | Telephony | **هیچ** روی رقم سپیدار؛ دسترسی ops داشبورد از نقش Admin جداگانه در `ExecutiveDashboardAccess.CanViewOps` است | **بله** — در `TelephonyAccessControlService` برای `CanReviewUnattributedHistoricalCalls` (همراه `Org.ReportsView`)؛ همچنین گیت‌های نظارتی در `CallsController` |

**آنچه روی Admin نیست (مهم برای نقض‌نشدن تفکیک مالی):**

- هیچ‌یک از `Finance.View` / `Finance.Reconcile` / `Finance.Matching*` / `Invoices.*` / `Matching.*` / `Reports.Finance` روی Admin در لیست حساس نیستند.
- `Finance.PartyLedgerView` روی Admin **نیست**؛ روی Finance هست.
- چهار Admin فعال (`CRM\Administrator`, `SAHAR\Administrator`, `SAHAR\j.asg`, `SAHAR\n.asghari`) نقش Finance/Manager ندارند (`HasFinance=0`, `HasManager=0`).

**نتیجه برای تصمیم نقش‌ها:** تفکیک مالی Admin از رقم سپیدار فعلاً برقرار است. سه پیوند باقی‌مانده فقط **شنود/نظارت تلفنی** هستند و با تصمیم تفکیک Admin فنی از شنود سراسری در تعارض‌اند؛ **رفع آن‌ها خارج از بستهٔ dashboard-exec است** و نباید داخل cutover باینری قاطی شود مگر تصمیم صریح جدا.

---

## ۱) خلاصه برای بازبین

| مورد | مقدار |
|------|--------|
| بستهٔ پذیرفته‌شده | `dashboard-exec-v1` |
| API ZIP SHA256 | `3A2B24F486A914B1A9CAEA47CBCD21FD566342D3DDDBAE8FB4F0B71B4634AD12` |
| Web ZIP SHA256 | `E1EC83E2AEDC9868AF03CBC5C9517C91617455C406F22C694F07A3B63494856F` |
| DLL هدف پس از نصب | `64E20E1397B26026FBD5800050F55078D40EC33742ADDB69B7029A4CDB902134` |
| Production Api.dll امروز | `7E693A9BDD8BC449BBFA3D037694B86EF46B533BE0DC9C410A36198386B068E0` |
| SQL در این cutover | خیر (فقط API+Web) |
| `deploy_allowed` | false — flip ممنوع در این طرح |
| آیا این سند مجوز Execute است؟ | **خیر** |

---

## ۲) تفاوت Production ↔ بسته

| لایه | Production | بسته / staging | اقدام پیشنهادی (هنوز اجرا نشده) |
|------|------------|----------------|----------------------------------|
| API | `7E693A9B…` | DLL `64E20E13…` | جایگزینی باینری با حفظ appsettings |
| Web | `index.html` `700DCB…` | ZIP `E1EC83E2…` | جایگزینی static |
| Sepidar / Issabel | CS زنده + `IssabelARI` + Sftp + RecordingsPath | قالب staging نباید overwrite کند | حفظ اجباری |
| DB | EmpHist=1؛ DisplayName؛ Finance PLV | بسته SQL ندارد | بدون SQL |
| Admin listen/supervise | ۳ کد بالا | تغییر نمی‌کند با ZIP | تصمیم نقش جدا |

---

## ۳) اسکلت استقرار / بازگشت (پیشنویس برای طرح نهایی — غیر اجرایی)

### B. بکاپ تازه (روز اجرا، هنوز انجام نشود)

1. `BACKUP … WITH COPY_ONLY` → مسیر+SHA ثبت  
2. آینهٔ `api` و `web` شامل appsettings  
3. ثبت SHA فعلی Api.dll

### C. حفظ زنده

قبل از Start: `ConnectionStrings.SepidarDatabase` غیرخالی، `CrmDatabase` غیرخالی، `Telephony:Provider=IssabelARI`، Sftp Host و RecordingsPath حاضر. اگر Simulated/خالی → Start ممنوع و Rollback تنظیمات.

### D. استقرار پیشنهادی

Stop AppPool → کپی API/Web با exclude یا restore فوری appsettings → تأیید C → Start → Api.dll باید `64E20E13…`.

رانر پیش‌فرض = DryRun بازبینی؛ منطق اجرایی با `-IsolatedFailureDrill` روی درخت ایزوله؛ Production `-ExecuteCutover` قفل.

### E. آزمون سلامت پس از انتشار (فقط پس از Execute تأییدشده)

| # | آزمون | انتظار |
|---|--------|--------|
| E1 | `GET https://crm.sahar.loc/api/health` | 200؛ `databaseConnected=true`؛ `sepidarIntegrationStatus` شامل **Active (Read-Only)** زنده |
| E2 | `GET /api/me` (با هویت مجاز؛ `MeController` مسیر `api/v1/me` ندارد) | 200 |
| E3 | Admin → executive-dashboard | 200؛ بدون PartyLedgerView؛ مالی Blocked/بدون رقم |
| E4 | Admin → finance dashboard | 403 |
| E5 | هدر `FakeRoleNotAssigned` روی finance | 403 |
| E6 | Finance + PartyLedgerView | مجاز |
| E7 | ED-06…10 | **Unverified / نیازمند اعتبارسنجی** |
| E8 | ED-11…15 | Blocked |
| E9 | Drill ops | فیلتر واقعی |
| E10 | تماس/ضبط | رگرسیون آشکار نباشد؛ وضعیت ۳ مجوز شنود Admin از پیش دانسته است |

### F. Rollback

Stop → بازگردانی api/web از بکاپ فایل → Start → Api.dll دوباره `7E693A9B…` و `/api/health` با Sepidar Active RO.

---

## ۴) الزام «نیازمند اعتبارسنجی»

`ExecutiveDashboardService` رقم زندهٔ trade AR/AP را به `FinancialFigureStatus.Unverified` remap می‌کند. UI: «نیازمند اعتبارسنجی».  
رد cutover اگر ED-06/07 به‌صورت Verified بدون برچسب دیده شوند.

---

## ۵) خارج از محدوده / تصمیم‌های باز

| موضوع | وضعیت |
|--------|--------|
| Execute cutover | **باز — منتظر تصمیم شما پس از طرح نهایی** |
| `deploy_allowed=true` | ممنوع در این مسیر |
| حذف ۳ پیوند `Calls.*` از Admin | تصمیم جدا — سند `ParkPalace_Admin_Telephony_Sensitive_Permissions_Decision_20261107.md`؛ داخل بسته/رانر داشبورد نیست |
| SQL جدید Production | خارج از محدوده |

---

## ۶) جمع‌بندی آمادگی

| پیش‌نیاز بازبینی | وضعیت |
|-------------------|--------|
| مسیر Health درست در طرح | اصلاح شد → `/api/health` |
| شاهد Health + DB + Sepidar زنده | ثبت شد (Anonymous و DefaultCredentials، ۲۰۰، Active Read-Only) |
| کد دقیق ۳ مجوز Admin + اثر داشبورد/شنود | ثبت شد |
| Production mutate شده؟ | خیر |
| `deploy_allowed` | false |
| چراغ سبز Execute؟ | **خیر — این سند فقط برنامهٔ قابل بازبینی است** |

پس از تأیید شما روی همین یافته‌ها، می‌توان **طرح نهایی استقرار و بازگشت** (با چک‌لیست روز اجرا و معیار Gateهای post-smoke) را جداگانه برای تصمیم cutover ارائه کرد — بدون اجرای خودکار.

</div>
