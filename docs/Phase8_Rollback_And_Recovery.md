# راهنمای جامع بازگشت به عقب، بازیابی و پشتیبان‌گیری — فاز ۸
# ParkPalace Procurement CRM — Phase 8 Rollback, Recovery & Backup Guide

**پروژه:** سامانه جامع تدارکات و خرید پارک پالاس (ParkPalace Procurement CRM)  
**فاز:** فاز ۸ — آمادگی استقرار عملیاتی و بازیابی از بحران (Disaster Recovery & Rollback)  
**تاریخ انتشار:** ۲۶ مهر ۱۴۰۵ (2026-10-18)  
**محیط اجرا:** محیط ایزوله پیش‌استقرار (`D:\ParkPlace`)  
**نسخه سند:** ۱.۰.۰  

---

## ۱. اهداف و راهبرد تاب‌آوری در برابر بحران (Resilience Strategy)

در استقرار سامانه‌های سازمانی حساس نظیر تدارکات و حسابداری، وجود رویه‌های مدون، تست‌شده و بازتولیدپذیر جهت بازگشت به نقطه امن قبلی (Rollback) در صورت بروز هرگونه اختلال پیش‌بینی‌نشده یک ضرورت قطعی است.  
این سند رویه‌های بازگشت دیتابیس، بازیابی فایل‌های اجرایی وب‌سرور، و احیای سرویس‌های پس‌زمینه را تشریح می‌نماید.

---

## ۲. بازگشت‌پذیری مایگریشن‌های پایگاه داده (EF Core Migration Reversibility)

کلیه مایگریشن‌های ایجادشده در لایه `ParkPalace.Infrastructure` دارای متدهای معکوس `Down()` متقارن بوده و امکان بازگشت کامل اسکیمای پایگاه داده به هر فاز دلخواه بدون باقی ماندن جداول یتیم یا محدودیت‌های نامعتبر را فراهم می‌آورند.

### فهرست تاریخچه مایگریشن‌ها و زنجیره بازگشت:
1. `20261017183302_InitialCrmIdentityAndAudit`: پایگاه داده هویتی اولیه و لاگ ممیزی.
2. `20261017190643_Phase3_CatalogAndSuppliers`: کاتالوگ مواد اولیه، واحدها، دسته‌ها و تأمین‌کنندگان.
3. `20261017200046_Phase4_InventoryAndWarehouse`: انبارها، موجودی، جابجایی‌ها و چک‌پوینت‌های سپیدار.
4. `20261017204424_Phase5_ProcurementAndThreeWayMatching`: سفارش‌های خرید، رسید انبار، فاکتورها و تطبیق.
5. `20261018042000_Phase6_TelephonyAndCallManagement`: جداول تماس، انتساب داخلی‌ها، لاگ‌های ممیزی تماس و یادداشت‌ها.

### فرمان بازگشت به فاز پیشین (Rollback Command):
جهت بازگشت پایگاه داده به نسخه قبل از یک فاز مشخص، دستور زیر در خط فرمان اجرا می‌شود:
```bash
# بازگشت به انتهای فاز ۵ (حذف ساختارهای تلفنی فاز ۶):
dotnet ef database update 20261017204424_Phase5_ProcurementAndThreeWayMatching --project src/ParkPalace.Infrastructure --startup-project src/ParkPalace.Api

# بازگشت به انتهای فاز ۴ (حذف ساختارهای تدارکات و تطبیق):
dotnet ef database update 20261017200046_Phase4_InventoryAndWarehouse --project src/ParkPalace.Infrastructure --startup-project src/ParkPalace.Api
```

---

## ۳. سیاست و اسکریپت‌های پشتیبان‌گیری و بازیابی پایگاه داده (SQL Server Backup & Restore)

