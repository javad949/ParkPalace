# خلاصه استخراج متادیتای سپیدار (فاز ۱) — شناسه: sepidar_metadata_20261027_172633

* **زمان اجرا (UTC):** 2026-10-27T13:56:20Z
* **مسیر ارتباطی:** کلاینت توسعه -> نشست امن WinRM HTTPS -> سرور CRM (192.168.1.248) -> SQL Server سپیدار (192.168.1.242,62753)
* **سرور واقعی SQL Server:** SEPIDAR\SEPIDAR
* **پایگاه‌داده هدف:** Sepidar01
* **تنظیمات قفل و مهلت:** CommandTimeout = 30 ثانیه | LOCK_TIMEOUT = 3000 میلی‌ثانیه
* **ایزولاسیون تراکنش:** READ COMMITTED (sys.* catalogs only)

## فایل‌های CSV استخراج‌شده:
* **04_columns_detail.csv:** 3733 سطر
* **02_schemas_summary.csv:** 16 سطر
* **03_tables_and_views.csv:** 299 سطر
* **01_database_config.csv:** 1 سطر
* **05_potential_inventory_columns.csv:** 1003 سطر
* **06_foreign_keys.csv:** 228 سطر

## وضعیت دسترسی و دید متادیتا:
* کوئری‌ها منحصراً روی جداول سیستمی کاتالوگ (sys.databases, sys.schemas, sys.objects, sys.columns, sys.foreign_keys, sys.extended_properties) اجرا شدند.
* صفر سطر از جداول عملیاتی خوانده شد.
* هیچ تنظیم دیتابیس، زیرساخت، فایروال یا مجوزی تغییر نیافت.
