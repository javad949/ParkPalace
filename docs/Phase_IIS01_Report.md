# گزارش رسمی پیاده‌سازی و اعتبارسنجی فاز IIS-01
# ParkPalace CRM — Phase IIS-01: Web Server & ASP.NET Core 8 Runtime Preparation

**پروژه:** سامانه جامع تدارکات و خرید پارک پالاس (ParkPalace CRM)  
**فاز:** `IIS-01` — آماده‌سازی وب‌سرور IIS 10، ران‌تایم ASP.NET Core 8 و ماژول URL Rewrite  
**سرور هدف:** ماشین مجازی `CRM` (`CRM.sahar.loc` / `192.168.1.248`)  
**سیستم‌عامل:** Windows Server 2022 Standard (Build 20348, 64-bit)  
**موتور پایگاه داده:** SQL Server 2022 Enterprise لوکال (از پیش نصب‌شده)  
**آدرس و پورت هدف:** `http://crm.sahar.loc` | پورت: `TCP 80` (HTTP)  
**تاریخ اجرا:** ۲۶ مهر ۱۴۰۵ (18 اکتبر 2026)  
**وضعیت:** **پایان موفقیت‌آمیز فاز IIS-01 و توقف کامل طبق الزامات امنیتی (Mandatory Stop)**  

---

## ۱. مقایسه وضعیت قبل و بعد از تغییرات (Pre & Post Baseline Comparison)

| شاخص / کامپوننت | وضعیت پایه قبل از تغییر (Preflight) | وضعیت نهایی پس از اجرا (Post-Execution) | نتیجه |
| :--- | :--- | :--- | :---: |
| **نقش و فیچرهای IIS 10** | عدم نصب (۰ فیچر فعال) | **۲۶ ویژگی ضروری و مدیریتی نصب شد** | **PASS** |
| **سرویس‌های W3SVC و WAS** | وجود نداشت | **هر دو سرویس Running و Automatic** | **PASS** |
| **ران‌تایم .NET** | عدم وجود `dotnet.exe` | **.NET 8.0.32 (CoreCLR + AspNetCore)** | **PASS** |
| **پکیج Hosting Bundle** | عدم نصب | **ASP.NET Core 8.0.32 Hosting Bundle (رسمی)** | **PASS** |
| **ماژول AspNetCoreModuleV2** | عدم وجود | **نسخه 18.0.26278.32 ثبت‌شده در Global Modules** | **PASS** |
| **ماژول URL Rewrite** | عدم وجود | **نسخه 7.1.1993.2351 (Rewrite 2.1) فعال** | **PASS** |
| **پاسخ HTTP لوکال (127.0.0.1)** | عدم پاسخ (پورت ۸۰ بسته) | **کد وضعیت 200 OK (سرور: Microsoft-IIS/10.0)** | **PASS** |

---

## ۲. فهرست ویژگی‌های نصب‌شده IIS 10 (Installed IIS Features)

کلیه فیچرهای پایه، امنیتی، عملکردی، فشرده‌سازی، سوکت‌های بلادرنگ و ابزارهای مدیریتی فعال شدند:

```text
Name                  DisplayName
----                  -----------
Web-Server            Web Server (IIS)
Web-WebServer         Web Server
Web-Common-Http       Common HTTP Features
Web-Default-Doc       Default Document
Web-Dir-Browsing      Directory Browsing
Web-Http-Errors       HTTP Errors
Web-Static-Content    Static Content (ضروری برای دارایی‌های استاتیک فرانت‌اند React)
Web-Http-Redirect     HTTP Redirection
Web-Health            Health and Diagnostics
Web-Http-Logging      HTTP Logging
Web-Log-Libraries     Logging Tools
Web-Request-Monitor   Request Monitor
Web-Performance       Performance
Web-Stat-Compression  Static Content Compression
Web-Dyn-Compression   Dynamic Content Compression
Web-Security          Security
Web-Filtering         Request Filtering
Web-Windows-Auth      Windows Authentication (ضروری برای یکپارچگی احراز هویت دامین sahar.loc)
Web-App-Dev           Application Development
Web-Net-Ext45         .NET Extensibility 4.8
Web-Asp-Net45         ASP.NET 4.8
Web-ISAPI-Ext         ISAPI Extensions
Web-ISAPI-Filter      ISAPI Filters
Web-WebSockets        WebSocket Protocol (ضروری برای SignalR و پاپ‌آپ بلادرنگ تماس‌های تلفنی)
Web-Mgmt-Tools        Management Tools
Web-Mgmt-Console      IIS Management Console
```

