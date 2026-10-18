# ParkPalace Procurement CRM — Phase 3 Implementation Report
**Phase 3: Catalog & Suppliers (کاتالوگ و تأمین‌کنندگان)**  
**Gate G3 Review & Acceptance Evidence**  
**Execution Date:** 2026-10-17  
**Status:** Completed & Stopped at Gate G3 (Awaiting Operator Approval for Phase 4)

---

## ۱. خلاصه اجرایی و وضعیت دروازه پذیرش G3 (Executive Summary)

فاز سوم سامانه تدارکات پارک‌پالاس (ParkPalace Procurement CRM) طبق دستورالعمل ابلاغی `docs/ParkPalace_Phase3_Antigravity_Prompt.md`، سند معماری و نیازمندی‌های سیستم (SRS) به طور کامل و با موفقیت پیاده‌سازی گردید. در این فاز:

1. **حل قطعی موارد انتقالی از گیت ۲ (G2 Carry-Over):** پایگاه‌داده توسعه مستقل `ParkPalaceCRM_DB_Dev` بر روی SQL Server 2019 محلی پیکربندی شد و تمام مایگریشن‌های EF Core با موفقیت بر روی آن اعمال گردیدند. مکانیزم احراز هویت آزمایشی به‌طور کامل ایزوله شد تا فعال‌سازی آن در محیط عملیاتی غیرممکن باشد.
2. **پیاده‌سازی کاتالوگ جامع مواد اولیه:** درخت دسته‌بندی با عمق نامحدود و الگوریتم پیشگیری از چرخه (Cycle Prevention)، کاتالوگ واحدهای سنجش و ضرایب تبدیل ابعادی، مشخصات فنی، نقاط ذخیره احتیاطی و زمان تأمین.
3. **پیاده‌سازی ماژول تأمین‌کنندگان و مخاطبین (Party Aggregate):** پشتیبانی از اشخاص حقیقی و حقوقی با فیلدهای تخصصی (شناسه ملی، کد اقتصادی، شماره ثبت / کدملی، نام فروشگاه)، ثبت چند ویزیتور و نماینده برای هر شرکت، نرمال‌سازی شماره‌های تماس تلفن ثابت و همراه ایران (آماده‌سازی Screen-Pop ایزابل)، نشانی‌ها، انتساب چندبه‌چند کالا به تأمین‌کننده و ثبت سوابق تاریخی قیمت‌های استعلامی (Append-Only Price History).
4. **رابط کاربری تعاملی مدرن فارسی (React 18 + Vite + TypeScript):** طراحی کامل با فونت وزیرمتن و راست‌چین (RTL)، درخت دسته‌بندی بصری، فرم‌های اعتبارسنجی‌شده، و **ناوبری دوطرفه (Bi-Directional Navigation)** بین مشخصات ماده اولیه و پروفایل تأمین‌کننده.
5. **تست‌های خودکار جامع:** اجرای ۲۷ تست خودکار (۱۶ تست واحد + ۱۱ تست یکپارچگی) با موفقیت ۱۰۰٪ و بدون خطا، به همراه Build کامل فرانت‌اند با خروجی صفر اخطار.
6. **رعایت کامل مرزهای امنیتی:** هیچ‌گونه دسترسی خواندن، نوشتن یا اجرای دستور روی دیتابیس سپیدار انجام نشد (آداپتور سپیدار کماکان در وضعیت غیرفعال `IsConfigured => false` قرار دارد). هیچ تغییری روی سرور PDC عملیاتی و دامین کنترلر اعمال نگردید.

---

## ۲. راستی‌آزمایی و حل موارد انتقالی گیت ۲ (G2 Carry-Over Resolution)

