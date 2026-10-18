# الزامات و ضوابط امنیتی فایل‌های ضبط مکالمات (Call Recording Security)
# ParkPalace Procurement CRM — Phase 6 Recording Security

**پروژه:** سامانه جامع خرید و تدارکات پارک پالاس (ParkPalace Procurement CRM)  
**فاز ۶:** یکپارچه‌سازی ایزابل، مدیریت تماس‌ها و پخش امن صوت  
**تاریخ گزارش:** ۲۶ مهر ۱۴۰۵ (2026-10-18)  
**محیط اجرا:** `D:\ParkPlace`

---

## ۱. اصول حاکم بر امنیت و محرمانگی مکالمات (Security Principles)

فایل‌های صوتی مکالمات ضبط‌شده حاوی مذاکرات تجاری، توافقات قیمتی، مشخصات تأمین‌کنندگان و اطلاعات محرمانه سازمانی هستند. افشای غیرمجاز یا دسترسی بدون ضابطه به این فایل‌ها، تهدیدی جدی برای امنیت اطلاعات سازمان محسوب می‌شود.

سیاست‌های بنیادین پیاده‌سازی‌شده در فاز ۶ به شرح زیر است:
1. **عدم افشای مسیرهای فیزیکی فایل:** مسیرهای خام لینوکسی ایزابل (مانند `/var/spool/asterisk/monitor/...`) یا ساختار پوشه‌های داخلی ویندوز هرگز در APIها، کدهای جاوااسکریپت، لاگ‌ها یا دیتابیس کلاینت افشا نمی‌شوند.
2. **عدم امکان دانلود مستقیم و عمومی (No Direct Unprotected URLs):** هیچ پوشه وابی تحت وب‌سرور برای دانلود آزاد فایل‌های صوتی به اشتراک گذاشته نمی‌شود.
3. **پخش انحصاری از طریق پراکسی امن API (Secure Stream Gateway):** تمامی کلاینت‌ها فقط از طریق اندپوینت محافظت‌شده `GET /api/calls/{id}/recording` و با ارسال توکن JWT معتبر می‌توانند به جریان صوت دسترسی پیدا کنند.
4. **حفاظت قطعی در برابر حملات تزریق مسیر (Path Traversal Protection).**

---

## ۲. حفاظت در برابر حملات تزریق مسیر (Path Traversal Protection)