---

## ۳. نسخه‌های ران‌تایم و هاستینگ باندل (.NET Runtime & Hosting Bundle Versions)

پکیج نصاب رسمی مایکروسافت با مشخصات زیر از منبع رسمی دریافت و اعتبارسنجی شد:
* **منبع دانلود:** `https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/8.0.32/dotnet-hosting-8.0.32-win.exe`
* **اعتبارسنجی امضای دیجیتال (Authenticode):** `Valid` (تاییدشده توسط `Microsoft Corporation`)
* **کد خروج نصاب:** `0` (Success)
* **نسخه هاستینگ باندل:** **`8.0.32`** (RID: `win-x64`)
* **ران‌تایم‌های فعال ثبت‌شده:**
  - `Microsoft.AspNetCore.App 8.0.32` در مسیر `C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App`
  - `Microsoft.NETCore.App 8.0.32` در مسیر `C:\Program Files\dotnet\shared\Microsoft.NETCore.App`

---

## ۴. وضعیت و محل استقرار ماژول AspNetCoreModuleV2

* **مسیر فایل کتابخانه:** `C:\Program Files\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll`
* **حجم فایل:** ۴۱۲,۴۷۲ بایت
* **نسخه باینری:** `18.0.26278.32`
* **وضعیت در ماژول‌های سراسری IIS (Global Modules):**
  - نام: `AspNetCoreModuleV2`
  - مسیر ثبت‌شده در `applicationHost.config`: `%ProgramFiles%\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll`
  - وضعیت: **کاملاً لود و آماده میزبانی فرایند In-Process / Out-of-Process اپلیکیشن .NET 8**

---

## ۵. نصب و ثبت ماژول URL Rewrite 2.1

با توجه به نیاز فرانت‌اند Single Page Application (React) به بازنویسی مسیرها به `index.html` و همچنین الزام هدایت ترافیک به HTTPS در آینده، ماژول URL Rewrite 2.1 نصب گردید:
* **منبع دانلود:** `https://download.microsoft.com/download/1/2/8/128E2E22-C1B9-44A4-BE2A-5859ED1D4592/rewrite_amd64_en-US.msi`
* **اعتبارسنجی امضای دیجیتال:** `Valid` (امضای رسمی Microsoft Corporation)
* **کد خروج نصاب:** `0` (Success)
* **مسیر باینری:** `C:\Windows\System32\inetsrv\rewrite.dll`
* **نسخه فایل:** `7.1.1993.2351`
* **ثبت در IIS Global Modules:** ماژول `RewriteModule` با موفقیت در IIS فعال و تست شد.

---

## ۶. وضعیت سرویس‌های IIS و WAS (IIS/WAS Service Status)

هر دو سرویس مرتبط با وب‌سرور در وضعیت کاملاً پایدار قرار گرفتند:

| نام سرویس | نام نمایشی | وضعیت اجرایی | نوع شروع (Startup Type) |
| :--- | :--- | :---: | :---: |
| **`W3SVC`** | World Wide Web Publishing Service | **Running** | **Automatic** |
| **`WAS`** | Windows Process Activation Service | **Running** | **Automatic** |

### نتیجه تست پروب محلی (Local HTTP Probe):
* **درخواست:** `GET http://127.0.0.1/`
* **کد وضعیت:** `200 OK`
* **هدر سرور:** `Server: Microsoft-IIS/10.0`
* **نوع محتوا:** `text/html` (صفحه خوش‌آمدگویی پیش‌فرض IIS 10)

---

## ۷. بررسی فایروال و تحلیل الزامات امنیتی (Firewall & Security Analysis)

### الف) وضعیت فعلی پروفایل‌های Windows Firewall:
* هر سه پروفایل `Domain`، `Private` و `Public` در حالت فعال (`Enabled: True`) هستند و رفتار پیش‌فرض ترافیک ورودی مسدودسازی (`BlockInbound`) است.

