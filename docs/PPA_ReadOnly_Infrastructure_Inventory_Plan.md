# برنامه ممیزی و جمع‌آوری اطلاعات صرفاً خواندنی زیرساخت — فاز پیش‌استقرار
# ParkPalace CRM — Pre-Production Acceptance (PPA)
## Read-Only Infrastructure Inventory & Health Collection Plan

**پروژه:** سامانه جامع تدارکات و ارتباط با تأمین‌کنندگان پارک پالاس (ParkPalace Procurement CRM)  
**سند:** برنامه ممیزی زیرساخت با فرامین کاملاً خواندنی (Proposed Read-Only Inventory Plan)  
**مسیر پروژه:** `D:\ParkPlace`  
**هدف ارزیابی:** سرور کنترل‌کننده دامنه `pdc.sahar.loc` (`192.168.1.250`) یا سرور برنامه کاربردی هدف  
**حالت ممیزی:** طرح پیشنهادی جهت تصویب و اجرای دستی ادمین ارشد شبکه (DO NOT EXECUTE BY AGENT)  
**تاریخ انتشار:** ۲۶ مهر ۱۴۰۵ (2026-10-18)  
**نسخه:** ۱.۰.۰  

---

## ۱. تذکرات امنیتی و محدودیت‌های قطعی اجرا (Security Disclaimers)

> [!IMPORTANT]
> **قانون منع اجرای خودکار (Mandatory Prohibition):**  
> کلیه دستورات و اسکریپت‌های درج‌شده در این سند صرفاً **پیشنهادی** بوده و برای اجرای بعدی توسط ادمین رسمی سیستم با مجوز مکتوب تهیه شده‌اند. عامل هوش مصنوعی (Antigravity) و اسکریپت‌های خودکار به هیچ عنوان مجاز به اجرای این فرامین روی سرور PDC یا محیط عملیاتی نمی‌باشند.

### ویژگی‌های الزامی فرامین پیشنهادی:
1. **۱۰۰٪ بدون تغییر و عاری از اثر جانبی (Idempotent & Read-Only):** تمام دستورات صرفاً از Cmdletهای خواندنی (نظیر `Get-*`, `Test-*`, `Measure-*`) استفاده می‌کنند و هیچ دستوری از خانواده `Set`, `New`, `Remove`, `Restart`, `Enable`, `Install` در آنها وجود ندارد.
2. **عدم ایجاد قفل سیستمی یا سربار پردازشی:** کوئری‌ها محدود شده و از ایجاد اسکن‌های سنگین خودداری شده است.
3. **عدم افشای اطلاعات محرمانه (No Secret Extraction):** دستورات هیچ رمز عبور، کلید خصوصی (Private Key) یا هش هویتی را استخراج یا لاگ نمی‌کنند.

---

## ۲. مجموعه فرامین پیشنهادی جهت ممیزی زیرساخت (Read-Only Command Sets)

### ۲.۱. مشخصات سیستم‌عامل و ویرایش ویندوز سرور (OS & Build Inventory)
* **هدف:** تعیین دقیق نسخه ویندوز سرور، شماره بیلد و وضعیت فعال‌سازی.
* **سطح ریسک:** صفر (صرفاً استعلام WMI/CIM).
```powershell
# بررسی نگارش ویندوز سرور و بیلد
Get-CimInstance -ClassName Win32_OperatingSystem | 
    Select-Object Caption, Version, BuildNumber, OSArchitecture, LastBootUpTime, 
                  InstallDate, TotalVisibleMemorySize, FreePhysicalMemory | 
    Format-List
```

---

### ۲.۲. وضعیت اکتیودایرکتوری، نقش‌های FSMO و وضعیت Global Catalog
* **هدف:** اعتبارسنجی میزبانی نقش‌های حیاتی دامین بر روی سرور جاری و بررسی سلامت سرویس‌های AD DS.
* **سطح ریسک:** صفر (کوئری خواندنی Active Directory).
```powershell
# استعلام وضعیت نقش کنترل‌کننده دامنه و FSMO Roles
Import-Module ActiveDirectory -ErrorAction SilentlyContinue
Get-ADDomainController -Identity $env:COMPUTERNAME | 
    Select-Object Name, HostName, IPv4Address, OperatingSystem, IsGlobalCatalog, 
                  OperationMasterRoles, Site, Enabled | 
    Format-List

# بررسی نقش‌های پنج‌گانه FSMO در کل فارست
Get-ADDomain | Select-Object PDCEmulator, RIDMaster, InfrastructureMaster
Get-ADForest | Select-Object SchemaMaster, DomainNamingMaster
```

