<div dir="rtl">

# ممیزی داشبورد تصمیم‌محور مدیریتی — ۱۴۰۵/۰۸/۱۶

**محدوده:** فقط خواندن کد و مستندات مخزن. Production مستقر نشد. مایگریشن Production اجرا نشد. `deploy_allowed=false` برای مسیر `release/v1.0.24-rc3` حفظ می‌شود. انتشار داشبورد جدا از rc3 است.

## شواهد کلیدی

| موضوع | فایل / شاهد |
|--------|-------------|
| داشبورد مالی زنده AR/AP | `FinancialDashboardController.cs`, `FinancialDashboardService.cs`, `SepidarLedgerReader.cs` |
| قرارداد معیار مالی | `docs/ParkPalace_Financial_Dashboard_Metric_Contract.md` |
| فرض‌های تأییدنشده سپیدار | `docs/sepidar_source_contract_and_unverified_assumptions.md` |
| گیت دفتر طرف‌حساب | `FinancialAccess.CanViewTotals` → فقط `Finance.PartyLedgerView` |
| داشبورد نقش عملیاتی CRM | `ReportsController.GetRoleDashboard`, `ReportingService.GetRoleDashboardAsync` |
| rc3 / deploy_allowed | `docs/ParkPalace_EmployeeIdentity_Deployment_Plan_20261106.md` |

## موجودی API قابل استفاده

### مالی سپیدار (خواندن‌صرف)

- `GET /api/v1/finance/dashboard` — مطالبات/بدهی تجاری، روند ماهانه، top parties، needsReview، follow-ups CRM
- `GET /api/v1/finance/dashboard/parties/{id}/ledger` — گردش/مانده با اسناد (در صورت موفقیت خواندن)
- دریافت/پرداخت نقدی و سررسید: عمداً **Blocked**
- وضعیت سند (`ACC.Voucher.State`) در جمع AR/AP فیلتر نمی‌شود → رقم زنده ولی **نیازمند اعتبارسنجی**

### عملیاتی CRM

- `GET /api/v1/reports/role-dashboard/{role}` — حجم سفارش، کسری مواد، مغایرت ۳طرفه، پیگیری تماس، سلامت یکپارچه‌سازی
- گزارش‌های جدا: procurement-summary، inventory-health، finance-reconciliation، telephony-activity

## اسلایسرها

| اسلایسر | وضعیت نگاشت | توضیح |
|---------|-------------|--------|
| سال مالی | فعال (سپیدار) | `FMK.FiscalYear` زنده |
| از/تا تاریخ | جزئی | روی پنجره سپیدار؛ clamp به سال مالی |
| نوع شخص (تأمین‌کننده/مشتری) | فقط CRM review | روی کارت‌های AR/AP اعمال نمی‌شود |
| جستجوی شخص | ترکیبی | CRM + SepidarPartyLookup |
| مسئول پیگیری | CRM | `CallNotes.FollowUpAssignedTo` |
| واحد سازمانی | **مسدود** | نگاشت معتبر به اسناد مالی سپیدار در کد فعلی نیست |
| گروه کالا | جزئی (CRM) | برای مصرف/موجودی؛ نه برای AR/AP سپیدار |
| وضعیت سفارش/مغایرت | CRM | برای هشدارهای عملیاتی |

## تفکیک نقش‌ها (کد فعلی)

| نقش | AR/AP سپیدار | عملیات CRM |
|-----|--------------|------------|
| Finance / Manager (+ PartyLedgerView) | بله | بله (با Reports.*) |
| Admin بدون PartyLedgerView | خیر (۴۰۳) | داشبورد Admin/یکپارچه‌سازی |
| OrganizationManager | خیر | Reports سازمانی؛ بدون Finance.* |

**نکته:** بخش دسترسی در `ParkPalace_Financial_Dashboard_Metric_Contract.md` نسبت به کد فعلی کهنه است؛ منبع حقیقت `FinancialAccess.cs` است.

## نتیجه ممیزی برای ساخت داشبورد تصمیم

**قابل اتکا برای اقدام (بدون رقم ساختگی):**

1. هشدار سفارش خرید معطل / سررسیدگذشته (CRM PO)
2. کسری مواد Critical/Low (CRM موجودی؛ برچسب NeedsValidation اگر SafetyStock ناقص)
3. مغایرت تطبیق سه‌طرفه باز (CRM)
4. پیگیری‌های باز تماس (CRM)
5. طرف‌حساب بدون نگاشت / شناسه تکراری (CRM)
6. سلامت یکپارچه‌سازی / آخرین خواندن موفق دفتر (در صورت وجود `IntegrationSyncRuns`)

**نمایش با برچسب «نیازمند اعتبارسنجی» (رقم زنده، بدون ادعای قطعی):**

7. مطالبات تجاری و بدهی تجاری سپیدار + مقایسه بازه معادل سال قبل + روند ماهانه + top parties

**مسدود (عدد نشان داده نمی‌شود):**

- فروش / خرید به‌عنوان KPI پولی سپیدار بدون قرارداد امضاشده
- دریافت و پرداخت نقدی
- مطالبات سررسیدگذشته سپیدار (ستون سررسید تأیید نشده)

</div>
