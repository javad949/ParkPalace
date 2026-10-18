# دستورالعمل استقرار عملیاتی و تحلیل ریسک سرور دامین — فاز ۸
# ParkPalace Procurement CRM — Phase 8 Production Deployment Runbook

**پروژه:** سامانه جامع تدارکات و خرید پارک پالاس (ParkPalace Procurement CRM)  
**فاز:** فاز ۸ — آمادگی استقرار عملیاتی و تحلیل ریسک معماری  
**تاریخ انتشار:** ۲۶ مهر ۱۴۰۵ (2026-10-18)  
**محیط هدف:** شبکه سازمانی کارخانه (`sahar.loc`)  
**نام دامنه هدف:** `crm.sahar.loc` | **پورت هدف:** `80` (HTTP) | **مسیر فایل‌ها:** `D:\ParkPlaceCRM`  
**آدرس IP پیشنهادی:** `192.168.1.248` | **ماشین مجازی:** `CRM` (طرح تک‌سرور VM-01)  
**وضعیت اجرا:** **پیشنهادی و آماده‌سازی‌شده — اکیداً بدون اجرا در سرور PDC (Mandatory Stop)**  
**نسخه سند:** ۲.۰.۰ (بازبینی‌شده بر اساس معماری تک‌ماشین مجازی)  

---

## ۱. ارزیابی معماری و تحلیل ریسک استقرار روی Domain Controller (PDC)

> [!CAUTION]
> **توصیه مؤکد و حیاتی معمار ارشد سیستم:**  
> سرور `pdc.sahar.loc` با آدرس IP `192.168.1.250`، **کنترل‌کننده اصلی دامین (Primary Domain Controller)** سازمان سحر است. اجرای وب‌اپلیکیشن تجاری (شامل IIS، وب‌سوکت‌های بلادرنگ SignalR، ورکر پس‌زمینه و پایگاه داده SQL Server) بر روی سرور PDC با خطرات امنیتی و عملیاتی بسیار شدیدی همراه است و کاملاً مردود است.

### دلایل فنی ضرورت استفاده از تک‌سرور مجزا (Dedicated Single VM - VM-01):
1. **سطح حمله امنیتی (Attack Surface):** هرگونه آسیب‌پذیری روز-صفر احتمالی در وب‌سرور یا وب‌سایت، دسترسی اولیه را روی کنترل‌کننده کل امنیت سازمان (PDC) فراهم می‌کند که می‌تواند منجر به تسخیر کامل دامین گردد.
2. **تداخل منابع پردازشی با سرویس‌های دامین:** پردازش‌های سنگین محاسبات گزارش‌گیری، فیلترینگ و موتور SQL Server موجب تسخیر حافظه و تداخل با سرویس‌های حیاتی `lsass.exe`, `Active Directory`, `Kerberos KDC` و `DNS` می‌شود.
3. **ریسک راه‌اندازی مجدد و اختلال شبکه:** خطاهای احتمالی یا نیاز به ریستارت کردن IIS یا سرور اپلیکیشن دسترسی کل کلاینت‌های سازمان به لاگین دامین، فایل‌شیرینگ و احراز هویت را برای دقایقی قطع می‌نماید.

**راهکار مصوب کارفرما (طرح VM-01):**  
تخصیص یک ماشین مجازی مستقل در بستر VMware ESXi با نام `CRM`، آدرس IP `192.168.1.248`، منابع ۸ هسته vCPU، ۳۲ گیگابایت RAM، ۳۰۰ گیگابایت دیسک و کارت شبکه VMXNET3، جهت میزبانی همزمان IIS 10 و SQL Server 2022 (با سقف حافظه ۲۰ گیگابایت).

---

## ۲. پیش‌نیازهای نرم‌افزاری و زیرساختی سرور استقرار (Prerequisites)

پیش از اقدام به استقرار نهایی، سرور هدف باید واجد مشخصات زیر باشد:
- **سیستم‌عامل:** Windows Server 2022 Standard (64-bit).
- **موتور دیتابیس:** SQL Server 2022 Standard/Enterprise (با سقف حافظه ۲۰ گیگابایت و ارتباط لوکال).
- **وب‌سرور IIS 10:** با فعال بودن ویژگی‌های:
  - `Windows Authentication`
  - `WebSocket Protocol` (جهت کارکرد پاپ‌آپ بلادرنگ SignalR روی پورت ۸۰)
  - `HTTP Activation`
