<div dir="rtl">

# تصمیم امنیتی — مجوزهای شنود/نظارت نقش Admin (جدا از بستهٔ dashboard-exec)

**تاریخ:** ۱۴۰۵/۰۸/۱۶  
**وضعیت:** **T2–T4 اعمال شد** — Gate `ProdAdminTelephonyT2T3T4Passed`  
**خارج از بستهٔ `dashboard-exec-v1`**

شواهد T1: `docs/runs/prod_admin_telephony_t1_discovery_20261107_142336.json`  
شواهد T2–T4: `docs/runs/prod_admin_telephony_t2t3t4_20261107_142613.json`  
بکاپ RolePermissions روی CRM: `D:\ParkPlaceCRM\backups\admin-telephony-t2t4_20261107_142607\` (+ `restore_admin_calls_sensitive.sql`)

---

## نتیجهٔ اجرا

| مرحله | نتیجه |
|--------|--------|
| T1 کشف | فقط‌خواندنی؛ پیشنهاد `Manager` + `SAHAR\j.asg` |
| T2 اعطا | نقش **`Manager`** به **`SAHAR\j.asg`** افزوده شد (اکنون `Admin,Manager`) |
| تکمیل مدیرعامل | نقش **`Manager`** به **`SAHAR\n.asghari`** (مدیر عامل) افزوده شد — Gate `ProdAdminTelephonyCeoManagerGrantPassed` |
| T3 آزمون پیش از حذف | Admin و Manager هر دو stream ضبط → 200 |
| T4 حذف از Admin | سه پیوند حذف شد؛ شمار Admin روی این سه کد = **0**؛ Manager همچنان **3** |
| تأیید پس از T4 | Admin stream → **403** («مجوز شنود ندارید»)؛ Manager stream → **200** |
| `SAHAR\n.asghari` | مدیر عامل — نقش **`Manager`** اعطا شد (`Admin,Manager`)؛ شنود از مسیر Manager |
| Admin فنی | `CRM\Administrator` / `SAHAR\Administrator` دیگر از نقش Admin شنود/نظارت ندارند |

کدهای حذف‌شده از Admin:

- `Calls.Recordings.Listen`
- `Calls.RecordingsListen`
- `Calls.Supervise`

---

## وضعیت فعلی (پس از T4)

| نقش | سه کد حساس |
|-----|------------|
| Admin | **ندارد** (۰) |
| Manager | دارد (۳) — مسیر جایگزین تأییدشده |
| سایر نقش‌های عملیاتی (Finance/… ) | RecordingsListen همچنان طبق وضعیت پیش از T4 (Supervise فقط Manager در میان نقش‌های اصلی) |

`Finance.PartyLedgerView` روی Admin همچنان غایب است؛ تفکیک مالی سپیدار برقرار است.

---

## کار باز باقی‌مانده (اختیاری)

| مورد | وضعیت |
|------|--------|
| اعطای مسیر شنود به `SAHAR\n.asghari` (مدیر عامل) | **انجام شد** — `Manager`؛ شواهد `prod_admin_telephony_ceo_nasghari_manager_20261107_143445.json` |
| نقش حداقل‌اختیار `CallSupervisor` به‌جای اتکا به `Manager` | پیشنهاد آینده؛ اعمال نشده |
| بازگردانی اضطراری Admin | `restore_admin_calls_sensitive.sql` در پوشهٔ بکاپ CRM |

</div>
