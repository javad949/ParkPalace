<div dir="rtl">

# چک‌لیست روز اجرا — cutover Production برای `dashboard-exec-v1`

**تاریخ:** ۱۴۰۵/۰۸/۱۶  
**وضعیت:** روز اجرا **انجام شد** — Execute + post-verify سبز (`ProductionAccepted`)  
**محدودهٔ انتشار:** فقط API/Web از بستهٔ پذیرفته‌شده. بدون SQL نقش. بدون تغییر سه مجوز شنود Admin.  
**Closeout:** `docs/ParkPalace_DashboardExec_V1_Production_Accepted_Closeout_20261107.md`
**`deploy_allowed`:** false می‌ماند.

مراجع:

| مدرک | مسیر |
|------|------|
| طرح cutover | `docs/ParkPalace_DashboardExec_V1_Production_Cutover_Plan_20261107.md` |
| رانر DryRun برای بازبینی (+ منطق اجرایی فقط در `-IsolatedFailureDrill`) | `scripts/crm_prod_cutover_dashboard_exec_v1.ps1` |
| تصمیم تلفنی Admin (جدا) | `docs/ParkPalace_Admin_Telephony_Sensitive_Permissions_Decision_20261107.md` |
| DryRun CRM | `docs/runs/prod_dashboard_exec_v1_cutover_runner_review_20261107_125111.json` |
| آزمون آفلاین کمک‌تابع‌ها | `tests/Test-CrmProdCutoverDashboardExecV1RunnerOffline.ps1` |
| drill ایزوله نصب موفق | `docs/runs/prod_dashboard_exec_v1_isolated_success_install_20261107_133012.json` → `ProdDashboardExecV1IsolatedSuccessInstallPassed` |
| drill ایزوله شکست+بازگشت | `docs/runs/prod_dashboard_exec_v1_isolated_failure_rollback_20261107_133013.json` → `ProdDashboardExecV1IsolatedFailureRollbackPassed` |
| DryRun تازه CRM | `docs/runs/prod_dashboard_exec_v1_cutover_dryrun_fresh_20261107.json` → `ProdDashboardExecV1CutoverDryRunReviewReady` |
| بستهٔ تصمیم (بدون Execute) | `docs/ParkPalace_DashboardExec_V1_Cutover_Decision_Package_20261107.md` |

**یادآوری:** آزمون بازگشت آفلاین جای تمرین/اجرای واقعی بازگشت در پنجرهٔ انتشار را نمی‌گیرد.

---

## A. پیش از پنجره (همان روز، قبل از Stop)

| # | مورد | معیار | ☐ |
|---|------|--------|---|
| A1 | بسته روی CRM = SHA پذیرفته‌شده | API ZIP `3A2B24F4…4AD12` / Web ZIP `E1EC83E2…4856F` | ☐ |
| A2 | `manifest.deploy_allowed` | `false` | ☐ |
| A3 | DryRun تازه روی CRM | Gate `ProdDashboardExecV1CutoverDryRunReviewReady`؛ `ExecuteReady=false` | ☐ |
| A3b | drill ایزوله موفق + شکست/بازگشت | `IsolatedSuccessInstallPassed` و `IsolatedFailureRollbackPassed` | ☐ |
| A3c | قفل ساختاری Production | `-ExecuteCutover` همیشه رد؛ بدون مسیر unlock با flag | ☐ |
| A4 | انگشت‌نگاری Api.dll فعلی | هنوز `7E693A9B…` (یا مقدار تازهٔ ثبت‌شدهٔ روز اجرا) | ☐ |
| A5 | Sepidar / Issabel زنده | CS Sepidar حاضر؛ Provider=`IssabelARI`؛ Sftp+RecordingsPath حاضر | ☐ |
| A6 | Health پایه | `GET https://crm.sahar.loc/api/health` → ۲۰۰؛ DB true؛ `Active (Read-Only)` | ☐ |
| A7 | Me پایه | `GET https://crm.sahar.loc/api/me` با هویت مجاز → ۲۰۰ | ☐ |
| A8 | تأیید محدوده | بدون RolePermissions؛ بدون حذف `Calls.*` Admin؛ بدون flip `deploy_allowed` | ☐ |
| A9 | مالک سیستم | تأیید کتبی پنجره + همین چک‌لیست (این ☐ مجوز Execute است، نه سند طرح به‌تنهایی) | ☐ |
| A10 | قفل رانر | تا تأیید A9: `AllowProductionExecute` باید `$false` بماند | ☐ |

---

## B. مرور ترتیب استقرار (چک‌لیست + منطق کد)

**اصطلاح:** تا وقتی Production Execute قفل است، رانر پیش‌فرض = **DryRun برای بازبینی** است نه «رانر نهایی اجرایی Production».  
منطق واقعی BACKUP/ZIP/swap/rollback در کد پیاده شده و فقط با `-IsolatedFailureDrill` روی درخت ایزوله آزموده می‌شود. `-ExecuteCutover` روی Production همچنان رد می‌شود.

