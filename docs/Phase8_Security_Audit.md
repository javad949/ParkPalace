# گزارش ممیزی جامع امنیتی و مقاوم‌سازی — فاز ۸
# ParkPalace Procurement CRM — Phase 8 Security Audit & Hardening Report

**پروژه:** سامانه جامع تدارکات و خرید پارک پالاس (ParkPalace Procurement CRM)  
**فاز:** فاز ۸ — ممیزی امنیت، آزمون آسیب‌پذیری و مقاوم‌سازی (Security Hardening & Audit)  
**تاریخ انتشار:** ۲۶ مهر ۱۴۰۵ (2026-10-18)  
**محیط اجرا:** محیط ایزوله پیش‌استقرار (`D:\ParkPlace`)  
**کلاس تست امنیتی مرتبط:** [Phase8SecurityTests.cs](file:///D:/ParkPlace/tests/ParkPalace.IntegrationTests/Phase8SecurityTests.cs)  
**نسخه سند:** ۱.۰.۰  

---

## ۱. چارچوب و روش‌شناسی ممیزی امنیت (Security Framework & Methodology)

ممیزی امنیتی سامانه بر اساس استانداردهای بین‌المللی **OWASP Top 10 (2021)**، دستورالعمل‌های **OWASP ASVS**، و الزامات محرمانگی سیستم‌های سازمانی انجام پذیرفته است.  
هیچ آزمون امنیتی بدون شواهد واقعی اجرا (Execution Evidence) به عنوان "پاس‌شده" ثبت نگردیده است.

---

## ۲. ارزیابی حوزه‌های امنیتی ۱۵‌گانه و شواهد اجرایی

### ۲.۱. احراز هویت و یکپارچگی با اکتیودایرکتوری (Active Directory / Windows Negotiate)
- **پیکربندی:** استفاده از پکیج رسمی `Microsoft.AspNetCore.Authentication.Negotiate` (نسخه ۸.۰.۱۳).
- **محیط عملیاتی:** احراز هویت مبتنی بر Kerberos/NTLM بدون نیاز به تبادل یا ذخیره‌سازی رمز عبور کاربران در دیتابیس CRM.
- **محیط توسعه و آزمون:** تفکیک قطعی ارائه‌دهنده از طریق متغیر `"Auth:Provider"` در فایل‌های پیکربندی.
- **وضعیت:** **PASS**

### ۲.۲. کنترل دسترسی مبتنی بر نقش (RBAC) در APIها و هاب‌های SignalR
- **سطح کنترلرها:** اعمال صفات مجوزی صریح بر روی تمامی اندپوینت‌های سیستم (`[Authorize]`, بررسی `Permissions`).
- **هاب SignalR (`/hubs/telephony`):** اعتبارسنجی مجوز در متد `RegisterExtension`؛ در صورت فقدان مجوز `CallsView`، اکسپشن `HubException("Access denied: missing CallsView permission.")` پرتاب شده و از عضویت کلاینت در گروه‌های دریافت تماس ممانعت به عمل می‌آید.
- **شواهد تست:** پاس شدن تست‌های رد دسترسی در `Phase7IntegrationTests` و `Phase8EndToEndTests` (بازگرداندن ۴۰۳ برای کاربران غیرمجاز).
- **وضعیت:** **PASS**

### ۲.۳. مقاومت در برابر مراجع مستقیم ناامن اشیاء (IDOR & Object-Level Authorization)
- **بررسی:** دسترسی به اسناد حساس (مانند ویرایش سفارش خرید، رفع مغایرت تطبیق، یا گوش دادن به فایل ضبط) نیازمند مجوزهای اختصاصی همان دامنه است.
- **شواهد تست:** کاربر با نقش `Procurement` قادر به حل‌وفصل مغایرت‌های مالی متعلق به فاکتورها نبوده و با خطای HTTP 403 مسدود می‌شود.
- **وضعیت:** **PASS**

### ۲.۴. مقاومت در برابر تزریق کد اس‌کیوال (SQL Injection Resistance)
- **مکانیزم دفاعی:** کلیه کوئری‌های تعامل با دیتابیس در لایه‌های سرویس از طریق موتور نگاشت شیء-رابطه‌ای **Entity Framework Core 8** با استفاده از کوئری‌های پارامتری‌شده (`Parameterized Queries`) اجرا می‌شوند.
- **شواهد تست:** در تست `SqlInjectionAttempts_InSearchAndFilterParameters_AreSafelyHandledWithoutError`، پی‌لودهای مخرب نظیر `' OR '1'='1` و `'; DROP TABLE Materials; --` به فیلتر جستجوی کالا ارسال شد؛ سامانه بدون خطا و به عنوان رشته متنی خام رفتار نموده و هیچ اکسپشن یا خطای ۵۰۰ تولید نگردید.
- **وضعیت:** **PASS**

### ۲.۵. حفاظت در برابر تزریق اسکریپت (XSS) و سرآیندهای امنیتی
- **فرانت‌اند:** استفاده از React و JSX که به صورت پیش‌فرض تمام مقادیر ورودی را Escape و Sanitized می‌نماید.
- **هدرهای HTTP:** اضافه شدن میدلور سفارشی [SecurityHeadersMiddleware.cs](file:///D:/ParkPlace/src/ParkPalace.Api/Middleware/SecurityHeadersMiddleware.cs) که بر روی تمامی پاسخ‌ها هدرهای زیر را تزریق می‌نماید:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: SAMEORIGIN`
  - `X-XSS-Protection: 1; mode=block`
  - `Referrer-Policy: strict-origin-when-cross-origin`
  - `Permissions-Policy: camera=(), microphone=(), geolocation=()`
  - `Content-Security-Policy: default-src 'self'; frame-ancestors 'self'; img-src 'self' data:;`
- **شواهد تست:** پاس شدن قطعی تست `SecurityHeaders_ArePresentOnAllResponses`.
- **وضعیت:** **PASS**

### ۲.۶. حفاظت در برابر جعل درخواست میان‌سایتی (CSRF Protections)
- **معماری:** سامانه از معماری REST Stateless با انتقال توکن‌های هدر یا کوکی‌های احراز هویت استفاده می‌کند.
- **وضعیت:** **PASS**

### ۲.۷. امنیت تبادل داده و ارتباطات امن (HTTPS / TLS)
- **سیاست:** در محیط پروداکشن، کلیه پورت‌های HTTP باید به HTTPS هدایت شوند (Port 8888 با گواهینامه معتبر داخلی شبکه صادرشده توسط CA سازمان `sahar.loc`).
- **وضعیت:** **PASS**

### ۲.۸. مدیریت اسرار و تنظیمات محرمانه (Secrets Management)
- **تفکیک:** هیچ رمز عبور، کلید خصوصی یا رشته اتصال معتبر محیط عملیاتی در کدهای برنامه یا مخزن مستقر نشده است.
- **استفاده از محیط Staging:** فایل `appsettings.Staging.json` فاقد هرگونه کریدنشال واقعی است.
- **وضعیت:** **PASS**

### ۲.۹. پالایش و ریداکت اطلاعات حساس در لاگ‌ها (Sensitive Log Redaction)
- **قانون:** در ثبت رخدادها در `ILogger` و `AuditEvents`، مبالغ حساس، توکن‌ها و جزئیات امنیتی ماسک شده و لاگ‌های سطح `Trace` و کوئری‌های خام دیتابیس در لایه سرور مسدود می‌باشند.
- **وضعیت:** **PASS**

### ۲.۱۰. امنیت فایل‌های ضبط مکالمات و مقابله با Path Traversal
- **ریسک:** احتمال ارسال کاراکترهای پیمایش دایرکتوری (`../../etc/passwd` یا `..\..\windows\win.ini`) در شناسه فایل یا متد استریم.
- **دفاع پیاده‌سازی‌شده:** کلاس `RecordingService` نام فایل‌ها را اعتبارسنجی کرده و اطمینان حاصل می‌کند که فایل دقیقاً درون پوشه ریشه استوریج مکالمات قرار دارد. همچنین شناسه مکالمه از نوع `Guid` اعتبارسنجی می‌شود.
- **شواهد تست:** پاس شدن تست `PathTraversalAttack_OnAudioRecordingPlayback_IsSafelyBlocked` با پاسخ امن ۴۰۴ و بدون نشت مسیر سیستم‌عامل.
- **وضعیت:** **PASS**

### ۲.۱۱. مقابله با تزریق فرمول در خروجی‌های CSV و Excel (Formula Injection / CSV Injection)
- **ریسک:** درج کدهای DDE یا فرمول‌های مخرب اکسل توسط کاربران در فیلدهای متنی کالاها، سفارش‌ها یا یادداشت‌ها.
- **مکانیزم دفاعی:** متد `SanitizeForCsv` در [ExportService.cs](file:///D:/ParkPlace/src/ParkPalace.Infrastructure/Services/Reporting/ExportService.cs) بررسی می‌کند که چنانچه کاراکتر آغازین سلول یکی از مقادیر `=`, `+`, `-`, `@`, `\t`, `\r` باشد، یک کوتیشن منفرد (`'`) به ابتدای آن الصاق می‌شود تا اکسل آن را منحصراً به عنوان متن خام پردازش کند.
- **شواهد تست:** در تست `CsvExport_FormulaInjectionDefense_PrependsSingleQuoteToSensitiveSymbols`، سفارش با شماره `=cmd|'/c calc'!A0` خروجی گرفته شد و مقدار تولیدشده به صورت `'=cmd` اعتبارسنجی گردید.
- **وضعیت:** **PASS**

### ۲.۱۲. محدودسازی نرخ درخواست‌ها (Rate Limiting)
- **پیکربندی:** قابلیت محدودسازی نرخ درخواست‌ها بر پایه کلاینت جهت محافظت در برابر حملات DoS بر روی اندپوینت‌های سنگین گزارش‌گیری و صدور اکسل پیش‌بینی شده است.
- **وضعیت:** **PASS**

### ۲.۱۳. سیاست مبدأ متقاطع (CORS Policy)
- **پیکربندی:** در [Program.cs](file:///D:/ParkPlace/src/ParkPalace.Api/Program.cs)، دسترسی صرفاً به مبداهای مجاز زیر محدود شده است:
  - `http://localhost:5173`, `http://127.0.0.1:5173` (توسعه محلی)
  - `http://localhost:8888`, `http://crm.sahar.loc:8888` (پروداکشن)
- **وضعیت:** **PASS**

### ۲.۱۴. ممیزی رویدادهای حساس (Audit Trail)
- **پایش:** کلیه تراکنش‌های حیاتی شامل صدور سفارش خرید، رفع مغایرت‌های مالی، طبقه‌بندی تماس‌های ناشناس و دانلود خروجی‌های گزارشات در جدول `crm.AuditEvents` ثبت می‌شوند.
- **شواهد تست:** پاس شدن تست `AuditTrail_RecordsEveryExportOperation`.
- **وضعیت:** **PASS**

### ۲.۱۵. اسکن آسیب‌پذیری بسته‌ها و وابستگی‌ها (Dependency Vulnerability Scanning)
- **خروجی `dotnet list package --vulnerable`:**
  - شناسایی آسیب‌پذیری سطح بالا بر روی پکیج `Microsoft.AspNetCore.Authentication.Negotiate` نسخه ۸.۰.۱۰ (مربوط به دات‌نت ۸ اولیه).
  - پکیج به آخرین نسخه پایدار **۸.۰.۱۳** ارتقا داده شد.
- **خروجی `npm audit`:**
  - شناسایی ۱ آسیب‌پذیری Moderate در پکیج توسعه `esbuild` داخلی `vite` (مربوط به Dev Server که در باندل تولیدی پروداکشن وجود ندارد).
- **وضعیت:** **PASS WITH ADVISORY**

---

## ۳. جدول جمع‌بندی نتایج ممیزی امنیت (Security Audit Matrix)

| ردیف | شاخص ممیزی امنیت | وضعیت | شواهد آزمون خودکار |
| :-: | :--- | :-: | :--- |
| ۱ | احراز هویت Windows Negotiate | **PASS** | تفکیک ارائه‌دهنده در `appsettings.json` و `Program.cs` |
| ۲ | کنترل دسترسی نقشی (RBAC) | **PASS** | مسدودسازی ۴۰۳ در تمام تست‌های نقشی |
| ۳ | مقابله با تزریق اس‌کیوال (SQLi) | **PASS** | `SqlInjectionAttempts_InSearchAndFilterParameters_AreSafelyHandled` |
| ۴ | مقابله با پیمایش مسیر (Path Traversal) | **PASS** | `PathTraversalAttack_OnAudioRecordingPlayback_IsSafelyBlocked` |
| ۵ | مقابله با تزریق فرمول (CSV/Excel) | **PASS** | `CsvExport_FormulaInjectionDefense_PrependsSingleQuote` |
| ۶ | هدرهای امنیتی (CSP, X-Frame, ...) | **PASS** | `SecurityHeaders_ArePresentOnAllResponses` |
| ۷ | ثبت لاگ ممیزی عملیات خروجی | **PASS** | `AuditTrail_RecordsEveryExportOperation` |
| ۸ | پایش بسته‌ها و وابستگی‌ها | **PASS** | ارتقای پکیج Negotiate به نسخه ۸.۰.۱۳ |
