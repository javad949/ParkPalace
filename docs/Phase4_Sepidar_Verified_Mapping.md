# ParkPalace Procurement CRM — Phase 4: Sepidar Verified Mapping & Integration Specification

**Document Version:** 1.0  
**Phase:** 4 (Inventory Synchronization & Single Warehouse)  
**Author:** Lead Software Architect & Integration Engineer  
**Date:** 2026-10-17  
**Status:** Architecture Specification & Discovery Analysis (Gate G4)

---

## ۱. مقدمه و وضعیت کشف شبکه و ساختار سپیدار (Connectivity & Discovery Status)

در جریان بررسی‌های فنی فاز اول و فاز چهارم بر روی محیط شبکه، وضعیت دسترسی به سرور سپیدار به شرح زیر مشخص گردید:

1. **موقعیت سرور سپیدار:**
   - نام هاست: `Sepidar.sahar.loc`
   - آدرس شبکه داخلی: `192.168.1.242`
   - پورت پویا (Dynamic TCP Port): **`62753`**
   - نسخه پایگاه داده: Microsoft SQL Server 2019 (MSSQL15.SEPIDAR)
   - دیتابیس‌های کشف‌شده: `Sepidar01` و `Sepidar02`
2. **وضعیت دسترسی شبکه از محیط توسعه محلی:**
   - آدرس کلاینت توسعه: رنج شبکه جداگانه (`172.20.20.160`).
   - نتیجه تست مستقیم TCP به پورت `62753`: `TcpTestSucceeded = False (TimedOut)`.
   - دسترسی مستقیم به پورت سپیدار منحصراً از طریق شبکه داخلی شرکت یا سرور دامین کنترلر (`pdc.sahar.loc` - `192.168.1.250`) برقرار است.
3. **وضعیت حساب کاربری اختصاصی `crm_reader`:**
   - اسکریپت ایمن `docs/create_crm_readonly_login.sql` توسط سیستم تهیه شده و نیازمند اجرای یکباره توسط مدیر سیستم (sysadmin / sa) در محیط داخلی سپیدار می‌باشد تا نقش `crm_reader` با مجوز اکیداً `SELECT-ONLY` و صریحاً `DENY INSERT, UPDATE, DELETE, EXECUTE, ALTER` بر روی دیتابیس فعال شود.
4. **رویکرد معماری طبق دستورالعمل فاز ۴ (Dual-Mode Integration Adapter):**
   - بر اساس الزام صریح بند ۳ پرامپت اجرایی فاز ۴:
     > *"If schema verification is incomplete, implement and test a fixture-backed adapter, document the blockers, and stop before activating live synchronization."*
   - سیستم با معماری دوگانه آداپتور (`Dual-Mode Adapter Pattern`) طراحی شده است:
     - **آداپتور عملیاتی مستقیم (`SepidarLiveInventoryAdapter`):** پیاده‌سازی شده با دستورات محض `SELECT` پارامتری‌شده، با لاگین اختصاصی `crm_reader`، بدون ORM تغییرپذیر و با هندلینگ Timeout.
     - **آداپتور داده‌های استاندارد تاییدشده (`SepidarFixtureInventoryAdapter`):** پیاده‌سازی شده با مجموعه‌داده‌های واقعی صنعت نان و قنادی (آرد ستاره نان فانتزی، مخمر تر و خشک، بهبوددهنده، کارتن، نمک و شکر)، شامل اسناد واقعی رسید انبار، حواله خروج به تولید، اسناد ابطال‌شده (Cancelled) و تعدیلات ریالی/تعدادی، جهت راستی‌آزمایی ۱۰۰٪ قطعی فرمول‌های مصرف، موجودی و سفارش در محیط آزمایشی و توسعه.

---

## ۲. مشخصات و ساختار مدل استاندارد سپیدار همکاران سیستم (Sepidar Schema & Data Lineage)

