-- ==============================================================================
-- ParkPalace CRM - Targeted Data Repair Script: Seed Tables Mojibake Fix
-- Target Database: ParkPalaceCRM
-- Version: v1.0.2-encoding-fix
-- Scope: ONLY Seed tables (Categories: 4, UnitsOfMeasure: 8, Warehouses: 1, Roles: 5, Permissions: 51)
-- Business Data (Parties: 101, Materials: 1712, Inventory: 34772): UNTOUCHED (0 rows modified)
-- ==============================================================================

USE [ParkPalaceCRM];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- ------------------------------------------------------------------------------
-- Step 1: Create Point-in-Time Backup Tables (Isolated Rollback Snapshot)
-- ------------------------------------------------------------------------------
IF OBJECT_ID('dbo.Categories_Backup_PreMojibakeFix', 'U') IS NOT NULL
    DROP TABLE dbo.Categories_Backup_PreMojibakeFix;
SELECT * INTO dbo.Categories_Backup_PreMojibakeFix FROM dbo.Categories;

IF OBJECT_ID('dbo.UnitsOfMeasure_Backup_PreMojibakeFix', 'U') IS NOT NULL
    DROP TABLE dbo.UnitsOfMeasure_Backup_PreMojibakeFix;
SELECT * INTO dbo.UnitsOfMeasure_Backup_PreMojibakeFix FROM dbo.UnitsOfMeasure;

IF OBJECT_ID('dbo.Warehouses_Backup_PreMojibakeFix', 'U') IS NOT NULL
    DROP TABLE dbo.Warehouses_Backup_PreMojibakeFix;
SELECT * INTO dbo.Warehouses_Backup_PreMojibakeFix FROM dbo.Warehouses;

IF OBJECT_ID('dbo.Roles_Backup_PreMojibakeFix', 'U') IS NOT NULL
    DROP TABLE dbo.Roles_Backup_PreMojibakeFix;
SELECT * INTO dbo.Roles_Backup_PreMojibakeFix FROM dbo.Roles;

IF OBJECT_ID('dbo.Permissions_Backup_PreMojibakeFix', 'U') IS NOT NULL
    DROP TABLE dbo.Permissions_Backup_PreMojibakeFix;
SELECT * INTO dbo.Permissions_Backup_PreMojibakeFix FROM dbo.Permissions;

PRINT 'Backup snapshots created successfully: Categories, UnitsOfMeasure, Warehouses, Roles, Permissions.';

-- ------------------------------------------------------------------------------
-- Step 2: Repair Categories (4 Records)
-- ------------------------------------------------------------------------------
UPDATE dbo.Categories 
SET Name = N'آرد و غلات', 
    Description = N'انواع آرد گندم، جو، سبوس و دانه‌های روغنی نانوایی'
WHERE Code = 'CAT-FLOUR';

UPDATE dbo.Categories 
SET Name = N'بهبوددهنده‌ها و مخمر', 
    Description = N'مخمر خشک و تازه، انواع بهبوددهنده‌های تخصصی نان باگت و حجیم'
WHERE Code = 'CAT-IMPROVER';

UPDATE dbo.Categories 
SET Name = N'روغن و چربی‌های قنادی', 
    Description = N'مارگارین، روغن مایع و قنادی، کره'
WHERE Code = 'CAT-FATS';

UPDATE dbo.Categories 
SET Name = N'ملزومات بسته‌بندی', 
    Description = N'کیسه نان، سلفون، کارتن و برچسب قیمت'
WHERE Code = 'CAT-PACKAGING';

-- ------------------------------------------------------------------------------
-- Step 3: Repair UnitsOfMeasure (8 Records)
-- ------------------------------------------------------------------------------
UPDATE dbo.UnitsOfMeasure SET Name = N'کیلوگرم' WHERE Code = 'KG';
UPDATE dbo.UnitsOfMeasure SET Name = N'گرم' WHERE Code = 'GR';
UPDATE dbo.UnitsOfMeasure SET Name = N'تن' WHERE Code = 'TON';
UPDATE dbo.UnitsOfMeasure SET Name = N'لیتر' WHERE Code = 'L';
UPDATE dbo.UnitsOfMeasure SET Name = N'میلی‌لیتر' WHERE Code = 'ML';
UPDATE dbo.UnitsOfMeasure SET Name = N'عدد' WHERE Code = 'COUNT';
UPDATE dbo.UnitsOfMeasure SET Name = N'کیسه ۵۰ کیلویی' WHERE Code = 'BAG50';
UPDATE dbo.UnitsOfMeasure SET Name = N'کارتن/بسته' WHERE Code = 'CARTON';

