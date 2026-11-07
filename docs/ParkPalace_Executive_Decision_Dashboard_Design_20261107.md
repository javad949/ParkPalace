<div dir="rtl">

# طراحی صفحه و قرارداد API — داشبورد تصمیم مدیریتی

**انتشار هدف:** جدا از `v1.0.24-rc3` (مثلاً شاخه/برچسب آینده `dashboard-exec-v1`). Production در این کار مستقر نمی‌شود.

## صفحه اصلی (یک نگاه)

سه لایه پاسخ به مدیر:

1. **وضعیت** — کارت‌های AR/AP با برچسب NeedsValidation فقط اگر `PartyLedgerView`؛ در غیر این صورت کارت‌های Blocked/Unavailable بدون رقم. کارت‌های فروش/خرید/دریافت/پرداخت/سررسید = Blocked.
2. **اقدام** — لیست هشدار قابل کلیک: PO معطل، کسری مواد، مغایرت فاکتور، پیگیری باز، نگاشت ناقص.
3. **تمرکز** — top parties (مالی) یا top تأمین‌کنندگان/مواد (عملیاتی) + لینک به جزئیات.

اسلایسرهای فعال اولیه: سال مالی، از/تا، جستجوی شخص، نوع شخص (فقط روی لیست review)، مسئول پیگیری. واحد سازمانی و گروه کالا روی ارقام سپیدار غیرفعال با tooltip «نگاشت معتبر نیست».

پاورقی هر کارت: منبع، وضعیت کامل‌بودن، زمان آخرین خواندن.

## API

### `GET /api/v1/reports/executive-dashboard`

Query (هم‌تراز `FinancialDashboardQuery` + فیلتر گزارش):

- `fiscalYearId`, `fromDate`, `toDate`, `search`, `partyType`, `ownerUserId`, `includeUnmapped`

پاسخ `ExecutiveDashboardDto`:

```text
GeneratedAtUtc
Viewer: { HasPartyLedgerView, Roles[], PermissionsNote }
Slicers: { FiscalYears[], ActiveFiscalYearId, Enabled[], Disabled[] with reason }
Freshness: { LastSuccessfulReadUtc, FiscalYearLabel, CompletenessFlags[] }
Ops: { ActionItems[], Metrics[] with contractId+status, Trends?, FocusLists? }
Finance: null | {
  Cards[] (TradeAR, TradeAP, YoY, Blocked stubs),
  MonthlyTrend[], TopDebtors[], TopCreditors[], NeedsReview[], FollowUps[]
}
DrillLinks: { partyLedger: "/finance/...", orders: "...", ... }
```

کنترل دسترسی در API:

- احراز هویت الزامی
- بدنه Ops اگر `Reports.View` یا نقش Manager/Finance/Procurement/Warehouse/OrganizationManager یا Reports.* مرتبط
- بخش `Finance` فقط اگر `Permissions.FinancePartyLedgerView`؛ در غیر این صورت `Finance = null` و کارت‌های Blocked stub در Ops برای شفافیت UI
- Admin فنی بدون PartyLedgerView رقم مالی نمی‌بیند

### `GET /api/v1/finance/dashboard/parties/{id}/ledger`

موجود؛ UI از کارت/نمودار به آن می‌رود (همان گیت).

## UI

- مسیر منو: «داشبورد تصمیم» — برای Manager، Finance، OrganizationManager، Admin
- Admin بدون ledger: بنر «برای ارقام سپیدار نقش/مجوز مالی صریح لازم است»
- بدون عدد ساختگی؛ وضعیت Blocked/NeedsValidation/NoData واضح

## تست‌های لازم (فاز ۴)

- فرمول AR/AP از سرویس موجود (بدون تغییر مخرب)
- اسلایسر سال مالی clamp
- YoY فقط وقتی سال قبل موجود
- Forbidden بدون PartyLedgerView
- Ops بدون Finance برای OrgManager
- کارت‌های Blocked بدون Value

</div>
