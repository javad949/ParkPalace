<div dir="rtl">

# گزارش پیاده‌سازی داشبورد تصمیم مدیریتی — مسیر staging (اصلاح عنوان گیت)

## محدوده

- Production / سپیدار / ایزابل تغییر نکردند.
- `deploy_allowed=false` برای `dashboard-exec-v1` و مسیر `v1.0.24-rc3` حفظ شد.
- **Execute روی سایت ایزوله انجام شد** → `Gate=StageDashboardExecV1ExecutePassed` روی `ParkPalaceCRM-Stage-DashboardExec` / `:51025`. Production و Stage-v1024 دست‌نخورده.

## بسته

| فیلد | مقدار |
|------|--------|
| مسیر ساخت در مخزن | `release/dashboard-exec-v1/latest/` |
| مسیر مورد انتظار روی CRM | `D:\ParkPlaceCRM\packages\dashboard-exec-v1\latest\` |
| API SHA256 | `78AC0225BD250DDF2525CEC3E0C3741143EA03D7FFFEB414BFE6CE272E2BC013` (8263013 بایت) |
| Web SHA256 | `1A00681E28BDDF1F71A577F88409DB186C2EA3041206FF081CB4C675B70919DD` (1616369 بایت) |

قبل از Preview/Execute روی CRM، ZIPها و `manifest.json` را به مسیر CRM کپی کنید. وجود `D:\Antigravity\ParkPlace` روی سرور CRM فرض نشده است.

## عناوین Gate (مهم)

| Gate | معنی |
|------|------|
| `PackageVerifiedOnly` | فقط صحت ZIP/manifest روی همین ماشین؛ گیت‌های IIS/SQL/Health کامل نیست؛ `ExecuteEligible=False` |
| `StagingPreviewPassed` | بسته + هویت سایت staging + DB + Health روی **خود CRM** سبز؛ `ExecuteEligible=True` |
| `Pending` | شکست پیش‌نیاز یا شکست Execute |
| `StageDashboardExecV1ExecutePassed` | فقط پس از Execute واقعی موفق |

عنوان قدیمی `PreviewPassed` حذف شد تا موفقیت عملیاتی القا نشود.

## سایت staging (ایزولهٔ داشبورد)

سایت `ParkPalaceCRM-Stage-v1024` / `:51024` (مسیر sepidarsearch) **دست نخورده ماند**. برای dashboard-exec سایت جدا ساخته شد:

| مورد | مقدار |
|------|--------|
| IIS | `ParkPalaceCRM-Stage-DashboardExec` |
| AppPool | `ParkPalaceCRM-Stage-DashboardExec` |
| Health | `http://127.0.0.1:51025/api/health` |
| DB | `ParkPalaceCRM_Stage_20261106_123740_c98981ae` (همان staging DB؛ بدون سپیدار) |
| ریشه | `D:\ParkPlaceCRM\staging\dashboard_exec_v1\` |
| API (پس از Execute) | `...\dashboardexec_api_20261107_093539` |
| Web (پس از Execute) | `...\dashboardexec_web_20261107_093539` |
| State | `D:\ParkPlaceCRM\staging\dashboard_exec_v1\site_state.json` |

اسکریپت provisioning: `scripts/crm_provision_dashboard_exec_isolated_stage.ps1`  
ارکستراتور WinRM (بدون Execute): `scripts/Invoke-CrmDashboardExecIsolatedStagePreview.ps1`

رانر با `-StagingSiteName` / `-StagingApiBase` / `-StagingRoot` / `-ExpectedApiBaselinePath` / `-ExpectedBindingPort` به سایت ایزوله قفل می‌شود.

## آزمون آفلاین (انجام‌شده — ۱۴۰۵/۰۸/۱۶)

```text
فرمان:
powershell -NoProfile -ExecutionPolicy Bypass -File D:\Antigravity\ParkPlace\scripts\Test-CrmStageDashboardExecV1RunnerOffline.ps1