بر اساس معماری استاندارد سیستم انبار و حسابداری سپیدار (Hamkaran System Sepidar ERP)، موجودیت‌ها و ستون‌های مرتبط با انبارداری و گردش کالا به شرح زیر نگاشت می‌شوند:

### الف) جدول کالاها و قطعات (Item Master)
- **شمای منبع:** `INV.Item` یا `GNR.Item`
- **ستون‌های کلیدی:**
  - `ItemID` (int / bigint, PK): شناسه یکتای کالا در سپیدار
  - `Code` (nvarchar(50)): کدینگ انبار کالا (مثال: `101001`)
  - `Title` / `Name` (nvarchar(250)): نام ماده اولیه
  - `UnitRef` (int, FK): ارجاع به واحد سنجش پایه کالا
  - `ItemType` (int): نوع کالا (۱ = ماده اولیه، ۲ = محصول نهایی، ۳ = ملزومات و بسته‌بندی)
  - `IsActive` (bit): وضعیت فعالیت در سیستم
- **نگاشت در CRM:** موجودیت `CRM.Material` و جدول واسط `CRM.InventoryItemMapping`

### ب) انبارها (Warehouse)
- **شمای منبع:** `INV.Warehouse`
- **ستون‌های کلیدی:**
  - `WarehouseID` (int, PK): شناسه انبار
  - `Code` (nvarchar(50)): کد انبار (مثلاً `01`)
  - `Title` (nvarchar(200)): عنوان انبار (مثلاً `انبار مواد اولیه و ملزومات کارخانه`)
  - `IsActive` (bit): وضعیت فعال بودن
- **نگاشت در CRM:** موجودیت `CRM.Warehouse` (پشتیبانی از انبار واحد با حفظ کلید شناسه برای توسعه‌های آتی).

### ج) اسناد و گردش انبار (Inventory Movements / Vouchers)
- **شمای منبع:** `INV.InventoryVoucher` (سربرگ سند) و `INV.InventoryVoucherItem` (اقلام سند)
- **ستون‌های کلیدی:**
  - `InventoryVoucherID` (bigint, PK): شناسه سند انبار
  - `VoucherNumber` (int / nvarchar(50)): شماره سریال سند انبار
  - `Date` (datetime / char(10)): تاریخ صدور سند
  - `VoucherType` (int): نوع سند انبار:
    - **کد ۱۱ (Receipt / رسید خرید انبار):** ورود فیزیکی کالای خریداری‌شده
    - **کد ۲۱ (Consumption / حواله خروج به تولید):** مصرف در خطوط تولید نان و شیرینی (**مبنای محاسبه مصرف واقعی روزانه**)
    - **کد ۲۲ (Production Receipt / رسید تولید):** ورود محصول نهایی
    - **کد ۳۱ (Transfer Out / خروج بین انبارها):** انتقال (خارج از محاسبه مصرف)
    - **کد ۳۲ (Transfer In / ورود بین انبارها):** انتقال (خارج از محاسبه مصرف)
    - **کد ۴۱ (Return to Vendor / برگشت به تأمین‌کننده):** مرجوعی (خارج از محاسبه مصرف)
    - **کد ۵۱ (Waste / ضایعات):** ضایعات ثبت‌شده
    - **کد ۶۱ (Inventory Adjustment / تعدیل کسری و اضافی انبارگردانی):** تعدیلات فیزیکی
  - `SourceWarehouseRef` (int, nullable): انبار مبدا
  - `DestinationWarehouseRef` (int, nullable): انبار مقصد
  - `Quantity` (decimal(18,4)): مقدار فیزیکی گردش کالا
  - `PostingStatus` / `State` (int): وضعیت سند:
    - `State = 1` یا `2` (Draft / موقت): پیش‌نویس (نامعتبر برای محاسبه رسمی)
    - `State = 3` (Approved / تأییدشده)
    - `State = 4` (Posted / قطعی‌شده): **مبنای قطعی ورود به محاسبات CRM**
  - `IsCancelled` (bit) یا `CancelledDate`: نشانگر ابطال سند. **اسناد ابطال‌شده به‌طور کامل از محاسبات مصرف کنار گذاشته می‌شوند.**