---

### ۲.۳. پایش و موجودی منابع سخت‌افزاری (CPU, RAM, Disks)
* **هدف:** سنجش ظرفیت پردازشی، میزان حافظه آزاد و فضای خالی درایو `D:` جهت میزبانی پایگاه داده و فایل‌ها.
* **سطح ریسک:** صفر.
```powershell
# مشخصات پردازنده و میزان استفاده
Get-CimInstance -ClassName Win32_Processor | 
    Select-Object Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, LoadPercentage | 
    Format-Table -AutoSize

# وضعیت دیسک‌ها و فضای آزاد
Get-CimInstance -ClassName Win32_LogicalDisk -Filter "DriveType=3" | 
    Select-Object DeviceID, VolumeName, 
                  @{Name="TotalSizeGB"; Expression={[math]::round($_.Size/1GB, 2)}}, 
                  @{Name="FreeSpaceGB"; Expression={[math]::round($_.FreeSpace/1GB, 2)}}, 
                  @{Name="PercentFree"; Expression={[math]::round(($_.FreeSpace/$_.Size)*100, 1)}} | 
    Format-Table -AutoSize
```

---

### ۲.۴. ممیزی سرویس وب IIS، سایت‌ها، استخرها و پورت‌های فعال
* **هدف:** تشخیص فعال بودن یا نبودن IIS، بررسی پورت‌های در حال گوش‌دادن و اطمینان از عدم تداخل با پورت ۸۸۸۸ یا ۸۰.
* **سطح ریسک:** صفر.
```powershell
# استعلام ویژگی‌های نصب‌شده مرتبط با IIS
Get-WindowsFeature -Name Web-* | Where-Object Installed | 
    Select-Object Name, DisplayName | 
    Format-Table -AutoSize

# بررسی سایت‌ها و بایندینگ‌های موجود در IIS (در صورت نصب بودن)
if (Get-Module -ListAvailable -Name WebAdministration) {
    Import-Module WebAdministration
    Get-Website | Select-Object ID, Name, State, PhysicalPath, Bindings | 
        Format-Table -AutoSize
    Get-ChildItem IIS:\AppPools | Select-Object Name, State, ManagedRuntimeVersion, ProcessModel | 
        Format-Table -AutoSize
} else {
    Write-Host "IIS WebAdministration module is NOT installed." -ForegroundColor Yellow
}

# بررسی وضعیت گوش دادن پورت‌های ۸۰، ۴۴۳، ۸۸۸۸ و ۱۴۳۳ روی شبکه
Get-NetTCPConnection -State Listen | 
    Where-Object { $_.LocalPort -in 80, 443, 8888, 1433, 5038, 53, 88, 389 } | 
    Select-Object LocalAddress, LocalPort, OwningProcess, @{Name="ProcessName"; Expression={(Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).ProcessName}} | 
    Sort-Object LocalPort | 
    Format-Table -AutoSize
```

---

### ۲.۵. ممیزی ران‌تایم و پکیج میزبانی دات‌نت (.NET Hosting Bundle Inventory)
* **هدف:** بررسی اینکه آیا .NET 8 Runtime و ASP.NET Core Module v2 (ANCM) بر روی سرور نصب شده است یا خیر.
* **سطح ریسک:** صفر.
```powershell
# بررسی نسخه‌های .NET نصب‌شده از طریق رجیستری و خط فرمان
dotnet --info 2>$null

# استعلام رجیستری جهت شناسایی ماژول ANCM در IIS
Get-ChildItem "HKLM:\SOFTWARE\Microsoft\IIS Extensions\IIS AspNetCore Module V2" -ErrorAction SilentlyContinue | 
    Select-Object Name, Property, Value
```

---