نتیجه: AST_OK؛ GATE_TITLES_OK؛ OFFLINE_TEST_PASSED
شواهد Preview:
docs/runs/stage_dashboard_exec_v1_runner_PreviewOnly_20261107_083946.json
Gate=PackageVerifiedOnly
ExecuteEligible=False
[FAIL] IIS / SQL / Health / Production fingerprint از این ماشین
وضعیت نصب staging: Pending
```

## دستورهای قابل‌کپی روی خود CRM

ابتدا بسته را کپی کنید، سپس اسکریپت رانر را هم روی CRM بگذارید (مثلاً `D:\ParkPlaceCRM\scripts\`).

### PreviewOnly (سایت ایزوله)

```powershell
$st = Get-Content D:\ParkPlaceCRM\staging\dashboard_exec_v1\site_state.json -Raw | ConvertFrom-Json
powershell -NoProfile -ExecutionPolicy Bypass -File D:\ParkPlaceCRM\scripts\crm_stage_dashboard_exec_v1_runner.ps1 `
  -Mode PreviewOnly `
  -PackageDir D:\ParkPlaceCRM\packages\dashboard-exec-v1\latest `
  -EvidenceDir D:\ParkPlaceCRM\docs\runs `
  -StagingSiteName $st.SiteName `
  -StagingApiBase $st.StagingApiBase `
  -StagingRoot $st.StagingRoot `
  -ExpectedApiBaselinePath $st.ExpectedApiBaselinePath `
  -ExpectedBindingPort $st.HttpPort
```

اگر خروجی `Gate=StagingPreviewPassed` و `ExecuteEligible=True` بود، سپس (فقط با تأیید صریح):

### Execute (هنوز اجرا نشود مگر درخواست صریح)

```powershell
$st = Get-Content D:\ParkPlaceCRM\staging\dashboard_exec_v1\site_state.json -Raw | ConvertFrom-Json
powershell -NoProfile -ExecutionPolicy Bypass -File D:\ParkPlaceCRM\scripts\crm_stage_dashboard_exec_v1_runner.ps1 `
  -Mode Execute `
  -PackageDir D:\ParkPlaceCRM\packages\dashboard-exec-v1\latest `
  -EvidenceDir D:\ParkPlaceCRM\docs\runs `
  -StagingSiteName $st.SiteName `
  -StagingApiBase $st.StagingApiBase `
  -StagingRoot $st.StagingRoot `
  -ExpectedApiBaselinePath $st.ExpectedApiBaselinePath `
  -ExpectedBindingPort $st.HttpPort