-- ------------------------------------------------------------------------------
-- Step 4: Repair Warehouses (1 Record)
-- ------------------------------------------------------------------------------
UPDATE dbo.Warehouses 
SET Name = N'انبار مرکزی کارخانه نان', 
    Description = N'انبار اصلی دریافت و ذخیره‌سازی آرد، مخمر، بهبوددهنده‌ها و ملزومات تولید نان و شیرینی'
WHERE Code = 'WH-MAIN';

-- ------------------------------------------------------------------------------
-- Step 5: Repair Roles (5 Records)
-- ------------------------------------------------------------------------------
UPDATE dbo.Roles SET Description = N'مدیر ارشد سیستم - دسترسی کامل به کلیه بخش‌ها و لاگ‌ها' WHERE Name = 'Admin';
UPDATE dbo.Roles SET Description = N'واحد تدارکات و خرید - ثبت سفارش، استعلام قیمت، تعامل با تأمین‌کنندگان' WHERE Name = 'Procurement';
UPDATE dbo.Roles SET Description = N'مدیر ناظر - نظارت بر فرایند خرید، داشبوردها و گزارشات' WHERE Name = 'Manager';
UPDATE dbo.Roles SET Description = N'انباردار - نظارت بر موجودی، اعلام ورود کالا، کسری و مرجوعی' WHERE Name = 'Warehouse';
UPDATE dbo.Roles SET Description = N'واحد مالی و حسابداری - انطباق فاکتورها، مغایرت‌گیری با سپیدار و تسویه' WHERE Name = 'Finance';