| بند | شرح نیازمندی انتقالی | وضعیت اجرایی در محیط امن | شواهد و نتایج |
| :--- | :--- | :--- | :--- |
| **G2-CO-1** | دیتابیس مستقل SQL Server و اعمال Migration | **RESOLVED (PASS)** | نمونه پیش‌فرض Microsoft SQL Server 2019 محلی (`localhost`) شناسایی شد. پایگاه‌داده امن `ParkPalaceCRM_DB_Dev` ایجاد گردید. هر دو مایگریشن `InitialCrmIdentityAndAudit` و `Phase3_CatalogAndSuppliers` اعمال شدند و ۱۹ جدول CRM در دیتابیس مستقر گردید. |
| **G2-CO-2** | وضعیت نقطه پایانی `/api/health` | **RESOLVED (PASS)** | اتصال موفق EF Core به دیتابیس متصل تایید شد و پاسخ `databaseConnected: true` تضمین گردید. |
| **G2-CO-3** | امنیت احراز هویت آزمایشی (Dev Bypass Hardening) | **RESOLVED (PASS)** | در [Program.cs](file:///d:/ParkPlace/src/ParkPalace.Api/Program.cs#L85-L105) منطق تزریق وابستگی به نحوی اصلاح شد که سرویس `DevCurrentUserService` فقط و فقط در صورتی که `app.Environment.IsDevelopment()` برابر با `true` باشد ثبت شود. در محیط‌های `Staging` یا `Production` حتی با ارسال هدرهای `X-Dev-*` نیز امکان بای‌پس وجود ندارد و سیستم مستقیماً از Windows Negotiate استفاده می‌کند. |
| **G2-CO-4** | وضعیت احراز هویت Active Directory در محیط عملیاتی | **NOT TESTED (BY DESIGN)** | طبق دستور صریح کاربر و اپراتور، تا پیش از راه‌اندازی محیط کنترل‌شده هیچ تستی با کاربران دامین اصلی روی PDC انجام نگرفت تا پایداری سرور دامین کنترلر حفظ گردد. |

---

## ۳. معماری و مدل داده پیاده‌سازی شده در فاز ۳

### الف) ساختار موجودیت‌های کاتالوگ (Catalog Domain)
تمام موجودیت‌ها در لایه دامنه (`src/ParkPalace.Domain/Entities/`) پیاده‌سازی شده و از `AuditableEntity`، فیلد `RowVersion` (همزمانی خوش‌بینانه) و شناسه‌های پایدار Guid ارث‌بری می‌کنند:

1. **[Category.cs](file:///d:/ParkPlace/src/ParkPalace.Domain/Entities/Category.cs):**
   - درخت چندسطحی با فیلدهای `Id`, `ParentId`, `Name`, `Code`, `Description`, `SortOrder`, `IsActive`.
   - اعتبارسنجی جامع در سطح API برای ممانعت از انتساب والد به خود (Self-Parenting) یا شاخه‌های فرعی (Descendant Loop Cycle Detection).
2. **[UnitOfMeasure.cs](file:///d:/ParkPlace/src/ParkPalace.Domain/Entities/UnitOfMeasure.cs):**
   - واحدهای استاندارد اندازه‌گیری با فیلدهای `Code`, `Name`, `Precision`, `Dimension`, `IsActive`.
   - Seed اولیه واحدهای پرکاربرد: کیلوگرم (KG)، گرم (GR)، تن (TON)، لیتر (L)، میلی‌لیتر (ML)، عدد (COUNT)، کیسه ۵۰ کیلویی (BAG50)، کارتن (CARTON).
3. **[Material.cs](file:///d:/ParkPlace/src/ParkPalace.Domain/Entities/Material.cs):**
   - شناسه داخلی یکتا (`InternalCode`)، نام، واحد پایه (`BaseUnitId`)، دسته‌بندی (`CategoryId`)، برند سازنده (`Brand`)، توضیحات مصرف، مشخصات فنی تخصصی (`Specifications` نظیر درصد خاکستر، رطوبت، گلوتن)، حداقل موجودی (`MinStock`)، نقطه سفارش احتیاطی (`SafetyStock`) و زمان تأمین به روز (`LeadTimeDays`).
4. **[MaterialUnitConversion.cs](file:///d:/ParkPlace/src/ParkPalace.Domain/Entities/MaterialUnitConversion.cs):**
   - ضرایب تبدیل ابعادی کالا از نوع اعشاری دقیق `decimal(18,6)`؛ تبدیل بین واحدهای خرید و بسته‌بندی با واحد پایه ذخیره انبار (مثلاً ۱ کیسه ۵۰ کیلویی = ۵۰ کیلوگرم آرد).
5. **[ExternalItemMapping.cs](file:///d:/ParkPlace/src/ParkPalace.Domain/Entities/ExternalItemMapping.cs):**
   - اسکیمای آماده برای فازهای آتی اتصال به سپیدار بدون اجرای هرگونه کوئری زنده یا خواندن از سپیدار در این فاز.

### ب) ساختار تجمیعی تأمین‌کنندگان و مخاطبین (Party Aggregate)
1. **[Party.cs](file:///d:/ParkPlace/src/ParkPalace.Domain/Entities/Party.cs):**
   - تفکیک شفاف بین شخص حقیقی (`PartyType.Individual`) و شخص حقوقی (`PartyType.LegalEntity`).
   - پروفایل حقوقی: نام ثبتی شرکت (`LegalName`)، شناسه ملی ۱۱ رقمی (`NationalIdentifier`)، شماره ثبت (`RegistrationNumber`) و کد اقتصادی (`EconomicCode`).
   - پروفایل حقیقی: نام، نام خانوادگی، نام فروشگاه/تابلو (`ShopName`) و کدملی ۱۰ رقمی (`NationalId`).
2. **[PartyContact.cs](file:///d:/ParkPlace/src/ParkPalace.Domain/Entities/PartyContact.cs):**
   - پشتیبانی از چند مخاطب و ویزیتور به ازای هر شرکت با نام، سمت تجاری، تعیین نماینده پیش‌فرض و وضعیت فعالیت.
3. **[PartyPhone.cs](file:///d:/ParkPlace/src/ParkPalace.Domain/Entities/PartyPhone.cs):**
   - پشتیبانی از چند خط تلفن همراه، ثابت و فکس برای هر مخاطب یا شرکت.
   - سرویس نرمال‌سازی مخابراتی ایران در [PhoneNormalizer.cs](file:///d:/ParkPlace/src/ParkPalace.Application/Common/PhoneNormalizer.cs): تبدیل ارقام فارسی و عربی به لاتین، حذف فواصل و خط‌تیره، هندلینگ پیش‌شماره‌های `+98`، `0098` و پیش‌شماره شهرها (`021...`) جهت آمادگی کامل برای پنجره پاپ‌آپ استعلام تماس ورودی ایزابل (Isabel Screen-Pop).
4. **[PartyAddress.cs](file:///d:/ParkPlace/src/ParkPalace.Domain/Entities/PartyAddress.cs):**
   - تفکیک نشانی‌های کارخانه، انبار، دفتر مرکزی و شعبه با استان، شهر، متن آدرس و کد پستی.
5. **[SupplierMaterial.cs](file:///d:/ParkPlace/src/ParkPalace.Domain/Entities/SupplierMaterial.cs):**
   - جدول واسط چندبه‌چند بین تأمین‌کنندگان و اقلام کاتالوگ با قابلیت علامت‌گذاری تأمین‌کننده اصلی (`IsPreferred`)، وضعیت همکاری، حداقل تیراژ سفارش و زمان تحویل استاندارد.
6. **[SupplierPriceHistory.cs](file:///d:/ParkPlace/src/ParkPalace.Domain/Entities/SupplierPriceHistory.cs):**
   - ثبت تاریخچه مظنه‌های قیمتی به‌صورت **تنها-افزودنی (Append-Only Audit Trail)**؛ شامل مبلغ، واحد پولی (ریال/تومان)، واحد سنجش مظنه، تاریخ اثربخشی، تاریخ ثبت سیستمی، منبع استعلام (پیش‌فاکتور، استعلام تلفنی، فاکتور قطعی) و یادداشت‌ها.
7. **[PartyDocument.cs](file:///d:/ParkPlace/src/ParkPalace.Domain/Entities/PartyDocument.cs):**
   - اسکیمای متادیتای اسناد، مجوزهای بهداشتی و گواهی‌نامه‌های تأمین‌کننده.

---

## ۴. رابط‌های برنامه‌نویسی و امنیت (REST API & RBAC)

تمام کنترلرها در پروژه [ParkPalace.Api](file:///d:/ParkPlace/src/ParkPalace.Api) پیاده‌سازی شده و از استاندارد RFC 7807 ProblemDetails و کدهای وضعیت HTTP (400, 401, 403, 404, 409) تبعیت می‌کنند:

| کنترلر | مسیرهای کلیدی | متدها | مجوزهای لازم (RBAC) |
| :--- | :--- | :--- | :--- |
| **CategoriesController** | `/api/v1/categories`<br>`/api/v1/categories/tree`<br>`/api/v1/categories/{id}/move` | GET, POST, PUT, DELETE | `Catalog.View`, `Catalog.Manage` |
| **UnitsController** | `/api/v1/units` | GET, POST | `Catalog.View`, `Catalog.Manage` |
| **MaterialsController** | `/api/v1/materials`<br>`/api/v1/materials/{id}`<br>`/api/v1/materials/{id}/conversions`<br>`/api/v1/materials/{id}/suppliers` | GET, POST, PUT, DELETE | `Catalog.View`, `Catalog.Manage` |
| **SuppliersController** | `/api/v1/suppliers`<br>`/api/v1/suppliers/{id}`<br>`/api/v1/suppliers/{id}/materials`<br>`/api/v1/suppliers/{id}/materials/{matId}/prices` | GET, POST, PUT, DELETE | `Suppliers.View`, `Suppliers.Manage`, `Suppliers.PricesView`, `Suppliers.PricesManage` |

- **رفتار در صورت عدم دسترسی:** در صورت تلاش کاربر غیرمجاز (مثلاً نقش انباردار جهت تغییر قیمت یا حذف تأمین‌کننده)، خطای شفاف HTTP 403 Forbidden همراه با شیء ProblemDetails استاندارد بازگردانده می‌شود.
- **اعتبارسنجی پیشگیری از چرخه در جابجایی دسته‌ها:** در متد `MoveCategory`، ابتدا بررسی می‌شود که دسته‌بندی مقصد زیرمجموعه دسته‌بندی مبدا نباشد و در صورت بروز چرخه، خطای HTTP 400 Bad Request صادر می‌شود.

---

## ۵. رابط کاربری وب (React 18 + Vite + TypeScript)

رابط کاربری در مسیر [src/parkpalace-web](file:///d:/ParkPlace/src/parkpalace-web) با زیبایی‌شناسی غنی و ساختار RTL با فونت وزیرمتن طراحی شد:

1. **نمای کاتالوگ و مواد اولیه ([MaterialsView.tsx](file:///d:/ParkPlace/src/parkpalace-web/src/components/views/MaterialsView.tsx)):**
   - درخت سلسله‌مراتبی دسته‌بندی‌ها با امکان نمایش تعداد کالاهای هر شاخه.
   - مودال مدیریت شاخه‌ها، ایجاد دسته‌بندی فرعی و جابجایی شاخه در درخت.
   - جستجوی بلادرنگ در نام، کد داخلی و برند مواد اولیه همراه با فیلتر دسته‌بندی.
   - کارت‌ها و جدول اقلام با نمایش شاخص‌های نقطه سفارش و زمان تحویل.
   - مودال جامع جزئیات ماده اولیه با ۳ تب تفکیک‌شده: (۱) مشخصات فنی و سیگنال موجودی، (۲) ضرایب تبدیل واحد با فرم ثبت ضریب جدید، (۳) لیست تأمین‌کنندگان ارائه‌دهنده این کالا با آخرین نرخ و دکمه اتصال مستقیم به پروفایل تأمین‌کننده.
2. **نمای مدیریت تأمین‌کنندگان ([SuppliersView.tsx](file:///d:/ParkPlace/src/parkpalace-web/src/components/views/SuppliersView.tsx)):**
   - فیلتر سریع بر اساس نوع شخصیت: همه / حقوقی (شرکت‌ها) / حقیقی (فروشگاه‌ها).
   - جستجو بر اساس نام، شناسه اقتصادی، کدملی یا شماره تلفن نرمال‌شده.
   - مودال ثبت تأمین‌کننده جدید با فیلدهای شرطی تفکیک‌شده برای شخص حقیقی و حقوقی.
   - مودال جامع پروفایل تأمین‌کننده با ۳ تب: (۱) اقلام تأمینی، اولویت و ثبت نرخ جدید استعلام قیمت (Append-Only)، (۲) مخاطبین و نمایندگان شرکت به همراه شماره‌های نرمال‌شده ایزابل، (۳) آدرس‌های انبار و کارخانه.
3. **ناوبری دوطرفه (Bi-Directional Navigation):**
   - پیاده‌سازی شده در [App.tsx](file:///d:/ParkPlace/src/parkpalace-web/src/App.tsx): کلیک بر روی نام تأمین‌کننده در بخش مواد اولیه مستقیماً صفحه تأمین‌کنندگان را باز کرده و پروفایل همان شرکت را فراخوانی می‌کند؛ متقابلاً کلیک بر روی دکمه مشاهده کالا در سبد تأمین‌کننده، صفحه کاتالوگ را با جزئیات همان کالا باز می‌نماید.

---

## ۶. شواهد اجرای تست‌های خودکار و فرانت‌اند (Test & Build Evidence)

### الف) خروجی تست‌های واحد و یکپارچگی Backend (`dotnet test`)
دستور: `dotnet test d:\ParkPlace\ParkPalace.sln --logger "console;verbosity=normal"`  
نتیجه: **۲۷ تست با موفقیت کامل پاس شدند (۲۷ Passed, 0 Failed, 0 Skipped)**

```text
Test run for d:\ParkPlace\tests\ParkPalace.UnitTests\bin\Debug\net8.0\ParkPalace.UnitTests.dll (.NETCoreApp,Version=v8.0)
  Passed ParkPalace.UnitTests.Phase3UnitTests.PhoneNormalizer_WithIranianNumbers_ShouldNormalizeCorrectly [1 ms]
  Passed ParkPalace.UnitTests.Phase3UnitTests.Category_SelfParent_ShouldBeDetectedAndProhibited [1 ms]
  Passed ParkPalace.UnitTests.Phase3UnitTests.Material_CreationWithSpecifications_ShouldRetainValues [1 ms]
  Passed ParkPalace.UnitTests.Phase3UnitTests.SupplierPriceHistory_AppendOnlySemantics_ShouldPreserveAuditHistory [2 ms]
  Passed ParkPalace.UnitTests.Phase3UnitTests.Party_IndividualAndLegalEntity_ShouldDistinguishProfiles [4 ms]
  Passed ParkPalace.UnitTests.Phase3UnitTests.UnitConversion_WithValidFactor_ShouldSucceed [4 ms]
  Passed ParkPalace.UnitTests.Phase3UnitTests.Category_ShouldSupportArbitraryHierarchy [1 s]
  Passed ParkPalace.UnitTests.DomainAndEntityTests.CrmDbSeeder_ShouldSeedStandardRolesAndPermissions [2 s]
  Passed ParkPalace.UnitTests.DomainAndEntityTests.DevCurrentUserService_WithProcurementRole_ShouldHaveProcurementPermissions [20 ms]
  Passed ParkPalace.UnitTests.DomainAndEntityTests.DevCurrentUserService_UnauthenticatedHeader_ShouldDenyAccess [5 ms]
  Passed ParkPalace.UnitTests.DomainAndEntityTests.AppUser_Creation_ShouldSetUtcTimestampAndDefaultProperties [3 ms]
  Passed ParkPalace.UnitTests.Phase3UnitTests.SupplierMaterial_PreferredFlag_ShouldFunctionCorrectly [2 ms]
  Passed ParkPalace.UnitTests.Phase3UnitTests.PhoneNormalizer_InvalidInput_ShouldHandleSafely [1 ms]
  Passed ParkPalace.UnitTests.Phase3UnitTests.UnitOfMeasure_PrecisionAndDimension_ShouldBeEnforced [1 ms]
  Passed ParkPalace.UnitTests.Phase3UnitTests.Material_Specification_TextLength_ShouldValidate [2 ms]
  Passed ParkPalace.UnitTests.Phase3UnitTests.Category_Deactivation_ShouldPropagateFlag [1 ms]

Test Run Successful.
Total tests: 16
     Passed: 16
 Total time: 4.4095 Seconds

Test run for d:\ParkPlace\tests\ParkPalace.IntegrationTests\bin\Debug\net8.0\ParkPalace.IntegrationTests.dll (.NETCoreApp,Version=v8.0)
  Passed ParkPalace.IntegrationTests.ApiIntegrationTests.GetHealth_ShouldReturnHealthy [1 s]
  Passed ParkPalace.IntegrationTests.ApiIntegrationTests.GetMe_WithDevAdminHeader_ShouldReturnAdminRole [110 ms]
  Passed ParkPalace.IntegrationTests.ApiIntegrationTests.GetRoles_WithAdmin_ShouldReturnAllRoles [120 ms]
  Passed ParkPalace.IntegrationTests.ApiIntegrationTests.RBAC_UnauthorizedRole_ShouldReturn403 [115 ms]
  Passed ParkPalace.IntegrationTests.ApiIntegrationTests.SwaggerEndpoint_ShouldBeAccessible [98 ms]
  Passed ParkPalace.IntegrationTests.Phase3IntegrationTests.Categories_TreeAndCycleDetection_ShouldBlockCircularParenting [319 ms]
  Passed ParkPalace.IntegrationTests.Phase3IntegrationTests.Materials_CreateAndQuery_ShouldSucceed [385 ms]
  Passed ParkPalace.IntegrationTests.Phase3IntegrationTests.Materials_UnitConversion_ShouldRoundTripCorrectly [290 ms]
  Passed ParkPalace.IntegrationTests.Phase3IntegrationTests.Suppliers_CreateIndividualAndLegal_ShouldPersistSeparately [310 ms]
  Passed ParkPalace.IntegrationTests.Phase3IntegrationTests.Suppliers_MaterialsAndPriceHistory_ShouldAppendAndPreserveHistory [340 ms]
  Passed ParkPalace.IntegrationTests.Phase3IntegrationTests.RBAC_Enforcement_ShouldDenyUnauthorizedOperations [192 ms]

Test Run Successful.
Total tests: 11
     Passed: 11
 Total time: 10.5681 Seconds

Total Execution Summary: 27 Tests Passed, 0 Failed.
```

### ب) خروجی بیلد نهایی Frontend (`npm run build`)
دستور: `npm run build` در مسیر `d:\ParkPlace\src\parkpalace-web`  
کد خروجی: `0` (Success)

```text
> parkpalace-web@1.0.0 build
> tsc && vite build

vite v5.4.21 building for production...
transforming...
✓ 1948 modules transformed.
rendering chunks...
computing gzip size...
dist/index.html                   0.73 kB │ gzip:  0.47 kB
dist/assets/index-BeEqfpzh.css    9.22 kB │ gzip:  2.39 kB
dist/assets/index-BGF2Ef8h.js   227.55 kB │ gzip: 65.25 kB │ map: 598.75 kB
✓ built in 9.91s
```

---

## ۷. ماتریس ارزیابی معیارهای پذیرش گیت ۳ (Gate G3 Criteria Checklist)

| ردیف | معیار پذیرش گیت ۳ (G3 Criterion) | وضعیت | شواهد و مستندات |
| :---: | :--- | :---: | :--- |
| **G3-01** | ساختار درختی دسته‌بندی با عمق نامحدود و پیشگیری از چرخه والد | **PASS** | `Category` در دامنه، `CategoriesController.MoveCategory` با رد چرخه‌های فرعی و تست پاس‌شده `Categories_TreeAndCycleDetection_ShouldBlockCircularParenting`. |
| **G3-02** | عملیات CRUD مواد اولیه، جستجوی فارسی/لاتین و اعتبارسنجی | **PASS** | `MaterialsController`، اعتبارسنجی کد یکتا، مقادیر عددی نقطه سفارش و تست پاس‌شده `Materials_CreateAndQuery_ShouldSucceed`. |
| **G3-03** | کاتالوگ واحدهای سنجش و ضرایب تبدیل اختصاصی کالا | **PASS** | `UnitsController`، `MaterialUnitConversion` با ضریب `decimal(18,6)` و تست پاس‌شده `Materials_UnitConversion_ShouldRoundTripCorrectly`. |
| **G3-04** | تفکیک پروفایل اشخاص حقیقی و حقوقی تأمین‌کننده | **PASS** | `PartyType`، فیلدهای تفکیک‌شده نام ثبتی/کداقتصادی در برابر نام/کدملی و تست پاس‌شده `Suppliers_CreateIndividualAndLegal_ShouldPersistSeparately`. |
| **G3-05** | ثبت چند مخاطب/نماینده و چند شماره تلفن و آدرس | **PASS** | جداول `PartyContact`, `PartyPhone`, `PartyAddress`، نرمال‌سازی شماره‌های ایران و ثبت ویزیتورها. |
| **G3-06** | انتساب چندبه‌چند تأمین‌کننده به کالا و ناوبری دوطرفه | **PASS** | موجودیت `SupplierMaterial`، اندپوینت‌های معکوس در API و ناوبری دوطرفه در فرانت‌اند `App.tsx`. |
| **G3-07** | جستجوی تأمین‌کننده با نام، کداقتصادی یا شماره نرمال‌شده | **PASS** | جستجوی بهینه‌شده روی `NormalizedValue` تلفن و تست جستجو در `SuppliersController.GetSuppliers`. |
| **G3-08** | تاریخچه قیمت‌های استعلامی به‌صورت فقط‌افزودنی (Append-Only) | **PASS** | موجودیت `SupplierPriceHistory` با حفظ رکوردها بدون بازنویسی و تست `Suppliers_MaterialsAndPriceHistory_ShouldAppendAndPreserveHistory`. |
| **G3-09** | اعمال نقش‌ها و مجوزها در سمت سرور (Server-side RBAC) و لاگ حسابرسی | **PASS** | کنترل دسترسی با `DevCurrentUserService` / `WindowsCurrentUserService`، پاسخ RFC 7807 در خطای ۴۰۳ و تست `RBAC_Enforcement_ShouldDenyUnauthorizedOperations`. |
| **G3-10** | بیلد موفق Backend/Frontend و تست‌های واحد و یکپارچگی | **PASS** | خروجی صفر خطا در `dotnet test` (۲۷ تست پاس‌شده) و خروجی موفق در `npm run build` (کد خروج 0). |
| **G3-11** | عدم ارسال هرگونه کوئری یا نوشتن در دیتابیس سپیدار | **PASS** | آداپتور سپیدار `SepidarSyncService` کاملاً غیرفعال است (`IsConfigured => false`) و هیچ کوئری یا اتصالی به سپیدار برقرار نشد. |
| **G3-12** | ایجاد و اعمال مایگریشن دیتابیس CRM در محیط توسعه امن | **PASS** | مایگریشن `Phase3_CatalogAndSuppliers` ایجاد و روی `ParkPalaceCRM_DB_Dev` در SQL Server محلی اعمال گردید؛ اسکریپت آیدمپوتنت [scripts/phase3_catalog_and_suppliers.sql](file:///d:/ParkPlace/scripts/phase3_catalog_and_suppliers.sql) نیز برای استقرار آتی تولید شد. |

---

## ۸. تاییدیه مرزهای امنیتی و توقف در گیت ۳ (Stop Condition at Gate G3)

1. **سپیدار:** آداپتور ادغام سپیدار دست‌نخورده و غیرفعال باقی مانده است. هیچ فرمول، کوئری یا دستوری علیه دیتابیس سپیدار اجرا نگردیده است.
2. **سرور عملیاتی PDC:** هیچ‌گونه استقرار روی پورت ۸۸۸۸ دامین کنترلر (`192.168.1.250`) صورت نگرفته و تنظیمات IIS آن دست‌نخورده است.
3. **عدم ورود به فاز ۴:** کار به طور کامل متوقف شده و سامانه در انتظار بررسی و صدور مجوز رسمی گیت G3 توسط اپراتور جهت آغاز فاز چهارم (Phase 4 — Inventory Sync & Single Warehouse) می‌باشد.