```

## Execute — ۱۴۰۵/۰۸/۱۶ (موفق روی سایت ایزوله)

شواهد: `docs/runs/crm_remote_execute_dashboard_exec_v1_20261107_093538.json`  
ریموت: `D:\ParkPlaceCRM\docs\runs\stage_dashboard_exec_v1_runner_Execute_20261107_093539.json`

| مورد | نتیجه |
|------|--------|
| Gate | `StageDashboardExecV1ExecutePassed` |
| API نصب‌شده | `...\dashboard_exec_v1\dashboardexec_api_20261107_093539` |
| Health | 200 + `databaseConnected=true` + Sepidar Fixture (نه Active RO) |
| Admin executive-dashboard | 200؛ بدون PartyLedgerView؛ ED-06 Blocked بدون رقم |
| Admin finance | 403 |
| Unassigned role header | 403 |
| Stage-v1024 | همچنان `sepidarsearch_api_20261106_200041` |
| Production Api.dll | بدون تغییر `7E693A9BDD8BC449BBFA3D037694B86EF46B533BE0DC9C410A36198386B068E0` |

پیش‌نیازهای رفع‌شده قبل از موفقیت Execute (فقط staging):

1. لاگین SQL برای `IIS APPPOOL\ParkPalaceCRM-Stage-DashboardExec` با `db_owner` روی DB استیجینگ (مثل Stage-v1024)
2. ستون additive `dbo.Roles.DisplayName` روی DB استیجینگ — اسکریپت `scripts/crm_stage_add_roles_displayname.sql` (Production از قبل PRESENT بود؛ mutate نشد)
3. گیت spoof: به‌جای `X-Active-Role:Finance` (حساب ops ممکن است Finance واقعی داشته باشد) از `FakeRoleNotAssigned` استفاده شد

Provision/Preview پیشین: `docs/runs/crm_remote_isolated_dashboard_stage_20261107_092604.json`

## تأیید پس از Execute — drill + RBAC + rollback (۱۴۰۵/۰۸/۱۶)

شواهد: `docs/runs/crm_remote_dashboard_verify_rollback_reinstall_20261107_094656.json`  
پروب UI/drill: `docs/runs/crm_remote_dashboard_ui_drill_probe_20261107.json`

| مورد | نتیجه |
|------|--------|
| Rollback به RC3-clone | Health=Healthy؛ Stage-v1024 دست‌نخورده |
| نصب مجدد بسته | `StageDashboardExecV1ExecutePassed` → `dashboardexec_api_20261107_094724` |
| Admin | کارت‌های مالی Blocked بدون رقم؛ `hasPartyLedgerView=false`؛ finance dashboard=403 |
| Finance + PartyLedgerView | executive/finance قابل دسترسی؛ ED-06/07 = Unavailable (سپیدار خاموش) بدون رقم زنده |
| Drill | کارت‌های ops دارای `drillTarget`+`drillQuery`؛ باندل وب شامل `consumeDrillForView` |
| تاریخ شمسی | اسلایسر `dateRange` فعال؛ برچسب سال مالی برای Finance=`1405` |
| مرورگر از این ورک‌استیشن | پورت `:51025` از خارج CRM unreachable (فقط WinRM)؛ باز شدن صفحه باید روی خود CRM یا از طریق مسیر شبکه داخلی انجام شود |

بستهٔ جدید: API SHA `3A2B24F4…4AD12` / Web SHA `E1EC83E2…4856F` (`release/dashboard-exec-v1/latest`).

## پذیرش staging — نهایی (۱۴۰۵/۰۸/۱۶)

وضعیت: **StagingAccepted (isolated site only)** — Production و `deploy_allowed=false` بدون تغییر.

| معیار پذیرش | نتیجه |
|-------------|--------|
| سایت ایزوله `:51025` / `ParkPalaceCRM-Stage-DashboardExec` | نصب dashboard-exec؛ Stage-v1024 و Production دست‌نخورده |
| کارت مسدود بدون رقم | Admin و کارت‌های Blocked با `—`؛ ED-06/07 در Fixture = Unavailable بدون رقم زنده |
| تاریخ شمسی + اسلایسر غیرفعال | تأیید در UI (بازه شمسی؛ واحد سازمانی/گروه کالا/وضعیت غیرفعال با دلیل) |
| Admin فنی | بدون PartyLedgerView؛ finance dashboard 403؛ رقم سپیدار دیده نمی‌شود |
| حساب مالی با مجوز | پیش‌نمایش نیلوفر باقری / Finance؛ دسترسی مجاز؛ سپیدار خاموش → Unavailable |
| Drill واقعی | کلیک ED-02 → انبار + فیلتر موجودی بحرانی (تأیید کاربر) |
| Rollback تمرین | بازگشت به RC3-clone سالم، سپس نصب مجدد موفق |

**خارج از محدودهٔ این پذیرش:** cutover Production، روشن‌کردن سپیدار زنده، تغییر `deploy_allowed`.

## ادغام مخزن GitHub — closeout (۱۴۰۵/۰۸/۱۶)

| فیلد | مقدار |
|------|--------|
| PR | https://github.com/javad949/ParkPalace/pull/1 |
| وضعیت | merged |
| SHA روی `master` | `5d11855de410a5f1d022ab2e73dafb3357dd6e60` |
| شاخهٔ منبع | `dashboard-exec-v1-staging-accept` |
| `deploy_allowed` | همچنان `false` |
| Production | دست‌نخورده |

مسیر پذیرش staging ایزولهٔ `dashboard-exec-v1` در مخزن بسته شد. گام بعدی عملیاتی فقط با درخواست صریح برای cutover / سپیدار زنده / تغییر پرچم بسته تعریف می‌شود.

## KPI / آزمون واحد (مرجع قبلی)

- واحد فیلترشده: 15 Passed (ExecutiveDashboard + FinancialDashboardRules)
- KPI Ready / NeedsValidation / Blocked بدون تغییر قرارداد

</div>
