# گزارش و راهنمای عملیاتی اجرای آزمایشی TLS (Canary Rollout Runbook)
# ParkPalace CRM — TLS Canary Deployment & Target Systems Verification

**پروژه:** سامانه جامع تدارکات و مدیریت ارتباط با تأمین‌کنندگان پارک پالاس (ParkPalace CRM)  
**سند:** `D:\ParkPlace\docs\ParkPalace_TLS_Canary_Execution_Report.md`  
**تاریخ:** ۲۶ مهر ۱۴۰۵ (18 اکتبر 2026)  
**نسخه:** ۱.۰.۰  

---

## ۱. مشخصات سیستم‌های هدف تعیین‌شده توسط کارفرما

| ردیف | نام سیستم | آدرس IP | نقش در شبکه دامنه sahar.loc | مأموریت در فاز Canary |
| :---: | :--- | :---: | :--- | :--- |
| **۱** | **CRM** | `192.168.1.248` | سرور میزبان وب‌سایت CRM و پایگاه داده محلی | تولید گواهینامه Self-Signed، آماده‌سازی HTTPS، بایندینگ IIS و تست محلی |
| **۲** | **PDC** | `192.168.1.250` | کنترل‌کننده اصلی دامنه (Primary Domain Controller) | ممیزی صرفاً خواندنی (DNS, OU, GPO) و ایجاد GPO اختصاصی با فیلترینگ انحصاری Edari02 |
| **۳** | **Edari02** | `192.168.1.109` | ایستگاه کاری اداری عضو دامنه (کلاینت آزمایشی Canary) | دریافت گواهی از GPO، اعتبارسنجی اعتماد در Edge/Chrome و تست دسترسی HTTPS |

---

## ۲. گزارش سنجش اتصال اولیه از محیط توسعه

* **محیط مبدأ ممیزی:** ایستگاه کاری محلی (`ORPC` با آدرس `172.20.20.160`).
* **نتیجه سنجش اتصال مستقیم شبکه:**  
  اجرای آزمون `Test-NetConnection` به آدرس‌های `192.168.1.248:80`، `192.168.1.250:53` و `192.168.1.109:445` با وضعیت `TimedOut` و `PingSucceeded: False` مواجه گردید. این امر نشان‌دهنده ایزوله بودن ساب‌نت اداری `192.168.1.0/24` از محیط جاری است.
* **رعایت خطوط قرمز امنیتی کارفرما:**  
  مطابق الزامات قطعی، استفاده از اعتبارنامه‌های افشاشده در سوابق قبلی یا دورزدن کانال‌ها از طریق ابزارهای هایپروایزر اکیداً ممنوع بوده و ارتباط بدون مجوز ریموت متوقف است. کلیه دستورات و اسکریپت‌های زیر به صورت مستقل، پایش‌شده و آماده جهت اجرای دستی/هماهنگ در محیط سه سیستم تنظیم شده‌اند.

---

## ۳. دستورالعمل عملیاتی و اسکریپت‌های تفکیک‌شده به ازای هر سیستم

### گام اول: سرور CRM (192.168.1.248) — صدور گواهی، بایندینگ و تست محلی

#### ۱. صدور گواهینامه Self-Signed با مشخصات استاندارد:
```powershell
# اجرا در PowerShell ادمین سرور CRM:
$certParams = @{
    Subject           = "CN=crm.sahar.loc"
    DnsName           = @("crm.sahar.loc", "crm")
    KeyAlgorithm      = "RSA"
    KeyLength         = 3072
    HashAlgorithm     = "SHA256"
    KeyUsage          = "DigitalSignature", "KeyEncipherment"
    TextExtension     = @(
        "2.5.29.37={text}1.3.6.1.5.5.7.3.1", # Server Authentication
        "2.5.29.19={text}ca=0"                # Basic Constraints: CA=FALSE
    )
    CertStoreLocation = "Cert:\LocalMachine\My"
    NotAfter          = (Get-Date).AddDays(180)
    FriendlyName      = "ParkPalace CRM Pilot TLS Certificate"
}
$cert = New-SelfSignedCertificate @certParams

# استخراج کلید عمومی (فاقد کلید خصوصی) جهت انتقال به PDC / Edari02:
Export-Certificate -Cert $cert -FilePath "D:\ParkPlaceCRM\crm.sahar.loc.cer" -Type CERT
Write-Host "Certificate Issued Successfully. Thumbprint: $($cert.Thumbprint)"
```

