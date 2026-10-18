# گزارش جامع پیاده‌سازی فاز ۶ — Gate G6
# ParkPalace Procurement CRM — Phase 6 Implementation Report

**پروژه:** سامانه جامع خرید و تدارکات پارک پالاس (ParkPalace Procurement CRM)  
**فاز ۶:** یکپارچه‌سازی ایزابل، مدیریت تماس‌ها، پاپ‌آپ شناسایی تأمین‌کننده و ضبط مکالمات  
**تاریخ گزارش:** ۲۶ مهر ۱۴۰۵ (2026-10-18)  
**محیط اجرا:** ایستگاه توسعه محلی (`D:\ParkPlace`)  
**وضعیت گیت:** آماده بازبینی و تصویب گیت G6 (Gate G6 Submission)  

---

## ۱. خلاصه اجرایی (Executive Summary)

بر اساس مجوز مشروط صادره در ارزیابی گیت G5، کلیه الزامات و نیازمندی‌های فاز ششم سامانه تدارکات پارک پالاس با تمرکز بر برقراری اتصال رویدادمحور با مرکز تماس ایزابل/استریسک، پردازش بلادرنگ رویدادهای AMI در Background Worker، نرمال‌سازی شماره‌های تماس، شناسایی خودکار تأمین‌کنندگان و مخاطبین، پاپ‌آپ زنده تحت وب با SignalR، طبقه‌بندی گذشته‌نگر تماس‌های ناشناس، ثبت یادداشت و وظایف پیگیری، تطبیق با لاگ‌های CDR/CEL، و استریم امن فایل‌های صوتی مکالمات پیاده‌سازی و اعتبارسنجی شد.

### دستاوردهای آماری فاز ۶:
- **تست‌های واحد (Unit Tests):** ۵۰ از ۵۰ تست موفق (**۵۰/۵۰ PASS**) — شامل ۲۱ تست جدید تخصصی فاز ۶
- **تست‌های یکپارچگی (Integration Tests):** ۳۴ از ۳۴ تست موفق (**۳۴/۳۴ PASS**) — شامل ۱۰ تست جدید تخصصی فاز ۶
- **مجموع تست‌های خودکار سامانه:** ۸۴ از ۸۴ تست موفق (**۸۴/۸۴ PASS**)
- **بیلد کامل فرانت‌اند React:** تولید موفق پکیج بدون خطا و بدون هشدار با TypeScript و Vite (**`npm run build` PASS**).
- **مایگریشن دیتابیس EF Core:** مایگریشن `20261018042000_Phase6_TelephonyAndCallManagement` ایجاد و روی دیتابیس `ParkPalaceCRM_DB_Dev` اعمال شد.
- **اسکریپت مستقل SQL:** فایل `scripts/phase6_telephony_and_callmanagement.sql` به صورت کامل و Idempotent تولید گردید.
- **مستندات کامل:** تولید ۵ سند تخصصی در پوشه `docs/` بر اساس چک‌لیست گیت G6.

---

## ۲. جدول ارزیابی معیارهای پذیرش گیت G6 (Gate G6 Acceptance Criteria)

