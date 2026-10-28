# گزارش استقرار عملیاتی سامانه ParkPalace CRM — نگارش v1.0.0
# ParkPalace CRM — Production Deployment Report v1.0.0

**پروژه:** سامانه جامع تدارکات و مدیریت ارتباط با تأمین‌کنندگان پارک پالاس (ParkPalace CRM)  
**سند:** `D:\ParkPlace\docs\ParkPalace_Production_Deployment_v1_0_0_Report.md`  
**تاریخ اجرا:** ۲۶ مهر ۱۴۰۵ (18 اکتبر 2026)  
**نسخه استقرار:** `v1.0.0-pilot` (بر اساس commit هدف `6c26b01`)  
**سرور میزبان:** ماشین اختصاصی `CRM` (`192.168.1.248`) عضو دامین `sahar.loc`  
**آدرس دسترسی نهایی:** `https://crm.sahar.loc`  
**وضعیت کلی استقرار:** **OPERATIONAL & ACTIVE (موفقیت‌آمیز و عملیاتی ۱۰۰٪)**  

---

## ۱. خلاصه اجرایی و وضعیت بخش‌ها

| بخش سامانه | وضعیت عملیاتی | جزئیات فنی و پیکربندی |
| :--- | :---: | :--- |
| **دیتابیس CRM** | **فعال و همگام (ONLINE)** | نام دیتابیس `ParkPalaceCRM`، تعداد ۵۰ جدول، ۵ مرحله Migration کامل، مسیر دیتا `D:\SQLData` و لاگ `L:\SQLLogs`، تفکیک دسترسی Runtime با `crm_app_user`. |
| **سرویس IIS و API** | **در حال اجرا (STARTED)** | وب‌سایت `ParkPalaceCRM`، اپلیکیشن فرزند `/api`، AppPool با معماری `InProcess` (AspNetCoreModuleV2)، احراز هویت Negotiate/Windows فعال. |
| **رابط کاربری Frontend** | **عملیاتی (SERVED)** | خروجی React 18 / Vite در مسیر `D:\ParkPlaceCRM\web`، قوانین URL Rewrite برای SPA Fallback و ریدایرکت خودکار HTTP به HTTPS. |
| **امنیت HTTPS و گواهی** | **رمزشده و معتبر (ACTIVE)** | گواهینامه اختصاصی وب `CN=crm.sahar.loc` با کلید RSA 3072-bit و SAN کامل، بایندینگ پورت ۴۴۳، اعتبارسنجی ۱۰۰٪ سخت‌گیرانه بدون Bypass. |
| **سرویس پس‌زمینه Worker** | **آماده‌به‌کار (STANDBY)** | استقرار باینری‌ها در `D:\ParkPlaceCRM\worker`؛ عدم فعال‌سازی عمدی جهت جلوگیری از تغییرات کنترل‌نشده در سپیدار و ایزابل. |
| **مسیر مدیریت WinRM** | **رمزشده و دست‌نخورده** | درگاه HTTPS 5987 به ۵۹۸۶ با گواهی `crm-mgmt.sahar.loc` کاملاً مستقل و پایدار حفظ گردید. |

---

## ۲. جزئیات دیتابیس ParkPalace CRM (SQL Server 2022)

1. **ایجاد و فایل‌های فیزیکی:**
   * نام دیتابیس: `ParkPalaceCRM`
   * فایل داده اصلی: `D:\SQLData\ParkPalaceCRM.mdf` (100MB، رشد 64MB)
   * فایل لاگ تراکنش‌ها: `L:\SQLLogs\ParkPalaceCRM.ldf` (50MB، رشد 32MB)
   * Collation استاندارد: `Persian_100_CI_AI`
2. **اعمال Migrationهای پنج‌گانه EF Core:**
   کلیه اسکریپت‌های مایگریشن با فعال‌سازی پارامتر `-I` (QUOTED_IDENTIFIER) برای ایندکس‌های فیلترشده با موفقیت ۱۰۰٪ اجرا شدند:
   * `20261017183302_InitialCrmIdentityAndAudit` (زیرساخت پایه و هویت)
   * `20261017190643_Phase3_CatalogAndSuppliers` (مدیریت تأمین‌کنندگان و کالاها)
   * `20261017200046_Phase4_InventoryAndWarehouse` (انبارداری و اسنپ‌شات موجودی)
   * `20261017204424_Phase5_ProcurementAndThreeWayMatching` (تدارکات و انطباق سه‌طرفه فاکتور)
   * `20261018042000_Phase6_TelephonyAndCallManagement` (سوابق تلفنی و مکالمات)