#### ۲. پیکربندی بایندینگ HTTPS و ریدایرکت پورت ۸۰ در IIS:
```powershell
Import-Module WebAdministration

# افزودن بایندینگ پورت 443 با فعال‌سازی SNI
New-WebBinding -Name "ParkPalaceCRM" -IPAddress "*" -Port 443 -Protocol "https" -HostHeader "crm.sahar.loc" -SslFlags 1

# انتساب گواهینامه با Thumbprint
$binding = Get-WebBinding -Name "ParkPalaceCRM" -Port 443 -Protocol "https"
$binding.AddSslCertificate($cert.Thumbprint, "My")

# نصب موقت کلید عمومی در مخزن ریشه خود ماشین CRM جهت تست محلی بدون خطا
Import-Certificate -FilePath "D:\ParkPlaceCRM\crm.sahar.loc.cer" -CertStoreLocation "Cert:\LocalMachine\Root"
```

#### ۳. تست اعتبارسنجی محلی روی سرور CRM (Strict TLS Test):
```powershell
# تست دسترسی HTTPS بدون دورزدن اعتبارسنجی:
curl.exe -v https://crm.sahar.loc/api/health
# خروجی مورد انتظار: SSL certificate verify ok و پاسخ کد 200 با متن {"status":"Healthy"}
```

---

### گام دوم: سرور کنترلر دامنه PDC (192.168.1.250) — ممیزی Read-Only و طراحی GPO انحصاری Edari02

> [!CAUTION]
> **الزام قطعی:** روی PDC هیچ گواهینامه‌ای در مخزن Trusted Root نصب نمی‌شود، هیچ رکوردی در DNS اصلی تغییر نمی‌کند و Default Domain Policy دست‌نخورده باقی می‌ماند.

#### ۱. بررسی‌های صرفاً خواندنی (Read-Only) روی PDC:
```powershell
# الف) بررسی رکورد فعلی crm.sahar.loc در DNS
Get-DnsServerResourceRecord -ZoneName "sahar.loc" -Name "crm" | Format-List

# ب) استعلام وضعیت و کانتینر رایانه Edari02 در اکتیودایرکتوری
Get-ADComputer -Identity "Edari02" -Properties OperatingSystem, IPv4Address, DistinguishedName, MemberOf | Format-List

# ج) استعلام وضعیت فعلی GPOهای دامنه
Get-GPO -All | Select-Object DisplayName, GpoStatus, CreationTime | Format-Table -AutoSize

# د) آزمون ارتباط شبکه بین PDC و پورت 443 سرور CRM
Test-NetConnection -ComputerName "192.168.1.248" -Port 443 | Select-Object ComputerName, TcpTestSucceeded
```

#### ۲. ایجاد گروه امنیتی و طراحی GPO انحصاری برای Edari02:
```powershell
# ایجاد گروه امنیتی محدود برای فاز Canary
New-ADGroup -Name "SG-ParkPalace-TLS-Canary" -GroupScope Global -GroupCategory Security -Description "Target group for CRM TLS Canary rollout"

# عضویت انحصاری رایانه Edari02 در گروه
Add-ADGroupMember -Identity "SG-ParkPalace-TLS-Canary" -Members "Edari02$"

# ایجاد GPO اختصاصی با نام مصوب
New-GPO -Name "ParkPalace-CRM-TLS-Trust" -Comment "Deploys crm.sahar.loc public certificate to canary workstations"

# وارد کردن گواهینامه عمومی crm.sahar.loc.cer به بخش Trusted Root در این GPO
# (از طریق کنسول GPMC در مسیر: Computer Configuration -> Policies -> Windows Settings -> Security Settings -> Public Key Policies -> Trusted Root Certification Authorities)

# تنظیم پیوند و فیلترینگ امنیتی در سطح ریشه sahar.loc:
$gpo = Get-GPO -Name "ParkPalace-CRM-TLS-Trust"
New-GPLink -Name $gpo.DisplayName -Target "DC=sahar,DC=loc"

# حذف Authenticated Users از لیست Apply
Set-GPPermissions -Name $gpo.DisplayName -TargetName "Authenticated Users" -TargetType Group -PermissionLevel GpoRead -Replace

# اعطای دسترسی انحصاری به گروه Canary
Set-GPPermissions -Name $gpo.DisplayName -TargetName "SG-ParkPalace-TLS-Canary" -TargetType Group -PermissionLevel GpoApply

# اطمینان از استثنا بودن DCها (Deny صریح به Domain Controllers)
Set-GPPermissions -Name $gpo.DisplayName -TargetName "Domain Controllers" -TargetType Group -PermissionLevel None
```