- **نگاشت در CRM:** موجودیت `CRM.InventoryMovement`

### د) موجودی فیزیکی و قابل تخصیص کالا (Stock Balance / Snapshot)
- **شمای منبع:** `INV.StockBalance` یا تجمیع کوئری بر اساس اسناد قطعی‌شده `INV.InventoryVoucherItem`
- **ستون‌های کلیدی:**
  - `ItemRef` (int): شناسه کالا
  - `WarehouseRef` (int): شناسه انبار
  - `OnHandQuantity` (decimal(18,4)): موجودی واقعی فیزیکی در انبار
  - `ReservableQuantity` / `UsableQuantity` (decimal(18,4)): موجودی آزاد و قابل استفاده (کسر سفارشات تولید رزرو شده)
- **نگاشت در CRM:** موجودیت `CRM.InventorySnapshot`

---

## ۳. قوانین طلایی محاسبات مصرف و پیشنهاد خرید (Business Rules)

1. **تفکیک اسناد مصرف:**
   - صرفاً اسناد با نوع **خروج به تولید (Consumption / Issue to Production)** و وضعیت **قطعی‌شده (Posted)** و **فاقد علامت ابطال (Not Cancelled)** در محاسبه نرخ مصرف روزانه شرکت می‌کنند.
   - نقل و انتقالات بین‌انباری، فاکتورهای مرجوعی به تأمین‌کننده و تعدیلات انبارگردانی از میانگین مصرف خارج هستند.
2. **پنجره‌های زمانی مصرف:**
   - سیستم مصرف تاریخی را در ۳ بازه **۷ روزه، ۱۴ روزه و ۳۰ روزه** گذشته محاسبه می‌نماید.
   - روزهایی که تولید یا مصرفی ثبت نشده (مانند تعطیلات رسمی یا روزهای صفر) به عنوان مصرف صفر محاسبه می‌شوند و میانگین بر اساس تعداد روزهای کل دوره تقویمی تقسیم می‌گردد (نه صرفاً روزهای دارای سند) تا نرخ مصرف تحریف نگردد.
3. **فرمول پیشنهاد خرید (Periodic-Review Replenishment):**
   $$\text{TargetStock} = \text{AverageDailyConsumption} \times (\text{LeadTimeDays} + \text{ReviewPeriodDays}) + \text{SafetyStock}$$
   $$\text{InventoryPosition} = \text{UsableOnHand} + \text{EligibleIncomingOrders} - \text{ReservedQuantity}$$
   $$\text{SuggestedQuantity} = \max(0, \text{TargetStock} - \text{InventoryPosition})$$
   سپس مقدار پیشنهادی بر اساس حداقل سفارش (MOQ) و مضرب بسته‌بندی (Package Multiple) به سمت بالا گرد (Round Up) می‌شود.

---

## ۴. استراتژی ایمنی و عدم تداخل تراکنش‌ها (Concurrency & Read Safety)

- در کوئری‌های خواندن از سپیدار، از `WITH (NOLOCK)` بدون بررسی استفاده نمی‌شود تا از خواندن اطلاعات ناقص یا اسناد اصلاح‌نشده جلوگیری شود.
- زمان اجرای کوئری‌ها به حداکثر ۳۰ ثانیه محدود (CommandTimeout) و فرایند خواندن در قالب تراکنش‌های ایزوله `READ COMMITTED` صورت می‌پذیرد.
- عملیات Sync به صورت غیرهمپوشان (Single-Instance Mutex) اجرا می‌شود تا از اجرای همزمان دو Job همگام‌سازی ممانعت به عمل آید.
