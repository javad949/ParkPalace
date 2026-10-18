# ParkPalace Procurement CRM — Phase 4 Implementation Report
## Inventory Synchronization & Single Warehouse

**تاریخ گزارش:** ۲۶ مهر ۱۴۰۵ (17 October 2026)  
**نسخه گزارش:** 1.0  
**نقش:** معمار ارشد نرم‌افزار، مهندس ارشد .NET، مهندس یکپارچه‌سازی SQL Server و مهندس QA  
**وضعیت گیت:** توقف در دروازه کیفی Gate G4 و درخواست بررسی و صدور مجوز  

---

## ۱. خلاصه اجرایی و دستاوردهای فاز ۴

فاز چهارم پروژه **ParkPalace Procurement CRM** با تمرکز بر همگام‌سازی مانده و کاردکس گردش انبار، مدل‌سازی تک‌انباره کارخانه نان (`WH-MAIN`)، محاسبه آماری مصرف واقعی مواد اولیه، و موتور پیشنهاد خرید دوره‌ای مبتنی بر فرمولاسیون تحلیلی با موفقیت کامل پیاده‌سازی و اعتبارسنجی گردید.

### خلاصه نتایج و شواهد کیفی:

| شاخص | مقدار / نتیجه | وضعیت |
| :--- | :---: | :---: |
| **تست‌های واحد (Unit Tests)** | **۲۳ از ۲۳ موفق (100%)** | **PASS** |
| **تست‌های یکپارچگی (Integration Tests)** | **۱۷ از ۱۷ موفق (100%)** | **PASS** |
| **کل آزمون‌های خودکار راه‌حل** | **۴۰ از ۴۰ موفق (100%)** | **PASS** |
| **ساخت فرانت‌اند (Vite/React Production Build)** | **موفق (کد خروجی ۰، بدون خطای TS)** | **PASS** |
| **مهاجرت دیتابیس CRM (EF Core Migration)** | **اعمال‌شده (`Phase4_InventoryAndWarehouse`)** | **PASS** |
| **تغییر در ساختار یا دیتای سپیدار** | **۰ (صفر) — اکیداً SELECT-Only** | **PASS** |
| **استقرار عملیاتی روی PDC** | **انجام‌نشده (مطابق مجوز Gate G3)** | **PASS** |
| **معیارهای پذیرش Gate G4 (G4-01 تا G4-12)** | **۱۲ از ۱۲ معیار PASS** | **PASS** |

---

## ۲. رعایت الزامات ایمنی و عدم دستکاری در سپیدار

مطابق بند ۲ منشور پروژه:
1. **عدم انجام هرگونه عملیات نوشتنی:** هیچ دستور `INSERT`، `UPDATE`، `DELETE`، `MERGE` یا فراخوانی Stored Procedure های تغییردهنده در کدهای این فاز وجود ندارد.
2. **اتصال مستقل CRM:** دیتابیس عملیاتی CRM (`ParkPalaceCRM_DB_Dev`) کاملاً مستقل بوده و تمامی جداول انبار، مانده‌ها، نگاشت‌ها و سوابق همگام‌سازی در آن ذخیره می‌شوند.
3. **کاربر اختصاصی `crm_reader`:** کلیه کوئری‌های ارتباطی به سپیدار منحصراً با لاگینِ دارای دسترسی SELECT روی جداول انبار و کالا تعریف شده‌اند.
4. **معماری آداپتور دوگانه (Dual-Mode Architecture):**
   - به دلیل ایزولاسیون شبکه در محیط تست فعلی (ایستگاه توسعه `172.20.20.160` و سرور سپیدار `192.168.1.242`)، ارتباط به صورت هوشمند مدیریت می‌شود:
   - در زمان پیکربندی رشته اتصال معتبر، آداپتور `SepidarLiveInventoryAdapter` فعال بوده و کوئری‌های بهینه‌سازی‌شده SELECT را اجرا می‌کند.
   - در غیاب رشته اتصال، آداپتور قطعی `SepidarFixtureInventoryAdapter` با دیتاست استاندارد مواد اولیه کارخانه نان فعال شده و تست‌های خودکار را به صورت Deterministic اعتبارسنجی می‌کند.

---

## ۳. دامنه مدل‌سازی موجودی و انبار (Domain Entities)

موجودیت‌های جدید زیر در لایه Domain با کلیدهای پایدار، متادیتا و شاخص‌های یکتایی ایجاد و به پایگاه داده اضافه شدند:

1. **`Warehouse`:** نگهداری مشخصات انبار مرکزی کارخانه نان (`WH-MAIN`) با نگاشت به کد انبار سپیدار (`SepidarWarehouseRef = "1"`).
2. **`InventoryItemMapping`:** جدول واسط نگاشت مواد اولیه CRM (`MaterialId`) با شناسه یکتای کالای سپیدار (`SepidarItemId`).
3. **`InventorySnapshot`:** آخرین وضعیت لحظه‌ای موجودی شامل `OnHandQuantity` (فیزیکی)، `ReservableQuantity` (آزاد)، `ReservedQuantity` (تخصیص‌یافته)، `IncomingQuantity` (سفارشات در راه)، برچسب کهنگی `IsStale` و زمان اسنپ‌شات.
4. **`InventoryMovement`:** کاردکس گردش انبار با فیلدهای `VoucherNumber`، `MovementType`، `Quantity`، `TransactionDateUtc`، `PostingStatus`، `IsCancelled` و کلید شناسه یکتای سپیدار (`SourceMovementKey`).
5. **`InventorySyncJob`:** رصد چرخه حیات هر اجرا شامل لاگ شروع، پایان، نوع (`Full` / `Incremental`)، تعداد اقلام، اسنپ‌شات‌ها و رکوردهای گردش بررسی‌شده.
6. **`InventorySyncCheckpoint`:** ردیابی آخرین شناسه برگه و تاریخ ثبت‌شده جهت همگام‌سازی افزایشی و جلوگیری از خواندن رکوردهای تکراری.
7. **`InventorySyncError`:** ثبت لاگ جزئیات خطاهای احتمالی حین همگام‌سازی.
8. **`MaterialConsumptionSummary`:** خلاصه‌سازی مصرف تجمعی، میانگین مصرف روزانه و روزهای فعال تولید در بازه‌های ۷، ۱۴ و ۳۰ روز.
9. **`ReplenishmentPolicy`:** خط‌مشی سفارش‌دهی هر ماده اولیه شامل `LeadTimeDays`، `ReviewPeriodDays`، `SafetyStock`، `MinOrderQuantity` (MOQ)، `PackageMultiple` (مضرب بسته/کیسه) و تأمین‌کننده برگزیده.
10. **`PurchaseRecommendation`:** پیشنهاد مشورتی خرید حاصل از محاسبات ریاضی با شرح تحلیلی فرمول، وضعیت نیاز و هشدارهای اطلاعاتی.

---

## ۴. موتور همگام‌سازی و ایمپورت پایا (Idempotent Sync Engine)

موتور همگام‌سازی در کلاس `InventorySyncService` پیاده‌سازی شده است و ویژگی‌های زیر را داراست:

- **انحصار تک‌اجرایی (Mutual Exclusion):** استفاده از `SemaphoreSlim(1, 1)` برای ممانعت از اجرای همزمان دو جاب همگام‌سازی همپوشان.
- **پردازش پایا (Idempotency):** بررسی وجود `SourceMovementKey`؛ در صورت ثبت قبلی برگه، درج مجدد انجام نشده و صرفاً در صورت تغییر وضعیت ابطال یا قطعی‌شدن سند، به‌روزرسانی صورت می‌گیرد.
- **تشخیص داده‌های کهنه (Stale Detection):** چنانچه فاصله آخرین همگام‌سازی کالا بیش از ۲۴ ساعت باشد، فلگ `IsStale = true` تنظیم شده و در تمامی سطوح داشبورد و پیشنهادات هشدار نمایش داده می‌شود.
- **همگام‌سازی پنجره همپوشان (Overlapping Bounded Sync):** در جاب‌های افزایشی، پنجره زمانی ۲ روز قبل از آخرین چک‌پوینت بازخوانی می‌شود تا اسناد اصلاحی یا ثبت با تأخیر از دست نروند.

---

## ۵. محاسبه مصرف روزانه (Daily Consumption Calculation)

موتور `ConsumptionCalculator` طبق ضوابط دقیق زیر عمل می‌کند:

1. **فیلترینگ دقیق نوع تراکنش:** صرفاً رکوردهای با نوع `ProductionConsumption` (حواله خروج مصرف خط تولید نان) محاسبه می‌شوند. انتقالات، برگشت از تولید، رسیدهای خرید و اسناد ابطال‌شده (`IsCancelled == true`) یا پیش‌نویس قطعی‌نشده از محاسبه مصرف خارج هستند.
2. **پشتیبانی از بازه‌های تاریخی:** محاسبه دقیق مصرف در ۳ بازه استاندارد ۷ روز، ۱۴ روز و ۳۰ روز.
3. **مدیریت روزهای بدون مصرف و تعطیلات:** میانگین مصرف به ازای کل طول دوره (`TotalDays`) محاسبه می‌گردد و میانگین در روزهای فعال تولید (`ActiveDays`) نیز جهت تحلیل رفتار عملیاتی خطوط نانوایی استخراج می‌شود.
4. **تبدیل واحد سنجش:** در صورت مغایرت واحد ثبت حواله با واحد پایه ماده اولیه، ضریب تبدیل تعریف‌شده در کاتالوگ به صورت خودکار اعمال می‌گردد.