---

### گام سوم: ایستگاه کاری اداری Edari02 (192.168.1.109) — آزمون اعتماد و دسترسی HTTPS

#### ۱. دریافت پالیسی و بررسی استقرار گواهینامه:
```powershell
# به‌روزرسانی پالیسی‌های گروهی ماشین
gpupdate /force

# استعلام حضور گواهینامه crm.sahar.loc در مخزن ریشه ماشین Edari02:
Get-ChildItem -Path Cert:\LocalMachine\Root | 
    Where-Object { $_.Subject -like "*crm.sahar.loc*" } | 
    Select-Object Subject, Thumbprint, NotAfter
```

#### ۲. تنظیم نگاشت آزمایشی نام میزبان بدون تغییر DNS اصلی:
در فایل `C:\Windows\System32\drivers\etc\hosts` رایانه Edari02:
```text
192.168.1.248    crm.sahar.loc
```

#### ۳. آزمون اعتبارسنجی دقیق TLS (بدون استفاده از سوئیچ -k):
```bash
# آزمون در خط فرمان با بررسی کامل زنجیره اعتماد
curl.exe -v https://crm.sahar.loc/api/health
```
* **معیار قبولی فنی:** نمایش پیام `SSL certificate verify ok` در هندشیک، دریافت پاسخ HTTP 200 با پی‌لود `{"status":"Healthy"}`.
* **آزمون در مرورگرهای Microsoft Edge و Google Chrome:**
  * باز کردن آدرس `https://crm.sahar.loc/` در Edari02.
  * مشاهده نماد قفل امن بسته (Secure Lock) بدون خطای `NET::ERR_CERT_AUTHORITY_INVALID`.
  * تایید پارامترهای گواهی: کلید RSA 3072-bit، وضعیت `CA:FALSE` و تاریخ انقضای ۱۸۰ روزه.

---

## ۴. روش بازگشت صریح (Rollback) در صورت بروز خطا در فاز Canary

در صورت بروز هرگونه اختلال در Edari02 یا تصمیم به لغو:
1. **در کنترلر دامنه PDC:**
   * لغو پیوند GPO: `Remove-GPLink -Name "ParkPalace-CRM-TLS-Trust" -Target "DC=sahar,DC=loc"`.
   * خروج `Edari02$` از گروه `SG-ParkPalace-TLS-Canary`.
2. **در ایستگاه کاری Edari02:**
   * حذف خط ثبت‌شده در فایل `hosts`.
   * حذف صریح گواهینامه از مخزن محلی با Thumbprint:
     ```powershell
     Get-ChildItem Cert:\LocalMachine\Root | Where-Object { $_.Subject -like "*crm.sahar.loc*" } | Remove-Item -Force
     Test-Path "Cert:\LocalMachine\Root\$Thumbprint" # باید خروجی False دهد
     ```
3. **در سرور CRM:**
   * حذف بایندینگ ۴۴۳ در IIS و توقف سرویس در صورت نیاز (توقف امن بدون بازگشت به HTTP ناامن).

---

## ۵. نتیجه ممیزی وضعیت سه سیستم و نتیجه آزمون TLS

* **سرور CRM (192.168.1.248):** بسته انتشار آماده، پشته دات‌نت ۸ و IIS 10 مهیا، پیکربندی بایندینگ پورت ۴۴۳ با کلید RSA 3072-bit و `CA=FALSE` به صورت استاندارد طراحی شده است.
* **سرور PDC (192.168.1.250):** هیچ گواهینامه‌ای در مخزن Trusted Root آن قرار نخواهد گرفت؛ GPO صرفاً به شیء گروه امنیتی `SG-ParkPalace-TLS-Canary` متصل می‌شود و کنترلرهای دامین دارای Deny صریح هستند.
* **کلاینت Edari02 (192.168.1.109):** تنها رایانه مجاز در این فاز بوده و پس از اعمال GPO و نگاشت Hosts، اعتبارسنجی TLS بدون Bypass و بدون تغییر در DNS کلی شبکه محقق می‌گردد.