مسیر هدف پس از باز شدن قفل Production (امروز اجرا نشود):

### B1. بکاپ یکتا

رانر با `stamp` + `token` هشت‌کاراکتری می‌سازد، مثلاً:

`D:\ParkPlaceCRM\backups\prod-dashboard-exec-v1\cutover_<stamp>_<token>\`

| خروجی | نقش |
|--------|-----|
| `ParkPalaceCRM_COPY_ONLY_<stamp>_<token>.bak` | بکاپ SQL `COPY_ONLY` |
| `files_api` / `files_web` | آینهٔ درخت قبل از swap |
| `config_snapshot` | کپی `appsettings*.json` زنده |
| `PriorApiRename` / `PriorWebRename` | نام پوشهٔ قبلی برای Rollback |

بدون مسیر بکاپ ثبت‌شده و SHA → ادامه ممنوع.

### B2. حفظ تنظیمات زنده

1. قبل از کپی: snapshot طول/حضور Sepidar CS و Provider تلفن.  
2. استخراج ZIP به مسیر موقت یا swap پوشه.  
3. بازنویسی `appsettings.json` و `appsettings.Production.json` از snapshot زنده روی خروجی بسته.  
4. `Assert-LiveConfigPreserved` — اگر Sepidar خالی، Telephony=`Simulated`، یا طول CS عوض شده → **Start ممنوع** و ورود به Rollback.

### B3. استقرار

1. `Stop-WebAppPool ParkPalaceCRM`  
2. Rename زنده → `Prior*`  
3. قرار دادن محتوای بسته در `D:\ParkPlaceCRM\api` و `...\web`  
4. اعمال B2  
5. `Start-WebAppPool`  
6. Api.dll باید `64E20E13…` شود

### B4. Smoke پس از نصب (حداقلی در رانر + تکمیل دستی)

| Id | مسیر | انتظار |
|----|------|--------|
| E1 | `GET /api/health` | ۲۰۰؛ `databaseConnected=true`؛ `Active (Read-Only)` |
| E2 | `GET /api/me` | ۲۰۰ با هویت مجاز |
| E3–E10 | طبق طرح | ✅ ثبت شد — `prod_dashboard_exec_v1_production_e3e10_20261107_141845.json` / Gate `ProdDashboardExecV1ProductionE3E10Passed` |

شکست E1/E2 یا DLL یا preserve → Rollback خودکار بلافاصله.

### B5. بازگشت (باید در پنجرهٔ واقعی قابل اجرا باشد)

آفلاین فقط منطق rename را ثابت کرده؛ در پنجره:

1. Stop AppPool  
2. `Invoke-FileTreeRollback` برای api و web از `Prior*`  
3. در صورت نیاز restore از `config_snapshot`  
4. Start  
5. تأیید Api.dll دوباره مقدار پیش از cutover + `/api/health` با Sepidar Active RO  

☐ تمرین کوتاه Rollback در پنجره (حتی dry rename روی کپی غیرlive در صورت امکان) قبل از اعلام موفقیت — **جایگزین آزمون آفلاین نیست؛ مکمل آن است.**

---

## C. پس از موفقیت / پس از Rollback

| # | مورد | ☐ |
|---|------|---|
| C1 | Evidence JSON روز اجرا با Gate مناسب ثبت شد | ✅ |
| C2 | `deploy_allowed` همچنان false | ✅ |
| C3 | RolePermissions Admin (سه `Calls.*`) دست‌نخورده | ✅ |
| C4 | اگر Rollback شد: علت و SHA نهایی ثبت شد | N/A (موفق) |

---

## D. خارج از این چک‌لیست — مجوزهای تلفنی (باز)

طبق سند تصمیم امنیتی؛ **در روز cutover داشبورد انجام نمی‌شود:**

| # | کار باز |
|---|---------|
| T1 | تعیین **نقش** مجاز به شنود/نظارت |
| T2 | تعیین **افراد** دارای آن نقش |
| T3 | اعطا + آزمون شنود ضبط و نظارت |
| T4 | فقط پس از T3 سبز: حذف سه پیوند از Admin |

---

## E. معیار «آمادهٔ تصمیم Execute» (هنوز نرسیده‌ایم)

| شرط | امروز |
|------|--------|
| طرح + چک‌لیست روز اجرا بازبینی‌شده | این سند |
| رانر با بکاپ/preserve/smoke/rollback مرور شده | بله (قفل) |
| A9 تأیید انسانی پنجره | باز |
| `AllowProductionExecute=$true` در بازنگری تأییدشدهٔ رانر | خیر |
| فرمان `-ExecuteCutover` | رد می‌شود |

تا وقتی ردیف‌های باز بالا سبز نشده‌اند، وضعیت = برنامه‌ریزی / DryRun؛ Production بدون تغییر.

</div>