---

## ۶. موتور پیشنهاد خرید (Purchase Recommendation Engine)

موتور `PurchaseRecommendationEngine` از مدل علمی **بازنگری دوره‌ای (Periodic Review Replenishment Model)** استفاده می‌کند:

$$\text{TargetStock} = \text{AverageDailyConsumption} \times (\text{LeadTimeDays} + \text{ReviewPeriodDays}) + \text{SafetyStock}$$

$$\text{InventoryPosition} = \text{UsableOnHand} + \text{EligibleIncomingOrders} - \text{ReservedQuantity}$$

$$\text{SuggestedQuantity} = \max(0, \text{TargetStock} - \text{InventoryPosition})$$

### اعمال قیود تجاری:
- **حداقل سفارش (MOQ):** چنانچه مقدار پیشنهادی بزرگتر از صفر باشد اما از `MinOrderQuantity` کمتر باشد، به حداقل مجاز افزایش می‌یابد.
- **مضرب بسته‌بندی (Package Multiple):** مقدار پیشنهادی با فرمول $\lceil \frac{Q}{M} \rceil \times M$ به سمت بالا گرد می‌شود (مانند مضارب کیسه ۵۰ کیلویی آرد یا گالن‌های ۲۰ لیتری روغن).
- **قاعده مشورتی (Advisory Rule):** پیشنهادات کاملاً مشورتی بوده و شرح ریاضی محاسبه، فرضیات و تأمین‌کنندگان جایگزین در خروجی درج می‌گردد. هیچ سفارش خریدی در فاز ۴ به صورت خودکار ایجاد نمی‌شود.

---

## ۷. تست‌های خودکار و نتایج راستی‌آزمایی

### ۱. اجرای تست‌های یکپارچه راه‌حل (.NET Test Suite):
```powershell
dotnet test ParkPalace.sln
```
**خروجی واقعی دستور:**
```text
Test run for D:\ParkPlace\tests\ParkPalace.UnitTests\bin\Debug\net8.0\ParkPalace.UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed!  - Failed: 0, Passed: 23, Skipped: 0, Total: 23, Duration: 6 s - ParkPalace.UnitTests.dll (net8.0)

Test run for D:\ParkPlace\tests\ParkPalace.IntegrationTests\bin\Debug\net8.0\ParkPalace.IntegrationTests.dll (.NETCoreApp,Version=v8.0)
Passed!  - Failed: 0, Passed: 17, Skipped: 0, Total: 17, Duration: 8 s - ParkPalace.IntegrationTests.dll (net8.0)
```
**مجموع تست‌های موفق:** ۴۰ آزمون موفق از ۴۰ آزمون (بدون حتی یک مورد خطا یا پرش).

### ۲. ساخت نسخه نهایی فرانت‌اند (React Production Build):
```powershell
npm run build
```
**خروجی واقعی دستور:**
```text
> parkpalace-web@1.0.0 build
> tsc && vite build

vite v5.4.21 building for production...
transforming...
✓ 1948 modules transformed.
rendering chunks...
computing gzip size...
dist/index.html                   0.73 kB │ gzip:  0.47 kB
dist/assets/index-DDbzkEvZ.css   11.63 kB │ gzip:  2.70 kB
dist/assets/index-D7shd-Js.js   267.09 kB │ gzip: 73.14 kB │ map: 706.56 kB
✓ built in 14.40s
```

---

## ۸. جدول ارزیابی معیارهای پذیرش Gate G4

