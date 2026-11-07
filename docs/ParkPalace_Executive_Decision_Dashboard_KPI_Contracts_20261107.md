<div dir="rtl">

# قرارداد KPI — داشبورد تصمیم‌محور مدیریتی

هر KPI قبل از پیاده‌سازی ثبت می‌شود. هیچ عددی بدون قرارداد و منبع معتبر نمایش داده نمی‌شود.

وضعیت‌ها: `Ready` | `NeedsValidation` | `Blocked` | `Unavailable`

---

## ED-01 — سفارش‌های خرید معطل / سررسیدگذشته

| فیلد | مقدار |
|------|--------|
| تعریف | PO با وضعیت Issued یا PartiallyReceived که ExpectedDeliveryDateUtc از UtcNow گذشته |
| فرمول | Count / فهرست از `PurchaseOrders` |
| منبع | CRM DB — `PurchaseOrders` |
| فیلترها | بازه ایجاد/سررسید اختیاری؛ واحد سازمانی فقط اگر روی PO نگاشت شده باشد (فعلاً بدون OrgUnit) |
| تجمیع | سطح سفارش |
| به‌روزرسانی | زنده در درخواست |
| مجوز | `Reports.View` یا نقش Manager/Procurement/Warehouse یا `Reports.Procurement` |
| داده ناقص | اگر ExpectedDeliveryDateUtc تهی → در «معطل بدون سررسید» جدا می‌آید، نه Critical سررسید |
| وضعیت قرارداد | **Ready** |

## ED-02 — کسری مواد / موجودی بحرانی

| فیلد | مقدار |
|------|--------|
| تعریف | SKU با وضعیت Critical یا Low طبق `GetInventoryHealth` / مصرف مواد |
| فرمول | Count Critical + Low؛ فهرست Top N با QtyOnHand و SafetyStock |
| منبع | CRM Inventory / MaterialConsumption |
| مجوز | Reports.Warehouse یا نقش Warehouse/Manager |
| داده ناقص | SafetyStock تهی → وضعیت `NeedsValidation` روی ردیف؛ عدد صفر ساختگی نه |
| وضعیت | **Ready** (برچسب NeedsValidation روی ردیف‌های بدون آستانه) |

## ED-03 — مغایرت فاکتور / تطبیق سه‌طرفه باز

| فیلد | مقدار |
|------|--------|
| تعریف | استثناهای تطبیق با `IsResolved == false` |
| فرمول | Count + فهرست از FinanceReconciliation |
| منبع | CRM matching |
| مجوز | Reports.Finance یا نقش Finance/Manager |
| وضعیت | **Ready** |

## ED-04 — پیگیری‌های باز تماس

| فیلد | مقدار |
|------|--------|
| تعریف | Follow-upهای CRM با سررسید گذشته یا باز |
| فرمول | از TelephonyActivity / CallNotes |
| منبع | CRM |
| مجوز | Reports.View یا نقش‌های عملیاتی |
| وضعیت | **Ready** |

## ED-05 — طرف‌حساب نیازمند نگاشت / بازبینی

| فیلد | مقدار |
|------|--------|
| تعریف | Party بدون Sepidar mapping یا با هشدار شناسه تکراری |
| منبع | CRM Parties + financial dashboard needsReview |
| مجوز | Finance.PartyLedgerView برای جزئیات مالی؛ Reports برای لیست عملیاتی بدون رقم |
| وضعیت | **Ready** (عملیاتی) |

## ED-06 — مطالبات تجاری (Trade AR)

| فیلد | مقدار |
|------|--------|
| تعریف / فرمول | مطابق `ParkPalace_Financial_Dashboard_Metric_Contract.md` § مطالبات تجاری |
| منبع | Sepidar read-only `ACC.VoucherItem` + `ACC.DL` + Party mapping |
| فیلتر | سال مالی، از/تا، partyIds، search؛ **بدون** OrgUnit / PersonType روی رقم |
| تجمیع | سطح طرف‌حساب سپس جمع |
| به‌روزرسانی | زنده؛ `LastSuccessfulReadUtc` از sync |
| مجوز | **فقط** `Finance.PartyLedgerView` |
| داده ناقص | mapping ناقص → NeedsReview؛ بدون رقم جعلی |
| وضعیت | **NeedsValidation** (State ووچر فیلتر نشده؛ حساب‌های غیرتجاری ممکن است نشت کنند) |

## ED-07 — بدهی تجاری (Trade AP)

| فیلد | مانند ED-06 با علامت بدهی |
| وضعیت | **NeedsValidation** |

## ED-08 — مقایسه دوره مشابه سال قبل (AR/AP)

| فیلد | مقدار |
|------|--------|
| تعریف | همان فرمول روی بازه معادل در سال مالی قبلی (منطق `FinancialDashboardService`) |
| مجوز | PartyLedgerView |
| وضعیت | **NeedsValidation** (وابسته به ED-06/07) |

## ED-09 — روند ماهانه AR/AP

| فیلد | مانند FinancialDashboard MonthlyTrend |
| وضعیت | **NeedsValidation** |

## ED-10 — اشخاص با بیشترین مانده / گردش

| فیلد | TopDebtors / TopCreditors از FinancialDashboard |
| مجوز | PartyLedgerView |
| وضعیت | **NeedsValidation** |

## ED-11 — فروش پولی سپیدار

| وضعیت | **Blocked** — قرارداد امضاشده و نگاشت قطعی نوع سند فروش وجود ندارد |
| رفتار UI | کارت با وضعیت Blocked؛ بدون عدد |

## ED-12 — خرید پولی سپیدار

| وضعیت | **Blocked** |
| رفتار | بدون عدد؛ حجم سفارش خرید CRM به‌عنوان ED-01/حجم عملیاتی جداست و پولی سپیدار نیست |

## ED-13 — دریافت نقدی

| وضعیت | **Blocked** — جداول دریافت سپیدار تأیید نشده |

## ED-14 — پرداخت نقدی

| وضعیت | **Blocked** |

## ED-15 — مطالبات سررسیدگذشته سپیدار

| وضعیت | **Blocked** — ستون سررسید تأیید نشده |

## ED-16 — تازگی داده و سال مالی فعال

| تعریف | FiscalYear از FMK؛ LastSuccessfulReadUtc؛ Flags کامل‌بودن |
| منبع | FinancialDashboard meta + IntegrationHealth |
| مجوز | دیدن متا برای دارندگان Reports؛ ارقام مالی فقط با PartyLedgerView |
| وضعیت | **Ready** |

</div>
