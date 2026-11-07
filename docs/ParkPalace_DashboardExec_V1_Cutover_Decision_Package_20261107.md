<div dir="rtl">

# بستهٔ تصمیم cutover — `dashboard-exec-v1` (بدون فرمان اجرا)

**تاریخ:** ۱۴۰۵/۰۸/۱۶  
**وضعیت:** **ProductionAccepted** — Execute موفق؛ post-verify سبز  
**`deploy_allowed`:** false  
**سه مجوز تلفنی Admin:** خارج از این انتشار (→ سند تصمیم تلفنی)  
**Closeout:** `docs/ParkPalace_DashboardExec_V1_Production_Accepted_Closeout_20261107.md`

---

## ۱) آنچه اثبات شده

| مورد | Gate / نتیجه |
|------|----------------|
| منطق اجرایی ایزوله — نصب موفق | `ProdDashboardExecV1IsolatedSuccessInstallPassed` — `docs/runs/prod_dashboard_exec_v1_isolated_success_install_20261107_133012.json` |
| منطق اجرایی ایزوله — شکست + بازگشت | `ProdDashboardExecV1IsolatedFailureRollbackPassed` — `docs/runs/prod_dashboard_exec_v1_isolated_failure_rollback_20261107_133013.json` |
| آزمون آفلاین کمک‌تابع‌ها | `ProdDashboardExecV1RunnerOfflineReviewPassed` |
| DryRun تازه روی CRM | `docs/runs/prod_dashboard_exec_v1_cutover_dryrun_fresh_20261107.json` |
| Health / Me در طرح | `/api/health` ، `/api/me` |
| Production mutate؟ | خیر |

رانر: `scripts/crm_prod_cutover_dashboard_exec_v1.ps1`  
شامل بکاپ یکتا، آینه فایل، استخراج ZIP، جابه‌جایی، حفظ appsettings زنده، smoke، Rollback (`robocopy /MIR`).

---

## ۲) قفل Production (دورزدنی با یک پرچم نیست)

در این بازنگری اسکریپت:

- `-ExecuteCutover` **همیشه** رد می‌شود (`ProdDashboardExecV1CutoverExecuteRefused_ProductionLocked`).
- `ProductionExecuteAvailableInThisRevision = false` — مسیر اجرای Production در همین فایل پیاده/باز نیست.
- مسیرهای اجرایی واقعی فقط `-IsolatedSuccessDrill` و `-IsolatedFailureDrill` هستند.
- باز کردن Production نیازمند **تغییر بازبینی‌شدهٔ جدا** (مثلاً PR) است که صریحاً مسیر Production را اضافه کند — نه تغییر یک boolean.

---

## ۳) اسناد هم‌خوان

| سند | نقش |
|------|-----|
| `ParkPalace_DashboardExec_V1_Production_Cutover_Plan_20261107.md` | طرح |
| `ParkPalace_DashboardExec_V1_Production_Cutover_DayOf_Checklist_20261107.md` | چک‌لیست روز اجرا |
| `ParkPalace_Admin_Telephony_Sensitive_Permissions_Decision_20261107.md` | مسیر جدا برای ۳ مجوز `Calls.*` |

---

## ۴) نسخهٔ اجرایی Production (بازبینی‌شده — اجرا نشده)

| مورد | نتیجه |
|------|--------|
| اسکریپت | `scripts/crm_prod_cutover_dashboard_exec_v1_production.ps1` |
| Preflight CRM | `ProdDashboardExecV1ProductionPreflightReady` — `docs/runs/prod_dashboard_exec_v1_production_preflight_review_20261107.json` |
| Execute | **انجام نشده** |
| جزئیات + فرمان | `docs/ParkPalace_DashboardExec_V1_Production_Execute_Edition_Review_20261107.md` |

## ۵) تصمیم‌های باز برای مالک سیستم

1. **آیا فرمان Execute نسخهٔ Production (سند بازبینی بالا) را تأیید می‌کنید؟**  
2. **نقش/افراد شنود** — جدا؛ قبل از حذف پیوندهای Admin.  
3. **`deploy_allowed`** — پیشنهادی: false بماند.

تا تأیید بند ۱، هیچ استقرار Production اجرا نمی‌شود.

</div>