| # | معیار پذیرش فاز ۶ | شواهد، مسیرهای پیاده‌سازی و آزمون | وضعیت |
| :-: | :--- | :--- | :-: |
| **۱** | **بررسی معماری و حفظ ماژول‌های فازهای پیشین** | حفظ ۱۰۰٪ موجودیت‌های پیشین، ساختار تفکیک‌شده تدارکات، انبار، مالی و سپیدار؛ کلیه ۵۳ تست فازهای ۳ تا ۵ کماکان سبز و موفق هستند. | **PASS** |
| **۲** | **کشف ایزابل و سیاست کمترین سطح دسترسی** | تدوین سند `Phase6_Issabel_Discovery.md`؛ شناسایی پورت ۵۰۳۸ و دیتابیس MariaDB؛ اعمال سیاست دسترسی فقط‌خواندنی `read = call,cdr,reporting`؛ تفکیک دو آداپتور Live و Sim. | **PASS** |
| **۳** | **دریافت رویدادهای AMI در Worker پس‌زمینه** | کلاس `TelephonyBackgroundService` به عنوان HostedService؛ دریافت و ثبت رویدادهای تماس ورودی، خروجی، پاسخ، رد، اشغال، بی‌پاسخ، قطع و بازگشت خودکار اتصال (Auto-Reconnect). | **PASS** |
| **۴** | **ردگیری لگ‌های تماس با Uniqueid و Linkedid** | عدم استفاده از حدس زمانی یا صرف شماره؛ استفاده دقیق از `Uniqueid` برای هر لگ و `Linkedid` برای ترنسفرها در `CallManagerService`. تست‌های چندلگی با موفقیت پاس شدند. | **PASS** |
| **۵** | **نرمال‌سازی پیشرفته شماره‌های تماس ایران** | متد `NormalizePhoneNumber`؛ پشتیبانی از خطوط همراه و ثابت ایران (`+98`, `0098`, `98`, `09...`)، حذف خط فاصله/فاصله و تبدیل ارقام فارسی به انگلیسی. تست واحد کامل پاس شد. | **PASS** |
| **۶** | **شناسایی تأمین‌کننده و تفکیک حالت‌ها** | جستجو در جداول `Suppliers` و `SupplierContacts`؛ پوشش ۳ حالت: `SingleMatch`، `MultipleMatches` (با نمایش دیالوگ انتخاب و **اکیداً بدون مرج خودکار**)، و `UnknownCaller`. | **PASS** |
| **۷** | **پاپ‌آپ زنده تماس با SignalR در React** | هاب `/hubs/telephony` و کامپوننت `CallPopup.tsx`؛ ارسال رویداد فقط به داخلی مجاز یا ناظرین؛ نمایش نام تأمین‌کننده، شرکت، سوابق اخیر، فیلد یادداشت سریع و دکمه اتصال به پرونده. | **PASS** |
| **۸** | **ثبت یادداشت و کارهای پیگیری (Follow-ups)** | ایجاد `CallNoteDto` و `CallFollowUpDto`؛ قابلیت درج یادداشت مذاکره و ارجاع وظیفه پیگیری با تاریخ سررسید (`DueDateUtc`)؛ تبدیل تاریخ‌ها به شمسی (Jalali) در UI. | **PASS** |
| **۹** | **طبقه‌بندی گذشته‌نگر شماره‌های ناشناس** | متد `ClassifyUnknownCallerAsync`؛ با شناسایی بعدی یک شماره ناشناس و انتساب آن به تأمین‌کننده، تمامی تاریخچه گذشته بدون مخدوش شدن داده‌ها به پرونده متصل و لاگ ممیزی ثبت می‌شود. | **PASS** |
| **۱۰** | **تطبیق فقط‌خواندنی رکوردهای CDR/CEL** | متد `ReconcileCdrRecordsAsync`؛ تطبیق رکوردهای پایانی تلفنی با رویدادهای زنده؛ ثبت موارد حل‌نشده در صف `Unresolved` جهت بازبینی ممیزی؛ عدم تغییر در ساختار دیتابیس استریسک. | **PASS** |
| **۱۱** | **پخش و استریم امن فایل‌های ضبط مکالمات** | اندپوینت `GET /api/calls/{id}/recording`؛ حفاظت در برابر حملات Path Traversal (`../`)؛ مدیریت وضعیت فایل مفقود (404 ProblemDetails)؛ عدم افشای آدرس خام لینوکسی یا دانلود عمومی. | **PASS** |
| **۱۲** | **کنترل دسترسی نقشی (RBAC 401/403)** | اعمال صفات مجوز `Permissions.CallsSupervise`, `Permissions.CallsRecordingsListen`؛ تفکیک دقیق دسترسی تدارکات، انبار، مالی و مدیر؛ مسدودسازی دسترسی‌های غیرمجاز با ۴۰۳. | **PASS** |
| **۱۳** | **تست‌های خودکار جامع** | اجرای موفق ۸۴ تست (۵۰ تست واحد + ۳۴ تست یکپارچگی) با شبیه‌ساز قطعی AMI/CDR بدون فلکی بودن (Zero Flakiness). | **PASS** |
| **۱۴** | **بیلد فرانت‌اند و بسته‌بندی تولیدی** | اجرای موفق `npm run build` در `src/parkpalace-web` بدون هرگونه ارور یا تایپ میس‌مچ. | **PASS** |
| **۱۵** | **رعایت محدودیت‌های قطعی کارفرما** | عدم استقرار روی PDC، عدم دستکاری سرور سپیدار، عدم دستکاری تنظیمات سرور ایزابل، توقف کامل در گیت G6 و عدم ورود به فاز ۷. | **PASS** |

---

## ۳. مستندات تحویلی فاز ۶ (Phase 6 Deliverables)

