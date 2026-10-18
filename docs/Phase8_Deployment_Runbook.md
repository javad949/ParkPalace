# دستورالعمل استقرار عملیاتی و تحلیل ریسک سرور دامین — فاز ۸
# ParkPalace Procurement CRM — Phase 8 Production Deployment Runbook

**پروژه:** سامانه جامع تدارکات و خرید پارک پالاس (ParkPalace Procurement CRM)  
**فاز:** فاز ۸ — آمادگی استقرار عملیاتی و تحلیل ریسک معماری  
**تاریخ انتشار:** ۲۶ مهر ۱۴۰۵ (2026-10-18)  
**محیط هدف:** شبکه سازمانی کارخانه (`sahar.loc`)  
**نام دامنه هدف:** `crm.sahar.loc` | **پورت هدف:** `8888` | **مسیر فایل‌ها:** `D:\ParkPlaceCRM`  
**وضعیت اجرا:** **پیشنهادی و آماده‌سازی‌شده — اکیداً بدون اجرا در سرور PDC (Mandatory Stop)**  
**نسخه سند:** ۱.۰.۰  

---

## ۱. ارزیابی معماری و تحلیل ریسک استقرار روی Domain Controller (PDC)

> [!CAUTION]
> **توصیه مؤکد و حیاتی معمار ارشد سیستم:**  
> سرور `pdc.sahar.loc` با آدرس IP `192.168.1.250`، **کنترل‌کننده اصلی دامین (Primary Domain Controller)** سازمان سحر است. اجرای یک وب‌اپلیکیشن تجاری (شامل IIS، وب‌سوکت‌های بلادرنگ SignalR، ورکر پس‌زمینه و دسترسی شبکه باز به پورت ۸۸۸۸) بر روی سرور PDC با خطرات امنیتی و عملیاتی بسیار شدیدی همراه است.

### دلایل فنی ضرورت استفاده از سرور مجزا (Dedicated Application Server):
1. **سطح حمله امنیتی (Attack Surface):** هرگونه آسیب‌پذیری روز-صفر احتمالی در وب‌سرور یا وب‌سایت، دسترسی اولیه را روی کنترل‌کننده کل امنیت سازمان (PDC) فراهم می‌کند که می‌تواند منجر به تسخیر کامل دامین گردد.
2. **تداخل منابع پردازشی با سرویس‌های دامین:** پردازش‌های سنگین محاسبات گزارش‌گیری، فیلترینگ و استریم صوتی ممکن است منجر به مصرف حافظه و تداخل با سرویس‌های حیاتی `lsass.exe`, `Active Directory`, `Kerberos Key Distribution Center` و `DNS` شود.
3. **ریسک راه‌اندازی مجدد و اختلال شبکه:** خطاهای احتمالی یا نیاز به ریستارت کردن IIS یا سرور اپلیکیشن ممکن است دسترسی کل کلاینت‌های سازمان به لاگین دامین، فایل‌شیرینگ و احراز هویت را برای دقایقی قطع نماید.

**راهکار پیشنهادی استاندارد:**  
تخصیص یک ماشین مجازی یا سرور اختصاصی مجزا (مانند `app-crm.sahar.loc` یا `192.168.1.246/248`) عضو دامین به عنوان وب‌سرور CRM، در حالی که پایگاه داده می‌تواند روی یک SQL Server استاندارد میزبانی شود.

---

## ۲. پیش‌نیازهای نرم‌افزاری و زیرساختی سرور استقرار (Prerequisites)

پیش از اقدام به استقرار نهایی، سرور هدف باید واجد مشخصات زیر باشد:
- **سیستم‌عامل:** Windows Server 2019 یا 2022 نسخه Standard یا Datacenter.
- **وب‌سرور IIS 10:** با فعال بودن ویژگی‌های:
  - `Windows Authentication`
  - `WebSocket Protocol` (جهت کارکرد پاپ‌آپ بلادرنگ SignalR)
  - `HTTP Activation`
- **پکیج میزبانی دات‌نت:** نصب **ASP.NET Core 8 Hosting Bundle** (نسخه 8.0.x).
- **ماژول URL Rewrite:** نصب نسخه ۲.۱ رسمی مایکروسافت.
- **گواهینامه SSL/TLS:** گواهینامه معتبر داخلی برای نام دامنه `crm.sahar.loc`.
- **حساب کاربری سرویس دامین:** حساب اختصاصی بدون دسترسی ادمین دامین (مانند `sahar\svc_crm`).

---

## ۳. مراحل گام‌به‌گام استقرار در IIS (Proposed Deployment Steps)