-- ------------------------------------------------------------------------------
-- Step 6: Repair Permissions (51 Records)
-- ------------------------------------------------------------------------------
UPDATE dbo.Permissions SET Description = N'مشاهده فهرست تأمین‌کنندگان و مخاطبین' WHERE Code = 'suppliers.view';
UPDATE dbo.Permissions SET Description = N'افزودن و ویرایش اطلاعات پایه تأمین‌کنندگان' WHERE Code = 'suppliers.manage';
UPDATE dbo.Permissions SET Description = N'مشاهده پیشینه قیمت و نرخ‌های خرید تأمین‌کنندگان' WHERE Code = 'suppliers.prices.view';
UPDATE dbo.Permissions SET Description = N'ثبت و بروزرسانی قیمت‌های پیشنهادی تأمین‌کنندگان' WHERE Code = 'suppliers.prices.manage';
UPDATE dbo.Permissions SET Description = N'مشاهده کاتالوگ اقلام و مشخصات فنی مواد اولیه' WHERE Code = 'materials.view';
UPDATE dbo.Permissions SET Description = N'ویرایش کاتالوگ و نگاشت کدهای اقلام' WHERE Code = 'materials.manage';
UPDATE dbo.Permissions SET Description = N'مشاهده موجودی لحظه‌ای و کاردکس انبار' WHERE Code = 'inventory.view';
UPDATE dbo.Permissions SET Description = N'ثبت تعدیلات دستی و تنظیم نقطه سفارش' WHERE Code = 'inventory.manage';
UPDATE dbo.Permissions SET Description = N'مشاهده وضعیت انبارهای تعریف شده' WHERE Code = 'warehouse.view';
UPDATE dbo.Permissions SET Description = N'اعلام وصول محموله و ثبت ورود موقت به انبار' WHERE Code = 'warehouse.receive';
UPDATE dbo.Permissions SET Description = N'مشاهده رسیدهای موقت و دائم انبار' WHERE Code = 'receipts.view';
UPDATE dbo.Permissions SET Description = N'تأیید کیفی، بازرسی و قطعی‌کردن رسیدهای انبار' WHERE Code = 'receipts.manage';
UPDATE dbo.Permissions SET Description = N'مشاهده سفارشات خرید و پیگیری وضعیت ارسال' WHERE Code = 'orders.view';
UPDATE dbo.Permissions SET Description = N'ایجاد و ویرایش پیش‌نویس سفارش خرید' WHERE Code = 'orders.create';
UPDATE dbo.Permissions SET Description = N'صدور قطعی سفارش خرید و ارسال برای تأمین‌کننده' WHERE Code = 'orders.issue';
UPDATE dbo.Permissions SET Description = N'اجرا و مانیتورینگ همگام‌سازی انبار با سپیدار' WHERE Code = 'inventory.sync';
UPDATE dbo.Permissions SET Description = N'ابطال سفارش خرید بدون دریافت کالا' WHERE Code = 'orders.cancel';
UPDATE dbo.Permissions SET Description = N'مشاهده استعلام‌ها و پیش‌فاکتورهای تأمین‌کنندگان' WHERE Code = 'quotes.view';
UPDATE dbo.Permissions SET Description = N'ثبت و مدیریت استعلام قیمت و RFQ' WHERE Code = 'quotes.manage';
UPDATE dbo.Permissions SET Description = N'مشاهده محاسبات و پیشنهادات خرید روزانه' WHERE Code = 'recommendations.view';
UPDATE dbo.Permissions SET Description = N'تنظیم و ویرایش خط‌مشی‌های سفارش‌دهی مواد اولیه' WHERE Code = 'policies.manage';
UPDATE dbo.Permissions SET Description = N'مشاهده وضعیت مالی و فاکتورها' WHERE Code = 'finance.view';
UPDATE dbo.Permissions SET Description = N'تطبیق اسناد سپیدار با سفارشات CRM' WHERE Code = 'finance.reconcile';
UPDATE dbo.Permissions SET Description = N'مشاهده فاکتورهای تأمین‌کنندگان' WHERE Code = 'invoices.view';
UPDATE dbo.Permissions SET Description = N'ثبت و مدیریت فاکتورهای تأمین‌کنندگان' WHERE Code = 'invoices.manage';
UPDATE dbo.Permissions SET Description = N'مشاهده نتایج تطبیق سه‌طرفه و صف مغایرت‌ها' WHERE Code = 'matching.view';
UPDATE dbo.Permissions SET Description = N'تصمیم‌گیری و رفع مغایرت‌های تطبیق سه‌طرفه' WHERE Code = 'matching.resolve';
UPDATE dbo.Permissions SET Description = N'مشاهده تاریخچه تماس‌ها' WHERE Code = 'calls.view';
UPDATE dbo.Permissions SET Description = N'ثبت یادداشت و پیگیری برای تماس‌ها' WHERE Code = 'calls.manage';
UPDATE dbo.Permissions SET Description = N'نظارت مدیریتی و مانیتورینگ تماس‌های کلیه پرسنل' WHERE Code = 'calls.supervise';
UPDATE dbo.Permissions SET Description = N'شنود و استریم فایل‌های صوتی مکالمات ضبط شده' WHERE Code = 'calls.recordings.listen';
UPDATE dbo.Permissions SET Description = N'پیکربندی تنظیمات تلفنی و اتصال به ایزابل' WHERE Code = 'calls.config';
UPDATE dbo.Permissions SET Description = N'مشاهده داشبوردها و گزارش‌های تحلیلی' WHERE Code = 'reports.view';
UPDATE dbo.Permissions SET Description = N'خروجی اکسل و CSV از گزارشات' WHERE Code = 'reports.export';
UPDATE dbo.Permissions SET Description = N'دسترسی به گزارشات اختصاصی تدارکات و خرید' WHERE Code = 'reports.procurement';
UPDATE dbo.Permissions SET Description = N'دسترسی به گزارشات جامع مدیریتی و KPIها' WHERE Code = 'reports.manager';
UPDATE dbo.Permissions SET Description = N'دسترسی به گزارشات موجودی و سلامت انبار' WHERE Code = 'reports.warehouse';
UPDATE dbo.Permissions SET Description = N'دسترسی به گزارشات انطباق مالی و مغایرت‌ها' WHERE Code = 'reports.finance';
UPDATE dbo.Permissions SET Description = N'دسترسی به مانیتورینگ سلامت سیستم و اتصالات' WHERE Code = 'reports.admin';
UPDATE dbo.Permissions SET Description = N'مدیریت کاربران و تخصیص نقش‌ها' WHERE Code = 'admin.users';
UPDATE dbo.Permissions SET Description = N'تنظیمات عمومی و سیستم' WHERE Code = 'admin.settings';
UPDATE dbo.Permissions SET Description = N'مشاهده لاگ‌های امنیتی و رویدادها' WHERE Code = 'admin.audit';

-- ------------------------------------------------------------------------------
-- Step 7: Post-Update Integrity Verification (Zero Mojibake check)
-- ------------------------------------------------------------------------------
DECLARE @MojibakeCount INT = 0;

SELECT @MojibakeCount = @MojibakeCount + COUNT(*) FROM dbo.Categories WHERE Name LIKE N'%Ø%' OR Name LIKE N'%Ù%';
SELECT @MojibakeCount = @MojibakeCount + COUNT(*) FROM dbo.UnitsOfMeasure WHERE Name LIKE N'%Ø%' OR Name LIKE N'%Ù%';
SELECT @MojibakeCount = @MojibakeCount + COUNT(*) FROM dbo.Warehouses WHERE Name LIKE N'%Ø%' OR Name LIKE N'%Ù%';
SELECT @MojibakeCount = @MojibakeCount + COUNT(*) FROM dbo.Roles WHERE Description LIKE N'%Ø%' OR Description LIKE N'%Ù%';

IF @MojibakeCount > 0
BEGIN
    PRINT 'ERROR: Mojibake characters still detected! Rolling back...';
    ROLLBACK TRANSACTION;
END
ELSE
BEGIN
    COMMIT TRANSACTION;
    PRINT 'SUCCESS: Targeted seed repair committed successfully with 0 Mojibake characters remaining.';
END
GO