3. **تفکیک دسترسی امنیتی (Least Privilege):**
   * لاگین ویندوزی اختصاصی برای Application Pool: `[IIS APPPOOL\ParkPalaceCRM]`
   * کاربر دیتابیس: `[crm_app_user]`
   * مجوزهای اعطاشده به کاربر زمان اجرا: صرفاً `db_datareader`، `db_datawriter` و `EXECUTE` (بدون دسترسی DDL یا مالکیت دیتابیس).
   * کانکشن استرینگ به صورت ویندوزی و امن (`Integrated Security=True;`) تنظیم گردید.

---

## ۳. پیکربندی و انتشار Backend و IIS

1. **استقرار فایل‌ها:** انتقال بسته به مسیر `D:\ParkPlaceCRM\api`.
2. **Application Pool اختصاصی:**
   * نام: `ParkPalaceCRM`
   * Managed Runtime Version: `No Managed Code`
   * StartMode: `AlwaysRunning`
   * فرآیند IdleTimeout: برابر `0` (دائماً فعال در حافظه).
3. **پیکربندی احراز هویت در IIS:**
   * آنلاک‌سازی بخش‌های `windowsAuthentication` و `anonymousAuthentication` در سطح وب‌سرور.
   * فعال‌سازی Windows Authentication روی مسیر `ParkPalaceCRM/api` جهت تطابق با هندلر Negotiate هسته ASP.NET Core.
4. **تطابق مسیر مسیریابی (Routing Alignment):**
   * اضافه شدن قاعده بازنویسی در `D:\ParkPlaceCRM\api\web.config` برای نگاشت صحیح درخواست‌های ورودی به کنترلرهای دارای پیشوند `api/`.
5. **لاگینگ و پایش:**
   * فعال‌سازی لاگینگ خروجی استاندارد (`stdoutLogEnabled="true"`) با ذخیره در مسیر `D:\ParkPlaceCRM\logs\stdout`.

---

## ۴. انتشار Frontend و تجربه کاربری SPA

1. **استقرار دارایی‌های ایستا:** انتقال خروجی React/Vite به `D:\ParkPlaceCRM\web`.
2. **پیکربندی `web.config` سایت اصلی:**
   * قانون ریدایرکت خودکار تمامی درخواست‌های HTTP پورت ۸۰ به HTTPS پورت ۴۴۳ (`HTTP 301 Moved Permanently`).
   * قانون بازنویسی مسیرهای SPA (React Router) به `/index.html` برای کلیه مسیرهای کلاینت (مانند `/inventory` و `/finance`) به‌جز ترافیک متناظر با `/api`.
   * ثبت MIME Types مربوط به وب‌فونت‌ها (`.woff`, `.woff2`) و فایل‌های داده (`.json`).
3. **آزمون لود فایل‌ها:** فایل‌های باندل جاوااسکریپت (`index-PqNNdkVu.js` با حجم 479KB) و استایل‌های CSS (`index-DDbzkEvZ.css` با حجم 11.6KB) با کد ۲۰۰ تحویل داده شدند.

---

## ۵. امنیت لایه انتقال (HTTPS) و گواهینامه‌ها

