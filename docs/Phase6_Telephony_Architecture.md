# معماری سامانه تلفنی، یکپارچه‌سازی ایزابل و جریان بلادرنگ پاپ‌آپ
# ParkPalace Procurement CRM — Phase 6 Telephony Architecture

**پروژه:** سامانه جامع خرید و تدارکات پارک پالاس (ParkPalace Procurement CRM)  
**فاز ۶:** یکپارچه‌سازی ایزابل، پردازش رویدادهای تماس و اطلاع‌رسانی بلادرنگ  
**تاریخ گزارش:** ۲۶ مهر ۱۴۰۵ (2026-10-18)  
**محیط اجرا:** `D:\ParkPlace`  
**وضعیت:** پیاده‌سازی و اعتبارسنجی کامل شده

---

## ۱. نمای کلی معماری تلفنی (Architecture Overview)

معماری فاز ۶ پارک پالاس به منظور اتصال مطمئن، امن و با عملکرد بالا به سرور تلفنی ایزابل/استریسک، بر اساس الگوی چندلایه‌ای و رویدادمحور (Event-Driven Reactive Architecture) طراحی شده است. 

دیاگرام جریان داده و اجزای سیستم به شرح زیر است:

```mermaid
flowchart TD
    subgraph Telephony_Sources [منابع تلفنی]
        PBX[Asterisk / Issabel 192.168.1.241] -->|AMI Socket Events| LiveAdapter[IssabelLiveTelephonyAdapter]
        DevSim[PBX Simulator / Test Fixtures] -->|Simulated Events| SimAdapter[SimulatedTelephonyAdapter]
    end

    subgraph Core_Telephony_Engine [موتور پس‌زمینه CRM]
        LiveAdapter -.->|ITelephonyAdapter| Worker[TelephonyBackgroundService]
        SimAdapter -->|ITelephonyAdapter| Worker
        Worker -->|Concurrent Queue / Channel| Ingest[CallManagerService.IngestAmiEventAsync]
        
        Ingest --> Match[Supplier Identification & Phone Normalizer]
        Match --> CRM_DB[(CRM Database: CallRecords, CallAudits, CallFollowUps)]
        
        Ingest --> SignalRService[SignalRTelephonyNotificationService]
    end

    subgraph RealTime_Frontend [فرانت‌اند و کاربران]
        SignalRService -->|/hubs/telephony| Hub[TelephonyHub]
        Hub -->|SendToExtension / Supervise| WebClient[React Frontend: CallPopup]
        WebClient -->|Quick Notes / Tasks| CallsAPI[CallsController]
        CallsAPI --> CRM_DB
        WebClient -->|Stream Audio Recording| AudioStream[GET /api/calls/{id}/recording]
        AudioStream --> RecordingSvc[RecordingService: Safe File Stream]
    end
```

---

## ۲. الگوی دوآداپتوره تلفنی (Dual-Adapter Pattern)

برای تضمین این که محیط تست، CI/CD و توسعه محلی بدون سرور فیزیکی ایزابل ۱۰۰٪ فعال و تست‌پذیر باشند، قرارداد رابط `ITelephonyAdapter` تعریف شده است:

```csharp
public interface ITelephonyAdapter
{
    bool IsConnected { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    event Func<AmiCallEventDto, Task>? OnCallEventReceived;
}
```

### ۲.۱. آداپتور شبیه‌ساز قطعی (`SimulatedTelephonyAdapter`)
- به عنوان پیاده‌سازی پیش‌فرض در `Program.cs` رجیستر شده است.
- دارای متد کنترل‌پذیر `SimulateIncomingCallAsync`, `SimulateOutgoingCallAsync`, `SimulateTransferAsync` برای ایجاد دقیق رویدادهای `Dial`, `Bridge`, `Hangup`, `Transfer`.
- امکان تولید فایل‌های صوتی شبیه‌سازی‌شده (Mock WAV files) با هدر معتبر استاندارد RIFF/WAVE جهت راستی‌آزمایی پلیر صوتی.