- **پکیج میزبانی دات‌نت:** نصب **ASP.NET Core 8 Hosting Bundle** (نسخه 8.0.x).
- **ماژول URL Rewrite:** نصب نسخه ۲.۱ رسمی مایکروسافت.
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

### گام ۳: ایجاد وب‌سایت در IIS با پورت ۸۰
```powershell
New-WebSite -Name "ParkPalaceCRM" `
    -Port 80 `
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

### گام ۵: ایجاد پایگاه داده لوکال و تنظیم سقف حافظه در SQL Server 2022
در پایگاه داده `ParkPalaceCRM_DB` روی همین ماشین، سقف حافظه و دسترسی‌های کاربر سرویس تنظیم می‌شود:
```sql
-- تنظیم سقف حافظه ۲۰ گیگابایت برای بافر پول SQL Server
EXEC sys.sp_configure N'show advanced options', 1;
RECONFIGURE WITH OVERRIDE;
EXEC sys.sp_configure N'max server memory (MB)', 20480;
RECONFIGURE WITH OVERRIDE;
GO

USE [ParkPalaceCRM_DB];
CREATE USER [sahar\svc_crm] FOR LOGIN [sahar\svc_crm];
ALTER ROLE [db_datareader] ADD MEMBER [sahar\svc_crm];
ALTER ROLE [db_datawriter] ADD MEMBER [sahar\svc_crm];
GRANT EXECUTE TO [sahar\svc_crm];
GO
```

### گام ۶: پیکربندی ورکر به عنوان Windows Service
اجرای ورکر پس‌زمینه با ابزار Windows Service Controller:
```powershell
sc.exe create "ParkPalaceCRMWorker" `
    binPath= "D:\ParkPlaceCRM\worker\ParkPalace.Worker.exe" `
    start= auto `
    obj= "sahar\svc_crm" `
    password= "YourSecurePasswordHere"
sc.exe start "ParkPalaceCRMWorker"
```

### گام ۷: پیکربندی فایروال سرور (Windows Defender Firewall)
باز کردن دسترسی فقط به پورت ۸۰ از مبدأ ساب‌نت اداری و مسدودسازی دسترسی مستقیم خارجی به پورت ۱۴۳۳ SQL:
```powershell
# باز کردن پورت 80 وب
New-NetFirewallRule -DisplayName "ParkPalace CRM Inbound HTTP Port 80" `
    -Direction Inbound `
    -LocalPort 80 `
    -Protocol TCP `
    -Action Allow `
    -RemoteAddress "192.168.1.0/24"

# اطمینان از مسدود بودن دسترسی خارجی به SQL Server
New-NetFirewallRule -DisplayName "Block Inbound Remote SQL 1433" `
    -Direction Inbound `
    -LocalPort 1433 `
    -Protocol TCP `
    -Action Block
```

### گام ۸: تنظیم رکورد DNS در کنترل‌کننده دامین
افزودن رکورد A برای `crm.sahar.loc` با اشاره به ماشین `CRM`:
```powershell
Add-DnsServerResourceRecordA -Name "crm" `
    -ZoneName "sahar.loc" `
    -IPv4Address "192.168.1.248" `
    -CreatePtr

# ثبت SPN برای پورت 80 جهت Kerberos
setspn -S "HTTP/crm.sahar.loc" "sahar\svc_crm"
setspn -S "HTTP/crm" "sahar\svc_crm"
```

---

## ۴. چک‌لیست اعتبارسنجی پس از استقرار (Smoke Verification)

1. باز کردن آدرس `http://crm.sahar.loc` در مرورگر کلاینت عضو دامین و اطمینان از لاگین خودکار با NTLM/Kerberos.
2. استعلام اندپوینت `http://crm.sahar.loc/api/health` و دریافت پاسخ `{"status":"Healthy"}`.
3. بررسی پاپ‌آپ زنده SignalR با شبیه‌سازی یک تماس تلفنی ورودی روی پورت ۸۰.
4. ممیزی مصرف رم و اطمینان از عدم تجاوز SQL Server 2022 از سقف ۲۰ گیگابایت.
5. بررسی صحت پشتیبان‌گیری دیتابیس به مسیر شبکه خارج از سرور (`\\nas.sahar.loc\backups\CRM`).