1. **گواهینامه اختصاصی وب‌سایت:**
   * **Thumbprint:** `27D9A486A30F5BFDD27B73F334299FE838F750F9`
   * **Subject:** `CN=crm.sahar.loc`
   * **Subject Alternative Names (SAN):** `crm.sahar.loc`, `192.168.1.248`, `94.182.47.116`
   * **مشخصات فنی:** RSA 3072-bit, SHA-256, Server Authentication EKU, CA=FALSE
   * **مدت اعتبار:** ۱۸۰ روز (مطابق طرح TLS مصوب)
   * **استخراج کلید عمومی:** در مسیر [scripts/crm-web.cer](file:///d:/ParkPlace/scripts/crm-web.cer) جهت توزیع امن ذخیره گردید.
2. **عدم تداخل با گواهینامه WinRM:**
   * گواهینامه مدیریتی WinRM با Thumbprint برابر `87957AE07BAE65B80C33BB5F313E938E921FAC6C` کاملاً دست‌نخورده و مستقل حفظ گردید.

---

## ۶. نتایج آزمون‌های عملیاتی (Verification Evidence)

### ۶.۱. آزمون سلامت سامانه و اتصال دیتابیس (`/api/health`)
پاسخ دریافتی از Endpoint با اعتبارسنجی سخت‌گیرانه گواهینامه:
```json
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Server: Microsoft-IIS/10.0

{
  "status": "Healthy",
  "service": "ParkPalace Procurement CRM API",
  "version": "1.0.0",
  "serverTimeUtc": "2026-10-18T13:47:52Z",
  "databaseConnected": true,
  "sepidarIntegrationStatus": "Not configured (Disabled by default in Phase 2)"
}
```

### ۶.۲. آزمون خواندن و نوشتن مستقیم در دیتابیس CRM
* ایجاد رکورد در جدول `AuditEvents` با شناسه تستی و بازیابی موفق با تطابق شناسه و زمان ثبت بدون خطا.

### ۶.۳. آزمون بازیابی و پایداری Application Pool (Recycle Test)
* ریستارت Application Pool انجام گرفت؛ اولین درخواست بلافاصله پردازش و وضعیت سلامت پایدار گزارش گردید.

### ۶.۴. ممیزی وضعیت DNS سرور دامین (PDC: 192.168.1.250)
* استعلام رکورد A نام `crm.sahar.loc` از DNS سرور داخلی دامین:
  * **نام:** `CRM.sahar.loc`
  * **نوع رکورد:** `A`
  * **آدرس IP:** `192.168.1.248`
  * **نتیجه:** رکورد DNS سازمانی از قبل به درستی به سرور CRM اشاره دارد و نیازی به تغییر دستی در PDC وجود ندارد.

---

## ۷. خطاهای شناسایی و رفع‌شده در جریان استقرار

1. **خطای SET QUOTED_IDENTIFIER در هنگام اعمال مایگریشن‌ها:**
   * *علت:* ابزار `sqlcmd` به صورت پیش‌فرض این فلگ را خاموش می‌گذارد که مانع ساخت ایندکس‌های فیلترشده فاز ۳ به بعد می‌شد.
   * *راهکار:* اجرای اسکریپت با فلگ `-I`، که منجر به ایجاد موفق ۵۰ جدول و ثبت تمامی ۵ مایگریشن گردید.
2. **خطای 500.30 در راه‌اندازی ASP.NET Core به دلیل هندلر Negotiate:**
   * *علت:* وابستگی به ماژول Windows Authentication در IIS و آنلاک نبودن این بخش در `applicationHost.config`.
   * *راهکار:* فعال‌سازی و آنلاک بخش ویندوز آتنتیکیشن در IIS و بازیافت استخر برنامه.
3. **عدم تطابق نام دیتابیس در `appsettings.Production.json`:**
   * *علت:* تنظیم پیش‌فرض کانکشن‌استرینگ پروداکشن به نام قبلی `ParkPalaceCRM_DB`.
   * *راهکار:* یکسان‌سازی کانکشن‌استرینگ در هر دو فایل تنظیمات به سمت دیتابیس مستقل `ParkPalaceCRM`.
4. **تطابق مسیر کنترلر با ساب‌اپلیکیشن `/api`:**
   * *علت:* جداسازی پیشوند اپلیکیشن توسط IIS و تکرار آن در کنترلرها.
   * *راهکار:* اعمال قانون بازنویسی دقیق در `web.config` داخلی API.

---

## ۸. وضعیت نهایی و آدرس‌های دسترسی

* **آدرس دسترسی کاربران شبکه داخلی:**  
  [https://crm.sahar.loc](https://crm.sahar.loc/)
* **آدرس مدیریت سرور از راه دور (Antigravity WinRM HTTPS):**  
  `https://crm-mgmt.sahar.loc:5987/wsman`
* **اقدامات پیشنهادی آینده:**
  * توزیع کلید عمومی [scripts/crm-web.cer](file:///d:/ParkPlace/scripts/crm-web.cer) به کلاینت‌های مجاز از طریق GPO یا ایمپورت دستی جهت حذف هشدار مرورگر کلاینت‌ها.
  * فعال‌سازی Worker پس از تأیید جداگانه مجوزهای دسترسی فقط‌خواندنی به سپیدار و ایزابل.