### ۲.۲. آداپتور زنده استریسک (`IssabelLiveTelephonyAdapter`)
- اتصال پایدار سوکت TCP به درگاه `5038` سرور استریسک.
- ارسال پیام احراز هویت اولیه:
  ```text
  Action: Login
  Username: crm_telephony_user
  Secret: ******
  Events: on
  ```
- مجهز به مکانیزم **Exponential Backoff Reconnect** در صورت قطع ارتباط با PBX یا قطعی موقت سوکت.
- تبدیل بلادرنگ بسته‌های رویداد متنی استریسک (`Event: DialBegin`, `Event: BridgeEnter`, `Event: Hangup`, `Event: AttendedTransfer`) به اشیای داده استاندارد `AmiCallEventDto`.

---

## ۳. سرویس پردازشگر رویدادها در پس‌زمینه (`TelephonyBackgroundService`)

این سرویس از نوع `BackgroundService` دات‌نت بوده و در تمام طول حیات اپلیکیشن اجرا می‌شود:
1. در زمان راه‌اندازی (Startup)، اشتراک متد هندلر با آداپتور تلفنی برقرار می‌شود.
2. با دریافت هر رویداد تلفنی از سوکت AMI، یک `IServiceScope` موقت ایجاد شده و متد `ICallManagerService.IngestAmiEventAsync` فراخوانی می‌گردد.
3. در صورت ری‌استارت شدن وب‌سرور یا ورکر، به لطف ذخیره‌سازی وضعیت تماس‌ها و شاخص یکتای `Uniqueid` در پایگاه داده، هیچ تماسی تکراری یا مخدوش نخواهد شد (Idempotency).

---

## ۴. موتور مدیریت و طبقه‌بندی تماس‌ها (`CallManagerService`)

وظایف اصلی پیاده‌سازی‌شده در سرویس هسته:
- **دریافت رویداد و همگام‌سازی وضعیت تماس:** نگاشت رویدادهای `Dial`, `Ringing`, `Answered`, `Completed`, `Missed`, `Busy`, `Failed`, `Transferred`.
- **رهگیری لگ‌های چندگانه و ترنسفرها:** ذخیره دقیق `Uniqueid` و `Linkedid` استریسک برای ارتباط میان لگ‌های تماس منتقل‌شده.
- **شناسایی تأمین‌کننده:** استفاده از نرمال‌ساز پیشرفته شماره تلفن (موبایل و خطوط ثابت شهری ایران) و تطبیق با جداول `Suppliers` و `SupplierContacts`.
- **طبقه‌بندی گذشته‌نگر تماس‌ها (Retrospective Classification):** هنگامی که یک شماره ناشناس پس از روزها یا ماه‌ها در سیستم به عنوان تأمین‌کننده ثبت می‌شود، با فراخوانی متد `ClassifyUnknownCallerAsync` تمامی تاریخچه تماس‌های گذشته با آن شماره بدون تغییر ساختار داده، به پرونده همان تأمین‌کننده منتسب می‌گردد.
- **ثبت یادداشت و اقدامات پیگیری (Notes & Follow-ups):** ذخیره یادداشت کارشناس و ایجاد کارهای پیگیری آتی دارای مهلت انجام (`DueDateUtc`) با قابلیت ارجاع به همکاران.

---

## ۵. اعلان بلادرنگ با SignalR و کنترل دسترسی پاپ‌آپ (`TelephonyHub`)

پروتکل وب‌سوکت SignalR در مسیر `/hubs/telephony` امکان ارسال آنی پاپ‌آپ تماس به مرورگر اپراتور را فراهم می‌سازد:

### ۵.۱. جداسازی داخلی و دسترسی امنیتی
1. هنگام اتصال کلاینت React به هاب، توکن JWT احراز هویت اعتبارسنجی شده و شماره داخلی کاربر بر اساس ادعای کاربری یا جدول `UserExtensions` استخراج می‌شود.
2. کلاینت به گروه اختصاصی داخلی خود متصل می‌شود (`extension_{ext}`).
3. کاربرانی که دارای دسترسی سرپرستی تلفن (`Permissions.CallsSupervise`) باشند، عضو گروه سرپرستان (`group_supervisors`) شده و می‌توانند تماس‌های کلیه خطوط را پایش کنند.
4. تماس‌ها صرفاً به کلاینت‌های مجاز همان داخلی ارسال می‌شود و کاربران سایر بخش‌ها پاپ‌آپ تماس همکاران خود را دریافت نمی‌کنند.

### ۵.۲. محتوای داده پاپ‌آپ بلادرنگ (`CallerPopupDto`)
```json
{
  "callId": "c3a1b2c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c",
  "uniqueid": "1729227000.101",
  "linkedid": "1729227000.101",
  "callerNumber": "09121112233",
  "extension": "101",
  "extensionOwnerName": "علیرضا تدارکاتی",
  "direction": "Inbound",
  "status": "Ringing",
  "matchType": "SingleMatch",
  "supplierId": "e1f2a3b4-...",
  "supplierName": "شرکت پارس متال",
  "companyName": "پارس متال تامین تجهیز",
  "representativeName": "مهندس کاظمی",
  "candidateMatches": [],
  "recentCalls": [
    {
      "callDateUtc": "2026-10-15T09:12:00Z",
      "direction": "Inbound",
      "status": "Completed",
      "durationSeconds": 145,
      "extension": "101"
    }
  ]
}
```

---

## ۶. پخش و استریم ایمن فایل‌های ضبط مکالمات (`RecordingService`)

پخش مکالمات تلفنی یکی از حساس‌ترین ماژول‌ها از منظر امنیت داده است. سرویس `RecordingService` لایه‌های زیر را پوشش می‌دهد:

- **حفاظت در برابر حملات تزریق مسیر (Path Traversal Protection):** نام فایل ورودی پالایش شده و هرگونه کاراکتر خطرناک نظیر `..`، `/`، `\` مسدود می‌شود. بررسی قطعی انجام می‌گیرد که مسیر نهایی درون پوشه مجاز فایل‌های صوتی قرار دارد (`Path.GetFullPath`).
- **کنترل دسترسی نقشی (RBAC):** استریم صوتی صرفاً برای کاربرانی که دسترسی `Permissions.CallsRecordingsListen` یا `Permissions.CallsSupervise` دارند مجاز است.
- **استریم غیرهمگام (Asynchronous Range/Chunk Streaming):** فایل به صورت `FileStreamResult` با هدر صوتی مناسب (`audio/wav`) استریم شده و مسیر خام فایل در سیستم‌عامل سرور هرگز به سمت کلاینت بازگردانده نمی‌شود.

---

## ۷. ساختار رابط کاربری فرانت‌اند React

در محیط فرانت‌اند:
- کامپوننت سراسری `CallPopup.tsx` در بالاترین لایه اپلیکیشن (`App.tsx`) قرار گرفته و در تمام صفحات با رسیدن رویداد SignalR باز می‌شود.
- صدای زنگ ملایم (Audio Web API Chime) در لحظه زنگ خوردن تلفن پخش می‌شود.
- در پاپ‌آپ، امکان ثبت یادداشت فوری، انتخاب تأمین‌کننده در صورت چندتایی بودن، و دکمه دسترسی سریع به پرونده تأمین‌کننده تعبیه شده است.
- نمای مدیریت تماس‌ها (`CallsView.tsx`) شامل ۵ زبانه کامل:
  1. تاریخچه و گزارش جامع تماس‌ها با فیلتر وضعیت و پلیر صوتی
  2. مدیریت شماره‌های ناشناس و انتساب دستی به تأمین‌کنندگان
  3. کارتابل وظایف و کارهای پیگیری تماس‌ها
  4. تطبیق گزارش‌های CDR/CEL
  5. کنسول شبیه‌ساز تلفنی برای تست و دمو