### گام ۱: آماده‌سازی ساختار دایرکتوری در سرور
ایجاد مسیرهای فیزیکی در درایو `D:` سرور میزبان:
```powershell
New-Item -ItemType Directory -Path "D:\ParkPlaceCRM\api" -Force
New-Item -ItemType Directory -Path "D:\ParkPlaceCRM\web" -Force
New-Item -ItemType Directory -Path "D:\ParkPlaceCRM\worker" -Force
New-Item -ItemType Directory -Path "D:\ParkPlaceCRM\recordings" -Force
New-Item -ItemType Directory -Path "D:\ParkPlaceCRM\logs" -Force
```

### گام ۲: ایجاد Application Pool در IIS
```powershell
Import-Module WebAdministration
New-WebAppPool -Name "ParkPalaceCRM_AppPool"
Set-ItemProperty "IIS:\AppPools\ParkPalaceCRM_AppPool" -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty "IIS:\AppPools\ParkPalaceCRM_AppPool" -Name "processModel.identityType" -Value "SpecificUser"
Set-ItemProperty "IIS:\AppPools\ParkPalaceCRM_AppPool" -Name "processModel.userName" -Value "sahar\svc_crm"
Set-ItemProperty "IIS:\AppPools\ParkPalaceCRM_AppPool" -Name "processModel.password" -Value "P@ssw0rdSecure!"
Set-ItemProperty "IIS:\AppPools\ParkPalaceCRM_AppPool" -Name "recycling.periodicRestart.time" -Value "04:00:00"
```

### گام ۳: ایجاد وب‌سایت در IIS با پورت ۸۸۸۸
```powershell
New-WebSite -Name "ParkPalaceCRM" `
    -Port 8888 `
    -PhysicalPath "D:\ParkPlaceCRM\api" `
    -ApplicationPool "ParkPalaceCRM_AppPool" `
    -HostHeader "crm.sahar.loc"
```

### گام ۴: پیکربندی احراز هویت Windows و رد دسترسی ناشناس
```powershell
Set-WebConfigurationProperty -Filter "/system.webServer/security/authentication/anonymousAuthentication" `
    -Name "enabled" -Value "False" -PSPath "IIS:\Sites\ParkPalaceCRM"

Set-WebConfigurationProperty -Filter "/system.webServer/security/authentication/windowsAuthentication" `
    -Name "enabled" -Value "True" -PSPath "IIS:\Sites\ParkPalaceCRM"
```

### گام ۵: دسترسی پایگاه داده در SQL Server (سیاست کمترین سطح دسترسی)
در پایگاه داده `ParkPalaceCRM_DB`، حساب سرویس صرفاً دسترسی‌های خواندن/نوشتن را دریافت می‌کند و از دادن نقش `sysadmin` یا `db_owner` اکیداً خودداری می‌شود:
```sql
USE [ParkPalaceCRM_DB];
CREATE USER [sahar\svc_crm] FOR LOGIN [sahar\svc_crm];
ALTER ROLE [db_datareader] ADD MEMBER [sahar\svc_crm];
ALTER ROLE [db_datawriter] ADD MEMBER [sahar\svc_crm];
GRANT EXECUTE TO [sahar\svc_crm];
```

### گام ۶: پیکربندی ورکر به عنوان Windows Service
اجرای ورکر پس‌زمینه با ابزار Windows Service Controller:
```powershell
sc.exe create "ParkPalaceCRMWorker" `
    binPath= "D:\ParkPlaceCRM\worker\ParkPalace.Worker.exe" `
    start= auto `
    obj= "sahar\svc_crm" `
    password= "P@ssw0rdSecure!"
sc.exe start "ParkPalaceCRMWorker"
```

### گام ۷: پیکربندی فایروال سرور (Windows Defender Firewall)
باز کردن دسترسی فقط به پورت ۸۸۸۸ از مبدأ ساب‌نت اداری:
```powershell
New-NetFirewallRule -DisplayName "ParkPalace CRM Inbound Port 8888" `
    -Direction Inbound `
    -LocalPort 8888 `
    -Protocol TCP `
    -Action Allow `
    -RemoteAddress "192.168.1.0/24"
```

### گام ۸: تنظیم رکورد DNS در کنترل‌کننده دامین
افزودن رکورد A برای `crm.sahar.loc`:
```powershell
Add-DnsServerResourceRecordA -Name "crm" `
    -ZoneName "sahar.loc" `
    -IPv4Address "192.168.1.246" `
    -CreatePtr
```

---

## ۴. چک‌لیست اعتبارسنجی پس از استقرار (Smoke Verification)

1. باز کردن آدرس `http://crm.sahar.loc:8888` در مرورگر کلاینت عضو دامین و اطمینان از لاگین خودکار با NTLM/Kerberos.
2. استعلام اندپوینت `http://crm.sahar.loc:8888/api/health` و دریافت پاسخ `{"status":"Healthy"}`.
3. بررسی پاپ‌آپ زنده با شبیه‌سازی یک تماس تلفنی.
4. بررسی عدم ثبت هرگونه خطای سطح بالا در Event Viewer ویندوز (`Application Log`).
