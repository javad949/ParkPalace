-- ======================================================================================
-- ParkPalace CRM - Sepidar Data Sampling: Phase 2 (Adapted Candidate Queries)
-- File: scripts/02_sepidar_sample_queries_pending_verification.sql
-- 
-- **************************************************************************************
-- وضعیت فایل: «تطبیق‌یافته محلی بر اساس نتایج دوره‌ها، انبارها و متادیتای ACC.Voucher»
-- STATUS: LOCALLY ADAPTED BASED ON VERIFIED FY=10, STOCK=1, AND ACC.VOUCHER METADATA
-- **************************************************************************************
--
-- شناسه‌ها و مقادیر صریح تأییدشده در مرحله قبل:
-- ۱. سال مالی ۱۴۰۵: FiscalYearId = 10 (StartDate: 2026-03-21 00:00:00, EndDate: 2027-03-20 00:00:00)
-- ۲. انبار مواد اولیه: StockID = 1 (کد ۱، عنوان: "مواد اوليه")
-- ۳. واحدهای سنجش: کیلوگرم = ۱ ("كيلوگرم") | عدد = ۲ ("عدد")
-- ۴. ستون‌های تأییدشده ACC.Voucher: VoucherId, Number, DailyNumber, Date, State, Type, FiscalYearRef, IssuerSystem, IsMerged
--
-- اصول و محدودیت‌های ایمنی:
-- - صرفاً آماده‌سازی محلی؛ اجرای عملیاتی نیازمند مجوز بعدی است.
-- - سقف زمان انتظار قفل: SET LOCK_TIMEOUT 3000 ms.
-- - مهلت اجرای هر فرمان: CommandTimeout = 30s.
-- - ایزولاسیون: READ COMMITTED.
-- - نام‌گذاری خنثی: HasAccountingVoucherRefCount و NoAccountingVoucherRefCount.
-- - حذف تمام ستون‌های Description و مبالغ مالی/ریالی.
-- ======================================================================================

SET NOCOUNT ON;
SET LOCK_TIMEOUT 3000;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

DECLARE @ExplicitFiscalYearId INT = 10; -- سال مالی ۱۴۰۵
DECLARE @ExplicitStockId INT = 1;      -- انبار مواد اولیه


/*
--------------------------------------------------------------------------------------
کوئری ۱: بازخوانی وضعیت فعال بودن انبار شماره ۱ با تبدیل صریح به عدد
هدف:
  ۱) تأیید قطعی مقدار IsActive به عنوان عدد صریح (1 یا 0) جهت راستی‌آزمایی پس از رفع باگ DBNull
  ۲) اطمینان از آماده بودن انبار برای پایلوت
--------------------------------------------------------------------------------------
*/
SELECT 
    StockID,
    Code,
    Title,
    IsActive,
    CAST(IsActive AS INT) AS IsActiveInt
FROM INV.Stock
WHERE StockID = @ExplicitStockId;


/*
--------------------------------------------------------------------------------------
کوئری ۲: تحلیل توزیع مقادیر Type و ارجاع حسابداری در رسیدهای انبار مواد اولیه
هدف:
  ۱) کشف مقادیر متمایز Type در رسیدهای سال ۱۰ برای انبار ۱
  ۲) تفکیک رسید عادی (IsReturn=0) از مرجوعی (IsReturn=1)
  ۳) شمارش خنثی اسناد دارای ارجاع حسابداری در برابر فاقد ارجاع
  ۴) حذف Description و تمامی داده‌های ریالی
محدوده: محدود به سال ۱۰ و انبار ۱ | CommandTimeout = 30s
--------------------------------------------------------------------------------------
*/
SELECT 
    Type AS ReceiptType,
    IsReturn,
    COUNT(*) AS TotalReceiptsCount,
    COUNT(AccountingVoucherRef) AS HasAccountingVoucherRefCount,
    COUNT(*) - COUNT(AccountingVoucherRef) AS NoAccountingVoucherRefCount,
    MIN(Date) AS MinTransactionDate,
    MAX(Date) AS MaxTransactionDate
FROM INV.InventoryReceipt
WHERE FiscalYearRef = @ExplicitFiscalYearId
  AND StockRef = @ExplicitStockId
