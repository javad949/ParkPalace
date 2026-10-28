-- ======================================================================================
-- ParkPalace CRM - Sepidar Data Sampling: Phase 2 Part 3 (Supplementary Candidate Queries)
-- File: scripts/03_sepidar_supplementary_sampling_queries.sql
-- 
-- **************************************************************************************
-- وضعیت فایل: «پیش‌نویس کوئری‌های تکمیلی محلی — آماده برای بررسی، غیرفعال برای اجرا»
-- STATUS: LOCAL SUPPLEMENTARY QUERIES — READY FOR REVIEW, NOT YET EXECUTED
-- **************************************************************************************
--
-- اهداف این بسته تکمیلی:
-- ۱. راستی‌آزمایی موجودی اقلام کیلوگرمی (UnitRef = 1) در سال ۱۰ و انبار ۱ (حداکثر ۲۰ سطر)
-- ۲. استخراج شماره سند (Number) و شناسه برای هر ترکیب Type/IsReturn مشاهده‌شده
--    جهت تطبیق بدون حدس در رابط کاربری نرم‌افزار سپیدار (UI) توسط اپراتور
-- ۳. بدون هرگونه اطلاعات مالی، مبالغ، قیمت‌ها یا شرح اسناد
--
-- تنظیمات ایمنی:
-- - SET LOCK_TIMEOUT 3000 ms; SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
-- - مهلت زمانی هر فرمان (CommandTimeout) باید ۳۰ ثانیه تنظیم شود.
-- ======================================================================================

SET NOCOUNT ON;
SET LOCK_TIMEOUT 3000;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

DECLARE @FiscalYearId INT = 10; -- سال مالی ۱۴۰۵
DECLARE @StockId INT = 1;       -- انبار مواد اولیه


/*
--------------------------------------------------------------------------------------
کوئری ۱: راستی‌آزمایی محاسباتی موجودی اقلام کیلوگرمی (UnitRef = 1)
هدف:
  ۱) بررسی اینکه آیا در اقلام کیلوگرمی نیز Quantity دقیقا برابر (InputQuantity - OutputQuantity) است یا خیر
  ۲) بررسی رفتار ستون‌های رزرو در اقلام وزنی
محدوده: انبار ۱، سال ۱۰، صرفاً اقلام با UnitRef = 1 (حداکثر ۲۰ قلم)
--------------------------------------------------------------------------------------
*/
SELECT TOP 20
    ItemRef,
    StockRef,
    UnitRef,
    InputQuantity,
    OutputQuantity,
    Quantity AS StoredQuantity,
    (InputQuantity - OutputQuantity) AS NetDifference,
    SaleQuantity,
    SaleWithReserveQuantity,
    FeedFromClosingOperation
FROM INV.ItemStockSummary
WHERE FiscalYearRef = @FiscalYearId
  AND StockRef = @StockId
  AND UnitRef = 1 -- منحصراً اقلام کیلوگرمی
  AND (InputQuantity > 0 OR OutputQuantity > 0)
ORDER BY ItemRef;


/*
--------------------------------------------------------------------------------------
کوئری ۲: استخراج شماره سند نمونه برای هر ترکیب Type/IsReturn در رسیدهای انبار
هدف:
  ۱) ارائه شماره سند واقعی (DocumentNumber) برای Type=1 عادی، Type=1 مرجوعی، Type=3 و Type=4
  ۲) تطبیق با عناوین فرم‌ها در رابط کاربری سپیدار بدون نیاز به حدس معنای مالی
  ۳) بدون شرح و بدون هیچ مبلغ ریالی
محدوده: سال ۱۰، انبار ۱، حداکثر ۳ سند برای هر ترکیب
--------------------------------------------------------------------------------------
*/
WITH RankedReceipts AS (
    SELECT 
        InventoryReceiptID,
        Number AS DocumentNumber,
        Date AS DocumentDate,
        Type AS ReceiptType,
        IsReturn,
        AccountingVoucherRef,
        ROW_NUMBER() OVER(PARTITION BY Type, IsReturn ORDER BY InventoryReceiptID DESC) AS RowNum
    FROM INV.InventoryReceipt
    WHERE FiscalYearRef = @FiscalYearId
      AND StockRef = @StockId
)
SELECT 
    InventoryReceiptID,
    DocumentNumber,
    DocumentDate,
    ReceiptType,
    IsReturn,
    AccountingVoucherRef
FROM RankedReceipts
WHERE RowNum <= 3
ORDER BY ReceiptType, IsReturn, RowNum;


/*
--------------------------------------------------------------------------------------
کوئری ۳: استخراج شماره سند نمونه برای هر ترکیب Type/IsReturn در حواله‌های انبار
هدف:
  ۱) ارائه شماره سند واقعی (DocumentNumber) برای Type=2 عادی، Type=2 مرجوعی، و Type=4
  ۲) تطبیق با فرم‌های خروج/حواله در رابط کاربری سپیدار
  ۳) بررسی اینکه چرا حواله‌ها فاقد AccountingVoucherRef هستند
محدوده: سال ۱۰، انبار ۱، حداکثر ۳ سند برای هر ترکیب
--------------------------------------------------------------------------------------
*/
WITH RankedDeliveries AS (
    SELECT 
        InventoryDeliveryID,
        Number AS DocumentNumber,
        Date AS DocumentDate,
        Type AS DeliveryType,
        IsReturn,
        AccountingVoucherRef,
        ROW_NUMBER() OVER(PARTITION BY Type, IsReturn ORDER BY InventoryDeliveryID DESC) AS RowNum
    FROM INV.InventoryDelivery
    WHERE FiscalYearRef = @FiscalYearId
      AND StockRef = @StockId
)
SELECT 
    InventoryDeliveryID,
    DocumentNumber,
    DocumentDate,
    DeliveryType,
    IsReturn,
    AccountingVoucherRef
FROM RankedDeliveries
WHERE RowNum <= 3
ORDER BY DeliveryType, IsReturn, RowNum;