### ب) وضعیت پورت ۸۰:
* به هنگام نصب IIS، ویندوز به صورت پیش‌فرض قانون `IIS-WebServerRole-HTTP-In-TCP` را اضافه کرده است که محدوده آدرس مبدا آن به صورت پیش‌فرض روی `RemoteAddress: Any` قرار دارد.
* **مطابق دستور صریح کاربر (عدم تغییر قوانین فایروال بدون تایید مستقل):** این قانون تا زمان اخذ تایید تغییر داده نشد.
* **پیشنهاد امنیتی محدودسازی به شبکه داخلی:**  
  جهت تحقق الزام «طراحی پورت ۸۰ فقط در محدوده شبکه داخلی»، دستور زیر برای تایید و اجرا در گام بعدی پیشنهاد می‌شود:
  ```powershell
  Set-NetFirewallRule -Name "IIS-WebServerRole-HTTP-In-TCP" -RemoteAddress "192.168.1.0/24"
  ```

### ج) ثبت رسمی ریسک امنیتی ارتباط HTTP بدون TLS (Security Risk Register):
> [!WARNING]
> **ریسک ارتباط HTTP بدون رمزنگاری (Plaintext HTTP Risk):**  
> 1. **افشای اعتبارسنجی و توکن‌ها:** در ارتباط بدون TLS، کوکی‌های نشست (Session Cookies) و هدرهای احراز هویت در ترافیک شبکه به صورت متن خام منتقل شده و در صورت شنود شبکه داخلی (Sniffing) یا حملات ARP Spoofing قابل سرقت هستند.  
> 2. **آسیب‌پذیری در برابر مرد میانی (MitM):** مهاجمین در شبکه داخلی قادر به دستکاری پاسخ‌های وب‌سرور یا تزریق اسکریپت خواهند بود.  
> 3. **راهکار و برنامه ارتقا (HTTPS Roadmap):** به محض تولید گواهینامه معتبر داخلی توسط ساختار سازمانی، بایندینگ پورت ۴۴۳ در IIS اضافه شده و از طریق ماژول URL Rewrite کلیه درخواست‌های پورت ۸۰ با کد ۳۰۱ به پورت ۴۴۳ هدایت خواهند شد.

---

## ۸. خطاکردها و هشدارهای سیستمی (Errors & Warnings)

* **رویدادهای وب‌سرور (IIS/WAS):** **۰ خطا و ۰ هشدار**. هیچ لاگ خطایی در Event Viewer مربوط به وب‌سرور، دات‌نت یا ماژول‌های هاستینگ ثبت نشده است.
* **امضای پکیج‌ها:** هر دو نصاب با موفقیت اعتبارسنجی امضای دیجیتال مایکروسافت را پشت سر گذاشتند.
* **فایل‌های موقت:** پس از اتمام نصب، کلیه فایل‌های موقت نصاب از مسیر `C:\Windows\Temp` حذف شدند.

---

## ۹. آمادگی برای استقرار CRM (Readiness for CRM Deployment)

سرور `CRM` از نظر زیرساخت وب‌سرور و ران‌تایم‌ها **۱۰۰٪ آماده** است:
1. وب‌سرور IIS 10 با تمام قابلیت‌های فشرده‌سازی، وب‌سوکت و احراز هویت ویندوز فعال است.
2. ران‌تایم کامل .NET 8 و موتور اجرای آن در IIS (AspNetCoreModuleV2) در سلامت کامل قرار دارند.
3. ماژول URL Rewrite جهت پشتیبانی از مسیریابی SPA فرانت‌اند React آماده است.
4. ایزوله‌سازی کامل رعایت شد؛ هیچ تغییری در سرور PDC (`192.168.1.250`) صورت نگرفت، دیتابیس `ParkPalaceCRM_DB` ایجاد نشد و هیچ رمزی در فایل‌های مخزن یا گزارش ثبت نگردید.

---

## ۱۰. توقف اجباری و قطعی (Mandatory Stop)

مطابق دستور مؤکد کارفرما:
* عملیات آماده‌سازی وب‌سرور در این مرحله **متوقف** گردید.
* هیچ دیتابیسی ایجاد نشد، مایگریشن اجرا نگردید و فایل‌های CRM منتشر نشدند.
* **ادامه مراحل (شامل ایجاد دیتابیس `ParkPalaceCRM_DB`، انتشار آزمایشی API و وب و محدودسازی رنج شبکه فایروال) اکیداً منوط به صدور مجوز جداگانه از سوی کارفرما است.**