### ۳.۱. پشتیبان‌گیری کامل با Checksum (Full Backup Script)
پیش از هرگونه استقرار نسخه جدید، اجرای یک نسخه پشتیبان کامل الزامی است:
```sql
DECLARE @BackupPath NVARCHAR(500) = N'D:\ParkPlaceCRM\backups\ParkPalaceCRM_DB_PreDeploy_' + 
    CONVERT(NVARCHAR(20), GETDATE(), 112) + N'_' + 
    REPLACE(CONVERT(NVARCHAR(20), GETDATE(), 108), ':', '') + N'.bak';

BACKUP DATABASE [ParkPalaceCRM_DB]
TO DISK = @BackupPath
WITH FORMAT,
     INIT,
     NAME = N'ParkPalaceCRM Full Pre-Deployment Backup',
     SKIP,
     NOREWIND,
     NOUNLOAD,
     COMPRESSION,
     CHECKSUM;

-- صحت‌سنجی فایل پشتیبان
RESTORE VERIFYONLY FROM DISK = @BackupPath WITH CHECKSUM;
PRINT N'Backup completed and verified successfully at: ' + @BackupPath;
```

### ۳.۲. بازیابی اضطراری به نقطه امن (Emergency Restore Procedure)
در صورت بروز نقص در استقرار یا خرابی داده‌ها:
```sql
USE [master];
ALTER DATABASE [ParkPalaceCRM_DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

RESTORE DATABASE [ParkPalaceCRM_DB]
FROM DISK = N'D:\ParkPlaceCRM\backups\ParkPalaceCRM_DB_PreDeploy_Latest.bak'
WITH REPLACE,
     RECOVERY;

ALTER DATABASE [ParkPalaceCRM_DB] SET MULTI_USER;
PRINT N'Database restored and returned to MULTI_USER mode.';
```

---

## ۴. بازگشت سریع فایل‌های اجرایی وب و کلاینت (Blue-Green File Rollback)

جهت امکان بازگشت در کمتر از ۳۰ ثانیه در وب‌سرور IIS:
1. نگهداری ساختار دایرکتوری نسخه‌بندی‌شده در کنار مسیر اصلی:
   - `D:\ParkPlaceCRM\releases\v7.0.0`
   - `D:\ParkPlaceCRM\releases\v8.0.0`
2. وب‌سایت IIS به یک مسیر ثابت یا Junction Point متصل است. در صورت بروز مشکل در نسخه ۸، مسیر فیزیکی وب‌سایت در IIS با یک فرمان به نسخه ۷ تغییر می‌یابد:
```powershell
Set-ItemProperty "IIS:\Sites\ParkPalaceCRM" -Name "physicalPath" -Value "D:\ParkPlaceCRM\releases\v7.0.0\api"
Restart-WebAppPool -Name "ParkPalaceCRM_AppPool"
```

---

## ۵. تاب‌آوری سرویس ورکر و بازیابی ارتباطات قطع‌شده (Worker Recovery)

### ۵.۱. قطعی ارتباط با مرکز تماس ایزابل (Asterisk AMI Disconnect)
- در کلاس `IssabelLiveTelephonyAdapter` و `TelephonyBackgroundService`، مکانیزم اتصال مجدد خودکار با الگوریتم توقف افزایشی (Exponential Backoff: ۳، ۶، ۱۲، ۳۰ ثانیه) پیاده‌سازی شده است.
- پس از اتصال مجدد، شماره‌های در حال زنگ بر اساس لاگ‌های وضعیت بازیابی می‌شوند و نیازی به ریستارت دستی سرویس نیست.

### ۵.۲. توقف همگام‌سازی انبار سپیدار (Idempotent Recovery)
- جدول `crm.InventorySyncCheckpoints` آخرین شناسه و زمان رکورد پردازش‌شده را نگهداری می‌کند.
- در صورت کرش کردن سرور حین همگام‌سازی، جاب بعدی بدون همپوشانی و بدون ایجاد رکوردهای تکراری از محل آخرین چک‌پوینت پردازش را از سر می‌گیرد.
- آزمون `RepeatedInventorySync_IsIdempotent_AndDoesNotDuplicateCheckpoints` در محیط استیجینگ این قابلیت را اثبات نمود.