GROUP BY Type, IsReturn
ORDER BY ReceiptType, IsReturn;


/*
--------------------------------------------------------------------------------------
کوئری ۳: تحلیل توزیع مقادیر Type و ارجاع حسابداری در حواله‌های خروج انبار مواد اولیه
هدف:
  ۱) کشف مقادیر متمایز Type در حواله‌های سال ۱۰ برای انبار ۱
  ۲) تفکیک حواله عادی از مرجوعی
  ۳) شمارش خنثی اسناد دارای ارجاع حسابداری در برابر فاقد ارجاع
  ۴) حذف Description و تمامی داده‌های ریالی
محدوده: محدود به سال ۱۰ و انبار ۱ | CommandTimeout = 30s
--------------------------------------------------------------------------------------
*/
SELECT 
    Type AS DeliveryType,
    IsReturn,
    COUNT(*) AS TotalDeliveriesCount,
    COUNT(AccountingVoucherRef) AS HasAccountingVoucherRefCount,
    COUNT(*) - COUNT(AccountingVoucherRef) AS NoAccountingVoucherRefCount,
    MIN(Date) AS MinTransactionDate,
    MAX(Date) AS MaxTransactionDate
FROM INV.InventoryDelivery
WHERE FiscalYearRef = @ExplicitFiscalYearId
  AND StockRef = @ExplicitStockId
GROUP BY Type, IsReturn
ORDER BY DeliveryType, IsReturn;


/*
--------------------------------------------------------------------------------------
کوئری ۴: نمونه‌برداری صحت محاسباتی موجودی انبار مواد اولیه (حداکثر ۲۰ قلم دارای گردش)
هدف:
  ۱) راستی‌آزمایی رابطه Quantity با (InputQuantity - OutputQuantity)
  ۲) بررسی وجود مقادیر در ستون‌های SaleQuantity و SaleWithReserveQuantity
  ۳) حذف کلیه اطلاعات ریالی؛ صرفاً ستون‌های مقداری
محدوده: سال ۱۰، انبار ۱، محدود به ۲۰ سطر
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
WHERE FiscalYearRef = @ExplicitFiscalYearId
  AND StockRef = @ExplicitStockId
  AND (InputQuantity > 0 OR OutputQuantity > 0)
ORDER BY ItemRef;


/*
--------------------------------------------------------------------------------------
کوئری ۵: نمونه محدود وضعیت اسناد حسابداری مرتبط (ACC.Voucher)
هدف:
  ۱) مشاهده مقادیر ستون State و Type برای اسناد حسابداری متصل به اسناد انبار
  ۲) کشف مقادیر تجاری وضعیت سند جهت تطبیق با واژگان سیستم (پیش‌نویس، تایید، ابطال)
  ۳) استفاده منحصراً از ستون‌های تأییدشده در متادیتای فاز قبلی
  ۴) فاقد ستون Description و هرگونه فیلد مالی یا طرف‌حساب
محدوده: حداکثر ۱۰ سطر متصل به رسیدها و حواله‌های سال ۱۰ انبار ۱
--------------------------------------------------------------------------------------
*/
SELECT TOP 10
    v.VoucherId,
    v.FiscalYearRef,
    v.Number,
    v.DailyNumber,
    v.Date,
    v.State,
    v.Type AS VoucherType,
    v.IssuerSystem,
    v.IsMerged
FROM ACC.Voucher v
WHERE v.FiscalYearRef = @ExplicitFiscalYearId
  AND v.VoucherId IN (
      SELECT TOP 5 AccountingVoucherRef 
      FROM INV.InventoryReceipt 
      WHERE FiscalYearRef = @ExplicitFiscalYearId AND StockRef = @ExplicitStockId AND AccountingVoucherRef IS NOT NULL
      UNION
      SELECT TOP 5 AccountingVoucherRef 
      FROM INV.InventoryDelivery 
      WHERE FiscalYearRef = @ExplicitFiscalYearId AND StockRef = @ExplicitStockId AND AccountingVoucherRef IS NOT NULL
  )
ORDER BY v.VoucherId DESC;