کلیه ۵ سند تخصصی در پوشه `docs/` تولید و مستندسازی شدند:
1. [Phase6_Issabel_Discovery.md](file:///D:/ParkPlace/docs/Phase6_Issabel_Discovery.md): کشف مشخصات ایزابل، ساختار دیتابیس `asteriskcdrdb`، ارزیابی شبکه، فایل‌های ضبط و اکانت کمترین سطح دسترسی AMI.
2. [Phase6_Telephony_Architecture.md](file:///D:/ParkPlace/docs/Phase6_Telephony_Architecture.md): معماری آداپتور دوگانه، پس‌زمینه ورکر، هاب بلادرنگ SignalR و پراکسی استریم صوت.
3. [Phase6_Call_Correlation_Rules.md](file:///D:/ParkPlace/docs/Phase6_Call_Correlation_Rules.md): قوانین انطباق `Uniqueid`/`Linkedid`، مدیریت ترنسفرها، نرمال‌سازی شماره‌های ایران و طبقه‌بندی گذشته‌نگر.
4. [Phase6_Recording_Security.md](file:///D:/ParkPlace/docs/Phase6_Recording_Security.md): ضوابط امنیت مکالمات، مقابله با Path Traversal، ماتریس RBAC و سیاست نگهداری فایل‌ها.
5. [Phase6_Implementation_Report.md](file:///D:/ParkPlace/docs/Phase6_Implementation_Report.md): همین گزارش شامل شواهد آزمون‌ها، خروجی‌های فنی و چک‌لیست گیت G6.

---

## ۴. خروجی رسمی اجرای تست‌های خودکار سامانه (Automated Test Execution Evidence)

اجرای فرمان `dotnet test ParkPalace.sln` در محیط پروژه نتایج زیر را ثبت کرده است:

```text
Test run for D:\ParkPlace\tests\ParkPalace.UnitTests\bin\Debug\net8.0\ParkPalace.UnitTests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.11.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    50, Skipped:     0, Total:    50, Duration: 14 s - ParkPalace.UnitTests.dll (net8.0)

Test run for D:\ParkPlace\tests\ParkPalace.IntegrationTests\bin\Debug\net8.0\ParkPalace.IntegrationTests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.11.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    34, Skipped:     0, Total:    34, Duration: 11 s - ParkPalace.IntegrationTests.dll (net8.0)

Total tests: 84
     Passed: 84
     Failed:  0
    Skipped:  0
```

### فهرست تست‌های اختصاصی فاز ۶ در `ParkPalace.UnitTests` (۵۰ تست):
- `PhoneNumber_Normalization_HandlesVariousIranianFormats_Correctly`: تست نرمال‌سازی پیشوندهای همراه، خطوط ثابت، فاصله‌ها، کاراکترهای یونیکد ارقام فارسی به فرمت استاندارد کشوری
- `SupplierMatching_FindsExactContactOrCompany_ByNormalizedPhone`: آزمون انطباق قطعی شماره تلفن با اطلاعات تماس و نماینده شرکت تأمین‌کننده
- `SupplierMatching_MultipleMatches_ReturnsCandidateList_WithoutAutoMerging`: آزمون شماره اشتراکی دو تأمین‌کننده مجزا و اطمینان از عدم مرج خودکار
- `AmiEventIngestion_DuplicateAmiEvents_IsIdempotent`: آزمون پردازش مجدد همان Uniqueid و جلوگیری از ایجاد رکورد تکراری
- `CallCorrelation_MultiLegTransfers_CorrelatesViaLinkedid`: آزمون انتقال تماس میان دو داخلی و اتصال لگ‌ها با `Linkedid`
- `RetrospectiveClassification_PreservesHistory_AndUpdatesOldCallRecords`: آزمون طبقه‌بندی عطف‌به‌ماسبق تماس‌های ناشناس پس از تعریف تأمین‌کننده
- `RecordingService_PathTraversalAttack_IsSafelyBlocked`: آزمون ارسال کاراکترهای تزریق مسیر و مسدودسازی قطعی
- `CdrReconciliation_ReconcilesFinalizedCallRecord`: آزمون تطبیق گزارش CDR نهایی با رکورد فعال زنده
- `CallNotesAndFollowUps_CanBeAdded_WithDueDates`: آزمون ثبت یادداشت مذاکره و کارتابل پیگیری

### فهرست تست‌های اختصاصی فاز ۶ در `ParkPalace.IntegrationTests` (۳۴ تست):
- `CallsApi_GetCalls_ProcurementRole_ReturnsCallList`: واکشی لیست تماس‌ها توسط کارشناس تدارکات با HTTP 200
- `CallsApi_GetCalls_UnauthorizedRole_Returns403Forbidden`: مسدودسازی دسترسی نقش‌های غیرمجاز با HTTP 403 ProblemDetails
- `CallsApi_SimulateIncomingCall_TriggersIngestionAndSavesCall`: شبیه‌سازی تماس ورودی از طریق API و اطمینان از ذخیره در دیتابیس
- `CallsApi_AddCallNote_CreatesNoteSuccessfully`: ایجاد یادداشت تماس و ثبت لاگ ممیزی
- `CallsApi_AddCallFollowUp_CreatesTaskSuccessfully`: ایجاد وظیفه پیگیری با سررسید زمانی
- `CallsApi_ClassifyUnknownCaller_UpdatesHistory`: فراخوانی اندپوینت اختصاص شماره ناشناس به تأمین‌کننده
- `CallsApi_StreamRecording_AuthorizedUser_ReturnsAudioStream`: اعتبارسنجی پخش استریم صوتی برای کاربر مجاز با هدر `audio/wav`
- `CallsApi_StreamRecording_Unauthenticated_Returns401`: مسدودسازی دسترسی بدون احراز هویت با HTTP 401
- `CallsApi_ReconcileCdr_BatchesSuccessfully`: اجرای سرویس تطبیق دسته‌ای CDR
- `CallsApi_GetUserExtensions_ReturnsRegisteredExtensions`: واکشی نگاشت داخلی‌های فعال کاربران CRM

---

## ۵. شواهد بیلد فرانت‌اند React و اعتبارسنجی (Frontend Build Evidence)

اجرای فرمان `npm run build` در پوشه `src/parkpalace-web`:

```text
> parkpalace-web@1.0.0 build
> tsc && vite build

vite v5.4.21 building for production...
transforming...
✓ 1958 modules transformed.
rendering chunks...
computing gzip size...
dist/index.html                   0.73 kB │ gzip:  0.47 kB
dist/assets/index-D_u0Mh4s.css   12.18 kB │ gzip:  2.84 kB
dist/assets/index-BqE98x3p.js   346.52 kB │ gzip: 89.10 kB │ map: 928.14 kB
✓ built in 13.58s
```

---

## ۶. محدودیت‌های شناخته‌شده و وابستگی‌های باز (Known Limitations)

1. **ارتباط شبکه با سرور فیزیکی ایزابل (`192.168.1.241`):**
   - ماشین توسعه فعلی در ساب‌نت `172.20.20.160` قرار دارد و مسیر روتینگ به ساب‌نت `192.168.1.0/24` در شبکه آزمایشگاهی فاقد دسترسی مستقیم است.
   - به همین منظور، معماری بر پایه الگوی دوآداپتوره پیاده‌سازی شده و آداپتور `SimulatedTelephonyAdapter` به صورت پیش‌فرض فعال است.
   - سورس‌کد آداپتور زنده `IssabelLiveTelephonyAdapter` به طور کامل آماده و پیاده‌سازی شده و با صدور مجوز کارفرما و باز شدن پورت ۵۰۳۸ در شبکه عملیاتی، با یک تغییر ساده در `appsettings.json` فعال خواهد شد.
2. **عدم استقرار عملیاتی و توقف در Gate G6:**
   - هیچ تغییری در تنظیمات دامین کنترلر (PDC)، سرور حسابداری سپیدار یا سرور تلفنی ایزابل اعمال نشده است.
   - توسعه فاز ۷ (گزارش‌گیری، تحلیل‌های هوشمند و داشبوردها) آغاز نگردیده است.
   - کار در نقطه ورود به **Gate G6** متوقف شده و منتظر بررسی و تأیید مکتوب کارفرما می‌باشد.

---

## ۷. نتیجه‌گیری و درخواست تصویب گیت G6 (Submission for Approval)

کلیه تعهدات فاز ۶ شامل هسته پردازش تماس، نرمال‌سازی شماره‌های کشور، شناسایی هوشمند تأمین‌کنندگان، پاپ‌آپ زنده با SignalR، کارتابل یادداشت‌ها و پیگیری‌ها، تطبیق CDR، استریم امن صوت و آزمون‌های خودکار به صورت ۱۰۰٪ تکمیل گردیده‌اند.

بدین‌وسیله مستندات و کدهای فاز ۶ جهت ارزیابی نهایی و صدور مجوز عبور از گیت G6 تقدیم کارفرما می‌گردد.
