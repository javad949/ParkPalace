# گزارش نهایی اجرای عملیاتی استقرار آزمایشی سامانه ParkPalace CRM — نگارش v1.0.0
# ParkPalace CRM — Pilot Deployment Execution Report v1.0.0

**پروژه:** سامانه جامع تدارکات و مدیریت ارتباط با تأمین‌کنندگان پارک پالاس (ParkPalace CRM)  
**سند:** [ParkPalace_Pilot_Deployment_Execution_Report.md](file:///d:/Antigravity/ParkPlace/docs/ParkPalace_Pilot_Deployment_Execution_Report.md)  
**تاریخ اجرا:** ۱ آبان ۱۴۰۵ (23 اکتبر 2026)  
**نسخه استقرار:** `v1.0.0-pilot` (بسته نهایی [release_v1.0.0_pilot_final](file:///d:/Antigravity/ParkPlace/dist/release_v1.0.0_pilot_final/))  
**سرور میزبان:** ماشین اختصاصی `CRM` (`192.168.1.248`) عضو دامین `sahar.loc`  
**آدرس دسترسی نهایی کاربران شبکه:** [https://crm.sahar.loc](https://crm.sahar.loc/)  
**وضعیت عملیاتی:** **OPERATIONAL & VERIFIED (استقرار موفق و ۱۰۰٪ اعتبارسنجی‌شده)**  

---

## ۱. خلاصه اجرایی و مراحل طی‌شده

پیرو تأییدیه صریح و مجوز اجرای کارفرما (`انجام شود`)، کلیه مراحل عملیاتی ۸ گانه مصوب طرح استقرار آزمایشی به صورت فازبندی‌شده، با توقف‌های ایمنی خودکار و بدون افشا یا ذخیره اطلاعات محرمانه بر روی زیرساخت سرور `CRM` با موفقیت کامل اجرا و اعتبارسنجی گردید:

| مرحله | شرح اقدام اجرایی | ابزار / مسیر اجرا | وضعیت عملیاتی | شواهد و نتایج آزمون |
| :---: | :--- | :--- | :---: | :--- |
| **۱** | **بررسی پیش‌نیازها و پشتیبان‌گیری** | `Deploy-ParkPalaceCrm.ps1 -Action ValidateOnly` | **موفق (PASS)** | تأیید سرویس‌های IIS، ماژول‌های ANCM v2، URL Rewrite، دیتابیس SQL Server 2022 و گواهینامه معتبر وب. ایجاد پشتیبان کامل در `D:\ParkPlaceCRM_Backups\`. |
| **۲** | **ایجاد و مایگریشن دیتابیس** | `ef_migrations_bundle_idempotent.sql` با فلگ `-I` | **موفق (PASS)** | تأیید ۵۰ جدول و ۵ مایگریشن کامل در دیتابیس مستقل `ParkPalaceCRM`. |
| **۳** | **تفکیک دسترسی حداقل اختیارات** | لاگین `[IIS APPPOOL\ParkPalaceCRM]` | **موفق (PASS)** | انتساب کاربر `crm_app_user` صرفاً به نقش‌های `db_datareader` و `db_datawriter` بدون دسترسی DDL یا sa. |
| **۴** | **استقرار باینری‌های جدید Web و API** | انتقال بسته [release_v1.0.0_pilot_final](file:///d:/Antigravity/ParkPlace/dist/release_v1.0.0_pilot_final/) | **موفق (PASS)** | استقرار فرانت‌اند در `D:\ParkPlaceCRM\web` و بک‌اند در `D:\ParkPlaceCRM\api` به همراه اعمال مجوزهای NTFS با `icacls`. |
| **۵** | **پیکربندی IIS و بایندینگ HTTPS** | AppPool `ParkPalaceCRM` و ساب‌اپلیکیشن `/api` | **موفق (PASS)** | فعال‌سازی `AlwaysRunning`، معماری InProcess، ویندوز آتنتیکیشن برای هندلر Negotiate و بایندینگ پورت ۴۴۳ با کلید `27D9A486A30F5BFDD27B73F334299FE838F750F9`. |
| **۶** | **محدودسازی سخت‌گیرانه فایروال** | قانون `ParkPalace-CRM-Web-Inbound` | **موفق (PASS)** | مجاز بودن پورت‌های ۸۰ و ۴۴۳ منحصراً برای ساب‌نت داخلی `192.168.1.0/24` (رد صریح `RemoteAddress = Any`). |
| **۷** | **آزمون سلامت سامانه (Health Check)** | فراخوانی امن `/api/health` با TLS | **موفق (PASS)** | دریافت `HTTP 200 OK` و وضعیت کاملاً سالم (`Healthy`، دیتابیس متصل، سپیدار در حالت آزمایشی فاز ۴ و ۵). |
| **۸** | **ممیزی رکورد DNS داخلی در PDC** | استعلام از سرور `192.168.1.250` | **موفق (PASS)** | رکورد A دامین `crm.sahar.loc` از پیش به درستی به `192.168.1.248` اشاره دارد و تفکیک نام بدون وقفه فعال است. |

---

## ۲. مستندات شواهد آزمون‌های عملیاتی (Verification Evidence)

### ۲.۱. آزمون لایه وب و تحویل SPA Frontend
درخواست مستقیم به وب‌سایت اصلی:
```http
HTTP/1.1 200 OK
Content-Type: text/html
Server: Microsoft-IIS/10.0
Date: Fri, 23 Oct 2026 08:18:09 GMT

<!DOCTYPE html>
<html lang="fa" dir="rtl">
...
```

### ۲.۲. آزمون سلامت API و اتصال پایگاه داده (`/api/health`)
پاسخ دریافتی زنده از سرور پس از استقرار:
```json
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Server: Microsoft-IIS/10.0
Date: Fri, 23 Oct 2026 08:18:09 GMT

{
  "status": "Healthy",
  "service": "ParkPalace Procurement CRM API",
  "version": "1.0.0",
  "serverTimeUtc": "2026-10-23T08:18:09.910438Z",
  "databaseConnected": true,
  "sepidarIntegrationStatus": "Not configured (Fixture Mode active - Phase 4 & 5)"
}
```

### ۲.۳. ریدایرکت خودکار ترافیک HTTP به HTTPS (پورت ۸۰ به ۴۴۳)
```http
HTTP/1.1 301 Moved Permanently
Content-Type: text/html; charset=UTF-8
Location: https://crm.sahar.loc/
Server: Microsoft-IIS/10.0
```

### ۲.۴. تنظیمات اعمال‌شده روی فایروال ویندوز سرور CRM
```powershell
DisplayName   : ParkPalace CRM Web & API (HTTPS/HTTP)
Enabled       : True
Direction     : Inbound
Action        : Allow
Protocol      : TCP
LocalPort     : {80, 443}
RemoteAddress : 192.168.1.0/24 (Strictly restricted to internal LAN)
```

### ۲.۵. وضعیت استعلام رکورد DNS سازمانی از سرور PDC (`192.168.1.250`)
```powershell
Name       : CRM.sahar.loc
Type       : A
IPAddress  : 192.168.1.248
DNS Server : 192.168.1.250 (PDC.sahar.loc)
TTL        : 1200
```

---

## ۳. اصلاحات اعمال‌شده در بسته نرم‌افزاری نسبت به نسخه پیشین

1. **اعمال قطعی کنترل دسترسی مبتنی بر نقش (RBAC):**
   * سایدبار ([Sidebar.tsx](file:///d:/Antigravity/ParkPlace/src/parkpalace-web/src/components/Sidebar.tsx)) و گارد سراسری مسیرها ([App.tsx](file:///d:/Antigravity/ParkPlace/src/parkpalace-web/src/App.tsx)) اصلاح شدند؛ هر کاربر تنها به منوها و صفحات مجاز نقش خود دسترسی دارد و در صورت تلاش برای ورود به مسیر غیرمجاز، صفحه `403 Forbidden` با دکمه بازگشت به داشبورد نمایش داده می‌شود.
2. **اصلاح نشانگر وضعیت سپیدار:**
   * برچسب قدیمی «پیکربندی نشده (فاز ۴)» حذف گردید و در هدر ([Header.tsx](file:///d:/Antigravity/ParkPlace/src/parkpalace-web/src/components/Header.tsx)) نشانگر پویا به صورت **«سپیدار: حالت آزمایشی (شبیه‌ساز فاز ۴ و ۵)»** نمایش می‌یابد.
3. **تطابق مسیر مسیریابی ساب‌اپلیکیشن `/api` در IIS:**
   * قاعده بازنویسی `PrependApiPrefix` در فایل `D:\ParkPlaceCRM\api\web.config` تنظیم گردید تا درخواست‌های ارسالی از فرانت‌اند بدون نیاز به پیشوند مضاعف مستقیماً به کنترلرهای ASP.NET Core هدایت شوند.
4. **پاکسازی کامل فایل‌های موقت:**
   * کلیه فایل‌های موقت استقرار (استیجینگ و آرشیو زیپ) از مسیر `C:\Windows\Temp\` سرور حذف گردید.

---

## ۴. وضعیت ریسک‌های حل‌نشده و توصیه‌های بهره‌برداری

* **ریسک افشای اعتبارنامه‌های تاریخی:** به دلیل عدم تمایل کارفرما به تعویض رمزها، این مورد به عنوان یک ریسک حل‌نشده ثبت شده است. در عملیات خودکار حاضر، هیچ‌گونه رمز عبوری در کدها یا مستندات ثبت نگردید.
* **اعتماد به گواهینامه در مرورگر کاربران:** کلید عمومی [crm-web.cer](file:///d:/Antigravity/ParkPlace/dist/release_v1.0.0_pilot_final/ssl/crm-web.cer) در پوشه `ssl` بسته انتشار قرار دارد. جهت حذف هشدار امنیتی مرورگر در کلاینت‌های شبکه داخلی، توصیه می‌شود این فایل از طریق GPO دامین در مخزن `Trusted Root Certification Authorities` سیستم‌های کاربران نصب شود.
* **آغاز فاز پایلوت:** سامانه هم‌اکنون به طور کامل آماده استفاده کاربران شبکه داخلی در نشانی **[https://crm.sahar.loc](https://crm.sahar.loc)** می‌باشد.