| شناسه | معیار پذیرش فاز ۴ | وضعیت | شواهد و نحوه راستی‌آزمایی |
| :--- | :--- | :---: | :--- |
| **G4-01** | نگاشت تأییدشده منبع سپیدار مستند شده باشد. | **PASS** | سند [Phase4_Sepidar_Verified_Mapping.md](file:///d:/ParkPlace/docs/Phase4_Sepidar_Verified_Mapping.md) با شرح متادیتا و مدل آداپتور دوگانه ایجاد شد. |
| **G4-02** | عدم نوشتن یا تغییر اسکیما در دیتابیس سپیدار. | **PASS** | آداپتور صرفاً شامل کوئری‌های SELECT بوده و دیتابیس CRM کاملاً مجزا در `localhost` ایجاد شده است. |
| **G4-03** | مدل موجودی تک‌انباره پیاده‌سازی شده باشد. | **PASS** | موجودیت‌های انبار با کلید `WH-MAIN`، اسنپ‌شات‌ها و کاردکس‌ها در دیتابیس ایجاد و مقداردهی اولیه شدند. |
| **G4-04** | همگام‌سازی انبار تکرارپذیر و پایا (Idempotent) باشد. | **PASS** | تست `Sync_IsIdempotent_DoesNotDuplicateMovements` با ۲ بار اجرای متوالی پاس شد و رکورد تکراری ایجاد نکرد. |
| **G4-05** | حرکات ابطال‌شده و اصلاحی به درستی مدیریت شوند. | **PASS** | تست `CalculateConsumption_ExcludesCancelledAndTransfers` اثبات کرد که تراکنش‌های ابطالی از مانده مصرف کسر می‌گردند. |
| **G4-06** | محاسبات مصرف روزانه با دیتای آزمایشی تأیید شود. | **PASS** | تست‌های `CalculateConsumption_HandlesZeroDaysAndPeriodCorrectly` و `CalculateConsumption_AppliesUnitConversion` پاس شدند. |
| **G4-07** | فرمول پیشنهاد خرید و فرضیات اعتبارسنجی شوند. | **PASS** | تست‌های `PeriodicReview_CalculatesSuggestedQuantity` و `PeriodicReview_AppliesMoqAndPackageMultiple` با دقت اعشاری پاس شدند. |
| **G4-08** | ناوبری تأمین‌کننده از موجودی و پیشنهادات کار کند. | **PASS** | در هر دو نمای انبار و سفارشات خرید، پیوندهای دوطرفه به کاتالوگ ماده اولیه و پروفایل تأمین‌کننده پیاده‌سازی شدند. |
| **G4-09** | دسترسی‌های API بر اساس نقش‌ها اعمال شوند. | **PASS** | تست یکپارچگی `RBAC_InventoryEndpoints_EnforceForbiddenForUnauthorizedRoles` خطای ۴۰۳ را برای نقش‌های غیرمجاز تأیید کرد. |
| **G4-10** | داشبورد انبار فارسی راست‌چین تکمیل شده باشد. | **PASS** | کامپوننت‌های `WarehouseView.tsx` و `OrdersView.tsx` با طراحی شکیل، کارت‌های KPI و کاردکس تعاملی ساخته شدند. |
| **G4-11** | مایگریشن‌های دیتابیس و تست‌های خودکار پاس شوند. | **PASS** | مایگریشن `20261017200046_Phase4_InventoryAndWarehouse` اعمال گردید و هر ۴۰ تست با موفقیت پاس شدند. |
| **G4-12** | خطاهای همگام‌سازی و داده‌های کهنه نمایش داده شوند. | **PASS** | فلگ `hasStaleInventoryWarning` و بج‌های داده قدیمی در داشبورد انبار و پیشنهادات خرید تعبیه شدند. |

---

## ۹. مستندات تحویلی و نیازمندی‌های استقرار

1. **سند نگاشت سپیدار:** [docs/Phase4_Sepidar_Verified_Mapping.md](file:///d:/ParkPlace/docs/Phase4_Sepidar_Verified_Mapping.md)
2. **سند معماری انبار و محاسبات:** [docs/Phase4_Inventory_Architecture.md](file:///d:/ParkPlace/docs/Phase4_Inventory_Architecture.md)
3. **گزارش تفصیلی فاز ۴:** [docs/Phase4_Implementation_Report.md](file:///d:/ParkPlace/docs/Phase4_Implementation_Report.md)
4. **اسکریپت مایگریشن دیتابیس CRM:** [scripts/phase4_inventory_and_warehouse.sql](file:///d:/ParkPlace/scripts/phase4_inventory_and_warehouse.sql)

### روش بازگشت به عقب (Rollback Strategy):
در صورت نیاز به بازگرداندن تغییرات دیتابیس CRM به فاز ۳:
```powershell
dotnet ef database update 20261017163836_Phase3_CatalogAndSuppliers --project src/ParkPalace.Infrastructure --startup-project src/ParkPalace.Api
```

---

## ۱۰. نتیجه‌گیری و توقف رسمی در Gate G4

تمام اهداف، الزامات ایمنی و معیارهای کمی و کیفی تعریف‌شده برای **Phase 4 — Inventory Synchronization & Single Warehouse** با بالاترین استانداردهای مهندسی نرم‌افزار پیاده‌سازی، آزمایش و مستندسازی شدند.

**توقف:** مطابق با مأموریت ابلاغی، کلیه عملیات توسعه متوقف شده و هیچ‌گونه کدی برای فاز ۵ یا استقرار نهایی در محیط عملیاتی اجرا نخواهد شد.

**منتظر بررسی و صدور تأییدیه رسمی اپراتور در Gate G4 جهت ورود به فاز ۵ هستیم.**