مهاجمان ممکن است تلاش کنند با ارسال کاراکترهای پیمایش دایرکتوری نظیر `../` یا `..\` به فایل‌های حساس سرور (نظیر `appsettings.json`, `web.config` یا فایل‌های سیستمی سیستم‌عامل) دسترسی پیدا کنند.

### سازوکار اعتبارسنجی در `RecordingService.cs`:

```csharp
public async Task<(bool Exists, Stream? Stream, string? ContentType, string? FileName)> GetRecordingStreamAsync(string? recordingFileName)
{
    if (string.IsNullOrWhiteSpace(recordingFileName))
        return (false, null, null, null);

    // ۱. استخراج صرفاً نام فایل و حذف هرگونه دایرکتوری نسبی یا پیشوند مسیر
    var safeFileName = Path.GetFileName(recordingFileName);

    // ۲. بررسی وجود الگوهای مشکوک پیمایش پوشه
    if (safeFileName.Contains("..") || safeFileName.Contains("/") || safeFileName.Contains("\\"))
    {
        _logger.LogWarning("Potential path traversal attempt detected in recording filename: {RawFileName}", recordingFileName);
        return (false, null, null, null);
    }

    // ۳. ترکیب مسیر فقط با ریشه مجاز و تاییدشده ذخیره‌سازی
    var fullPath = Path.GetFullPath(Path.Combine(_recordingsBasePath, safeFileName));
    var fullBasePath = Path.GetFullPath(_recordingsBasePath);

    // ۴. تطبیق ریاضیاتی مسیر نهایی با ریشه مجاز
    if (!fullPath.StartsWith(fullBasePath, StringComparison.OrdinalIgnoreCase))
    {
        _logger.LogWarning("Security violation: Path is outside base directory. Resolved: {ResolvedPath}", fullPath);
        return (false, null, null, null);
    }

    if (!File.Exists(fullPath))
        return (false, null, null, null);

    var memory = new MemoryStream();
    using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        await fileStream.CopyToAsync(memory);
    }
    memory.Position = 0;

    return (true, memory, "audio/wav", safeFileName);
}
```

### شواهد آزمون خودکار:
تست اختصاصی `RecordingService_PathTraversalAttack_IsSafelyBlocked` در مجموعه `ParkPalace.UnitTests` با ارسال ورودی‌های مخرب نظیر `../../etc/passwd` و `..\..\Windows\System32\cmd.exe` اجرا شده و مسدودسازی امن ۱۰۰٪ با وضعیت `Exists = false` تأیید گردید.

---

## ۳. ماتریس کنترل دسترسی نقشی برای شنود مکالمات (RBAC Access Matrix)

شنود و پخش صوت تماس‌ها مشمول مجوزهای تفکیک‌شده است:

| نقش کاربری | مجوز مورد نیاز | دامنه تماس‌های مجاز برای شنود مکالمه | وضعیت دسترسی |
| :--- | :--- | :--- | :---: |
| **مدیر ارشد (Super Admin / Manager)** | `Permissions.CallsSupervise` | کلیه تماس‌های ورودی، خروجی و ترنسفرشده تمامی داخلی‌ها | **مجاز** |
| **کارشناس ارشد تدارکات (Procurement)** | `Permissions.CallsRecordingsListen` | تماس‌های مرتبط با داخلی خود یا تأمین‌کنندگان تحت مسئولیت | **مجاز** |
| **کارشناس انبار (Warehouse)** | - | بدون دسترسی پیش‌فرض به صوت مکالمات (مگر در موارد تأیید موردی) | **مسدود (403)** |
| **کارشناس امور مالی (Finance)** | - | دسترسی به صورت فاکتور و متادیتا؛ مسدودسازی دسترسی به صوت بدون مجوز | **مسدود (403)** |
| **کاربر ناشناس / بدون لاگین** | - | فاقد توکن JWT | **مسدود (401)** |

---

## ۴. مدیریت وضعیت فایل‌های صوتی مفقود یا ناموجود (Missing Recording State)

در صورتی که تماسی در سامانه ثبت شده باشد اما فایل ضبط آن در سرور موجود نباشد (نظیر تماس‌های بی‌پاسخ، تماس‌های داخلی که ضبط برای آن‌ها فعال نبوده، یا تأخیر در انتقال فایل از ایزابل به CRM):
- اندپوینت API خطای ۵۰۰ تولید نخواهد کرد.
- یک پاسخ استاندارد `404 Not Found` به همراه ساختار استاندارد RFC-7807 `ProblemDetails` بازگردانده می‌شود:
  ```json
  {
    "type": "https://parkpalace.ir/errors/recording-not-found",
    "title": "فایل ضبط مکالمه یافت نشد",
    "status": 404,
    "detail": "فایل صوتی برای این تماس وجود ندارد یا هنوز توسط سرور تلفنی آماده نشده است."
  }
  ```
- در فرانت‌اند React، دکمه پخش به صورت غیرفعال (Disabled) به همراه برچسب متنی «فایل صوت موجود نیست» نمایش می‌یابد تا تجربه کاربری دچار سردرگمی نشود.

---

## ۵. متادیتا و سیاست نگهداری فایل‌های صوتی (Retention Policy)

### ۵.۱. متادیتای ذخیره‌شده برای هر فایل
در جدول `CallRecords` اطلاعات فنی زیر نگهداری می‌شود:
- `RecordingFileName`: نام فایل نرمال‌شده بدون مسیر
- `RecordingDurationSeconds`: مدت زمان دقیق فایل صوتی
- `RecordingStorageState`: وضعیت در دسترس بودن فایل (`Available`, `Missing`, `Archived`)
- `CreatedAtUtc`: تاریخ و زمان دقیق تولید فایل به فرمت استاندارد UTC

### ۵.۲. سیاست پاکسازی و بایگانی دوره‌ای (Data Retention)
- **دوره نگهداری فعال (Hot Storage):** فایل‌های صوتی به مدت ۶ ماه روی حافظه سریع سرور باقی می‌مانند.
- **بایگانی سرد (Cold Archival):** مکالمات بالای ۶ ماه به فضای ذخیره‌سازی ابری/NAS پشتیبان منتقل می‌شوند.
- **ممنوعیت حذف خودکار بدون ممیزی:** هیچ فایلی به صورت خام یا بدون ثبت در لاگ ممیزی `CallAudits` حذف نخواهد شد.
- **ممنوعیت دستکاری سرور تلفنی:** هیچ تغییری در دستورات لینوکسی ایزابل یا اسکریپت‌های حذف خودکار Asterisk بدون تأیید کتبی اعمال نخواهد شد.