### ۲.۶. ممیزی گواهینامه‌های دیجیتال TLS/SSL در مخزن لوکال سرور
* **هدف:** بررسی وجود گواهینامه معتبر صادرشده توسط CA سازمان برای نام دامنه `crm.sahar.loc`.
* **سطح ریسک:** صفر (عدم استخراج کلید خصوصی).
```powershell
# فهرست گواهینامه‌های موجود در مخزن کامپیوتر محلی بدون استخراج کلید
Get-ChildItem -Path Cert:\LocalMachine\My | 
    Select-Object Subject, Issuer, NotBefore, NotAfter, HasPrivateKey, Thumbprint | 
    Format-Table -AutoSize
```

---

### ۲.۷. پایش سلامت رویدادهای سیستمی و خطاهای ۲۴ ساعت اخیر (Event Log Health)
* **هدف:** اطمینان از سلامت پایدار سرور، بررسی خطاهای سخت‌افزاری دیسک، سرویس AD DS و DNS.
* **سطح ریسک:** صفر.
```powershell
$Past24Hours = (Get-Date).AddHours(-24)

# خطاهای لاگ System
Get-WinEvent -FilterHashtable @{LogName='System'; Level=1,2; StartTime=$Past24Hours} -MaxEvents 30 -ErrorAction SilentlyContinue | 
    Select-Object TimeCreated, ProviderName, Id, Message | 
    Format-Table -Wrap

# خطاهای سرویس Directory Service (AD DS)
Get-WinEvent -FilterHashtable @{LogName='Directory Service'; Level=1,2; StartTime=$Past24Hours} -MaxEvents 20 -ErrorAction SilentlyContinue | 
    Select-Object TimeCreated, Id, Message | 
    Format-Table -Wrap

# خطاهای سرویس DNS Server
Get-WinEvent -FilterHashtable @{LogName='DNS Server'; Level=1,2; StartTime=$Past24Hours} -MaxEvents 20 -ErrorAction SilentlyContinue | 
    Select-Object TimeCreated, Id, Message | 
    Format-Table -Wrap
```

---

### ۲.۸. اعتبارسنجی شبکه و دسترسی‌پذیری پورت‌های سرورهای وابسته
* **هدف:** سنجش ارتباط لایه شبکه از مبدأ سرور میزبان به سمت SQL Server، سرور سپیدار و سرور ایزابل.
* **سطح ریسک:** صفر (صرفاً TCP Handshake استاندارد بدون ارسال پی‌لود).
```powershell
# آزمون اتصال به سرور پایگاه داده (Port 1433)
Test-NetConnection -ComputerName "192.168.1.250" -Port 1433 -WarningAction SilentlyContinue | 
    Select-Object ComputerName, RemoteAddress, RemotePort, TcpTestSucceeded

# آزمون اتصال به سرور مالی سپیدار (Port 62753 روی 192.168.1.242)
Test-NetConnection -ComputerName "192.168.1.242" -Port 62753 -WarningAction SilentlyContinue | 
    Select-Object ComputerName, RemoteAddress, RemotePort, TcpTestSucceeded

# آزمون اتصال به سرور مرکز تماس ایزابل (Port 5038 روی 192.168.1.241)
Test-NetConnection -ComputerName "192.168.1.241" -Port 5038 -WarningAction SilentlyContinue | 
    Select-Object ComputerName, RemoteAddress, RemotePort, TcpTestSucceeded
```

---

## ۳. فرمت پیشنهادی لاگ‌گیری و تجمیع نتایج توسط ادمین

جهت اجرای منسجم توسط ادمین سیستم، پیشنهاد می‌شود دستورات فوق در یک اسکریپت با قالب‌بندی متنی ذخیره شده و خروجی آن به صورت امن در قالب فایل متنی ذخیره گردد:
```powershell
# نمونه دستور اجرای تجمیعی توسط ادمین در کنسول امنیتی:
# .\Invoke-PpaReadOnlyAudit.ps1 | Out-File -FilePath "C:\PPA_Audit_Report_$((Get-Date).ToString('yyyyMMdd_HHmm')).txt" -Encoding utf8
```

## ۴. جمع‌بندی
این طرح ممیزی به ادمین‌های زیرساخت شرکت سحر امکان می‌دهد بدون هیچ‌گونه ریسک تغییرات، ریبوت یا افت کارایی، وضعیت سلامت سرور را ارزیابی نموده و مستندات لازم را برای تصمیم‌گیری معماری فراهم آورند.
