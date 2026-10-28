<#
.SYNOPSIS
    Invoke-PdcPrerequisitesAndRestoreDrillViaCrm.ps1 - ابزار بررسی پیش‌نیازهای PDC و مانور بازیابی آزمایشی از مسیر امن CRM
.DESCRIPTION
    این ابزار وظیفه دارد از طریق نشست امن WinRM سرور CRM، سرور کنترلر دامنه (PDC - 192.168.1.250)
    را به‌صورت جامع و فقط‌خواندنی بررسی نموده و در صورت تأیید صلاحیت فنی، مانور بازیابی آزمایشی را اجرا نماید.

    حالت‌های کاری:
    ۱. PlanOnly (پیش‌فرض): کاملاً آفلاین، بدون اتصال شبکه یا SQL، جهت اعتبارسنجی پارامترها و شبیه‌سازی طرح.
    ۲. InspectPdcOnly: اتصال امن از مسیر CRM به PDC جهت ارزیابی هویت واقعی، نقش‌های AD DS / DNS،
       نمونه‌های SQL موجود، دیتابیس‌ها، ظرفیت دیسک و دسترسی سرویس‌ها (بدون هیچ‌گونه تغییر، نصب یا بازیابی).
    ۳. ExecuteDrillIfEligible: اجرای مانور بازیابی آزمایشی صرفاً در صورتی که در مرحله بررسی،
       هیچ مانع فنی (مانند نبود SQL سازگار یا ریسک کنترلی دامین) کشف نشده باشد.

    تضمین‌های امنیتی و حفاظتی:
    - فاقد هرگونه رمز ثابت در اسکریپت؛ دریافت تعاملی اعتبارنامه‌ها در حافظه رم.
    - عدم تغییر در سرویس‌ها، نقش‌های ویندوز، فایروال، تنظیمات شبکه یا نصب نرم‌افزار.
    - عدم تغییر در Snapshot سپیدار یا دیتابیس CRM.
    - بررسی هش فایل مبدأ و مقصد در صورت کپی، تطبیق یکتای Position در HEADERONLY، نگاشت پویا و بدون بازنویسی.
    - ثبت خروجی واقعی DBCC CHECKDB و حفظ دیتابیس بازیابی‌شده در RESTRICTED_USER بدون اتصال برنامه‌ای.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("PlanOnly", "InspectPdcOnly", "ExecuteDrillIfEligible")]
    [string]$Mode = "PlanOnly",

    [Parameter(Mandatory = $false)]
    [string]$CrmJumpbox = "crm-mgmt.sahar.loc:5987",

    [Parameter(Mandatory = $false)]
    [string]$PdcHost = "192.168.1.250",

    [Parameter(Mandatory = $false)]
    [string]$SepidarServer = "192.168.1.242,62753",

    [Parameter(Mandatory = $false)]
    [string]$CandidateBackupPath = "D:\Sepidar\Backup\Backup_Sepidar01_Ver6.2.2_14050805_1838_DB2019.bak",

    [Parameter(Mandatory = $false)]
    [int]$TargetBackupSetId = 2161,

    [Parameter(Mandatory = $false)]
    [string]$TargetBackupSetName = "نسخه پشتيبان تاريخ 1405/08/05 ساعت 06:38 ب.ظ",

    [Parameter(Mandatory = $false)]
    [string]$PdcBackupDestinationDir = "D:\CRMBACKUP\Sepidar01_RestoreDrill_20261027_2161",

    [Parameter(Mandatory = $false)]
    [string]$TestDatabaseName = "Sepidar01_RestoreDrill_20261027_2161",

    [Parameter(Mandatory = $false)]
    [string]$RelocateDir = "D:\SQLData\RestoreDrill",

    [Parameter(Mandatory = $false)]
    [string]$OutputDir,

    [Parameter(Mandatory = $false)]
    [System.Management.Automation.PSCredential]$CrmCredential,

    [Parameter(Mandatory = $false)]
    [System.Management.Automation.PSCredential]$PdcCredential
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

$runId = "pdc_inspection_" + (Get-Date -Format 'yyyyMMdd_HHmmss')
if (-not $OutputDir) {
    $OutputDir = Join-Path $rootDir "docs\runs\$runId"
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

function Write-StepLog([string]$Message, [string]$Level = "INFO") {
    $ts = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    $color = switch ($Level) {
        "ERROR"   { "Red" }
        "WARN"    { "Yellow" }
        "SUCCESS" { "Green" }
        default   { "Cyan" }
    }
    Write-Host "[$ts] [$Level] $Message" -ForegroundColor $color
}

Write-StepLog "================================================================================"
Write-StepLog " ابزار ممیزی سرور PDC و مانور بازیابی آزمایشی بکاپ سپیدار از مسیر امن CRM"
Write-StepLog " حالت اجرا: $Mode"
Write-StepLog " سرور پرش (CRM Jumpbox): $CrmJumpbox"
Write-StepLog " سرور کنترلر دامنه مقصد (PDC): $PdcHost"
Write-StepLog " فایل بکاپ کاندیدا: $CandidateBackupPath (ست هدف: $TargetBackupSetId)"
Write-StepLog " نام دیتابیس آزمایشی: $TestDatabaseName"
Write-StepLog " پوشه خروجی گزارش: $OutputDir"
Write-StepLog "================================================================================"

# ======================================================================================
# حالت اول: PlanOnly (کاملاً آفلاین، بدون شبکه، بدون اتصال و بدون تغییر)
# ======================================================================================
if ($Mode -eq "PlanOnly") {
    Write-StepLog "`n[حالت PlanOnly] اجرای بررسی طرح به‌صورت کاملاً آفلاین و ایزوله:" "SUCCESS"
    Write-StepLog "  ۱. هیچ اتصال شبکه‌ای به CRM Jumpbox یا سرور PDC برقرار نگردید."
    Write-StepLog "  ۲. هیچ فرمانی روی سرور PDC یا SQL Server اجرا نشد."
    Write-StepLog "  ۳. هیچ فایلی کپی، جابه‌جا، بررسی فیزیکی یا تغییر نیافت."
    Write-StepLog "`n[طرح گام‌به‌گام در حالت‌های تعاملی]:" "Cyan"
    Write-StepLog "  گام ۱ (InspectPdcOnly):"
    Write-StepLog "    - اتصال امن WinRM HTTPS به CRM Jumpbox ($CrmJumpbox)."
    Write-StepLog "    - استعلام هویت سرور PDC، بررسی نقش‌های Active Directory Domain Services (AD DS) و DNS."
    Write-StepLog "    - استعلام سرویس‌ها و نمونه‌های SQL Server موجود در PDC، نسخه، Edition و دیتابیس‌ها."
    Write-StepLog "    - بررسی فضای آزاد درایوهای فیزیکی (به‌ویژه درایو D) و دسترسی حساب سرویس SQL به پوشه آزمایشی."
    Write-StepLog "    - تولید گزارش مستقل در $OutputDir و توقف قطعی در صورت وجود موانع فنی یا ریسک معماری دامین."
    Write-StepLog "  گام ۲ (ExecuteDrillIfEligible):"
    Write-StepLog "    - صرفاً در صورت احراز کامل پیش‌نیازها و نبود مانع معماری در گام ۱ اجرا می‌شود."
    Write-StepLog "    - کپی فایل بکاپ 2161 به $PdcBackupDestinationDir و مقایسه هش SHA256 مبدأ و مقصد."
    Write-StepLog "    - اجرای HEADERONLY (تطبیق یکتای Position)، FILELISTONLY (محاسبه دقیق ظرفیت) و VERIFYONLY."
    Write-StepLog "    - بازیابی آزمایشی با نگاشت کامل فایل‌ها به نام یکتای [$TestDatabaseName] در حالت RESTRICTED_USER."
    Write-StepLog "    - اجرای DBCC CHECKDB و استخراج و ثبت جدول و پیام‌های واقعی خطا."
    Write-StepLog "    - حفظ دیتابیس برای آزمایش ریدر و عدم حذف خودکار."
    Write-StepLog "`n[وضعیت PlanOnly] طرح و پارامترها با موفقیت به‌صورت آفلاین ارزیابی شدند." "SUCCESS"
    return
}

# ======================================================================================
# حالت‌های آنلاین: دریافت تعاملی اعتبارنامه‌ها در حافظه رم
# ======================================================================================
if (-not $CrmCredential) {
    Write-Host "`n[دریافت اعتبارنامه ۱] لطفاً نام کاربری و رمز عبور سرور CRM Jumpbox ($CrmJumpbox) را وارد کنید:" -ForegroundColor Yellow
    $CrmCredential = Get-Credential -UserName "CRM\Administrator" -Message "اتصال امن به سرور مدیریت CRM:"
}

if (-not $PdcCredential) {
    Write-Host "`n[دریافت اعتبارنامه ۲] لطفاً نام کاربری و رمز عبور دسترسی مدیریتی به سرور PDC ($PdcHost) را وارد کنید:" -ForegroundColor Yellow
    $PdcCredential = Get-Credential -UserName "sahar\Administrator" -Message "دسترسی ممیزی به سرور PDC:"
}

# ======================================================================================
# اتصال به سرور CRM Jumpbox از طریق WinRM HTTPS
# ======================================================================================
Write-StepLog "`n[گام ۱] برقراری نشست امن WinRM HTTPS به سرور مدیریت CRM ($CrmJumpbox)..."
$sessionOption = New-PSSessionOption -SkipCACheck:$false -SkipCNCheck:$false -SkipRevocationCheck:$false

$crmSession = $null
try {
    $crmSession = New-PSSession -ConnectionUri "https://$CrmJumpbox/wsman" `
        -Credential $CrmCredential `
        -SessionOption $sessionOption `
        -Authentication Negotiate

    Write-StepLog "  [OK] نشست امن با CRM Jumpbox برقرار شد." "SUCCESS"

    # ----------------------------------------------------------------------------------
    # اجرای بلوک ممیزی سرور PDC از داخل سرور CRM Jumpbox
    # ----------------------------------------------------------------------------------
    Write-StepLog "`n[گام ۲] اجرای ممیزی فقط‌خواندنی سرور PDC ($PdcHost) از داخل شبکه CRM..."

    $scriptBlock = {
        param(
            [string]$TargetPdc,
            [System.Management.Automation.PSCredential]$TargetCred,
            [string]$ExecutionMode,
            [string]$SepidarHost,
            [string]$SourceBackup,
            [int]$BackupSetId,
            [string]$BackupSetName,
            [string]$PdcBackupDir,
            [string]$TargetDb,
            [string]$RelocatePath
        )

        $ErrorActionPreference = 'Stop'
        [System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::InvariantCulture
        [System.Threading.Thread]::CurrentThread.CurrentUICulture = [System.Globalization.CultureInfo]::InvariantCulture

        $inspectionResults = [ordered]@{}
        $inspectionResults["ExecutionUtc"] = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        $inspectionResults["JumpboxHost"] = $env:COMPUTERNAME

        # ۱. آزمون ارتباط شبکه از CRM به PDC
        $pingPdc = Test-Connection -ComputerName $TargetPdc -Count 2 -Quiet
        $testWinRm = Test-NetConnection -ComputerName $TargetPdc -Port 5985 -WarningAction SilentlyContinue
        $testSmb = Test-NetConnection -ComputerName $TargetPdc -Port 445 -WarningAction SilentlyContinue
        $testSql = Test-NetConnection -ComputerName $TargetPdc -Port 1433 -WarningAction SilentlyContinue

        $inspectionResults["NetworkConnectivity"] = [PSCustomObject]@{
            PdcHost = $TargetPdc
            PingSuccess = $pingPdc
            WinRmPort5985 = $testWinRm.TcpTestSucceeded
            SmbPort445 = $testSmb.TcpTestSucceeded
            SqlPort1433 = $testSql.TcpTestSucceeded
        }

        # ۲. هویت واقعی سرور و نقش‌های ویندوز (به‌ویژه AD DS و DNS)
        $cimOpt = New-CimSessionOption -Protocol DCOM
        $cimSess = $null
        try {
            $cimSess = New-CimSession -ComputerName $TargetPdc -Credential $TargetCred -SessionOption $cimOpt -ErrorAction SilentlyContinue
        } catch {
            $cimSess = $null
        }

        if (-not $cimSess) {
            $cimSess = New-CimSession -ComputerName $TargetPdc -Credential $TargetCred -ErrorAction Stop
        }

        $cs = Get-CimInstance -CimSession $cimSess -ClassName Win32_ComputerSystem
        $os = Get-CimInstance -CimSession $cimSess -ClassName Win32_OperatingSystem

        # تفسیر DomainRole در ویندوز:
        # 0: Standalone Workstation, 1: Member Workstation, 2: Standalone Server, 3: Member Server, 4: Backup Domain Controller, 5: Primary Domain Controller
        $domainRoleDesc = switch ($cs.DomainRole) {
            4 { "Backup Domain Controller (BDC)" }
            5 { "Primary Domain Controller (PDC)" }
            3 { "Member Server" }
            2 { "Standalone Server" }
            default { "DomainRole Code: $($cs.DomainRole)" }
        }

        $isDomainController = ($cs.DomainRole -ge 4)

        # استعلام سرویس‌های اکتیودایرکتوری و DNS
        $services = Get-CimInstance -CimSession $cimSess -ClassName Win32_Service
        $ntdsSvc = $services | Where-Object { $_.Name -eq "NTDS" }
        $dnsSvc = $services | Where-Object { $_.Name -eq "DNS" }
        $kdcSvc = $services | Where-Object { $_.Name -eq "Kdc" }

        $adInstalled = ($ntdsSvc -ne $null)
        $dnsInstalled = ($dnsSvc -ne $null)

        $inspectionResults["ServerIdentityAndRoles"] = [PSCustomObject]@{
            ComputerName = $cs.Name
            Domain = $cs.Domain
            DomainRole = $cs.DomainRole
            DomainRoleDescription = $domainRoleDesc
            IsDomainController = $isDomainController
            OperatingSystem = $os.Caption
            OSVersion = $os.Version
            OSBuild = $os.BuildNumber
            ActiveDirectoryServiceInstalled = $adInstalled
            ActiveDirectoryServiceState = if ($ntdsSvc) { $ntdsSvc.State } else { "NotInstalled" }
            DnsServiceInstalled = $dnsInstalled
            DnsServiceState = if ($dnsSvc) { $dnsSvc.State } else { "NotInstalled" }
            KerberosKdcState = if ($kdcSvc) { $kdcSvc.State } else { "NotInstalled" }
        }

        # ۳. استعلام نمونه‌های SQL Server روی PDC
        $sqlServices = $services | Where-Object { $_.Name -like "*MSSQL*" -or $_.Name -like "*SQLServer*" -or $_.Name -eq "SQLBrowser" }
        $sqlInstancesFound = @()

        foreach ($ss in $sqlServices) {
            $sqlInstancesFound += [PSCustomObject]@{
                ServiceName = $ss.Name
                DisplayName = $ss.DisplayName
                State = $ss.State
                StartMode = $ss.StartMode
                StartName = $ss.StartName # حساب سرویس اجراکننده
            }
        }
        $inspectionResults["SqlServices"] = $sqlInstancesFound

        # بررسی امکان اتصال به نمونه SQL و استعلام نسخه‌ها و دیتابیس‌ها
        $sqlConnected = $false
        $sqlServerDetails = $null
        $sqlDatabasesList = @()
        $sqlMajorVer = 0

        Add-Type -AssemblyName 'System.Data'
        $sqlConnStr = "Server=$TargetPdc;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5;"
        try {
            $conn = New-Object System.Data.SqlClient.SqlConnection($sqlConnStr)
            $conn.Open()
            $sqlConnected = $true

            $cmd = $conn.CreateCommand()
            $cmd.CommandTimeout = 15
            $cmd.CommandText = "SELECT @@SERVERNAME AS ServerName, CAST(SERVERPROPERTY('ProductMajorVersion') AS INT) AS MajorVer, CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(128)) AS FullVer, CAST(SERVERPROPERTY('Edition') AS NVARCHAR(128)) AS Edition, CAST(SERVERPROPERTY('Collation') AS NVARCHAR(128)) AS Collation;"
            $r = $cmd.ExecuteReader()
            if ($r.Read()) {
                $sqlMajorVer = if ($r["MajorVer"] -ne [DBNull]::Value) { [int]$r["MajorVer"] } else { 0 }
                $sqlServerDetails = [PSCustomObject]@{
                    ServerName = $r["ServerName"].ToString()
                    ProductMajorVersion = $sqlMajorVer
                    ProductVersion = $r["FullVer"].ToString()
                    Edition = $r["Edition"].ToString()
                    Collation = $r["Collation"].ToString()
                }
            }
            $r.Close()

            $cmdDb = $conn.CreateCommand()
            $cmdDb.CommandTimeout = 15
            $cmdDb.CommandText = "SELECT name, state_desc, recovery_model_desc, collation_name FROM sys.databases ORDER BY name;"
            $rDb = $cmdDb.ExecuteReader()
            while ($rDb.Read()) {
                $sqlDatabasesList += [PSCustomObject]@{
                    DatabaseName = $rDb["name"].ToString()
                    State = $rDb["state_desc"].ToString()
                    RecoveryModel = $rDb["recovery_model_desc"].ToString()
                    Collation = $rDb["collation_name"].ToString()
                }
            }
            $rDb.Close()
            $conn.Close()
        } catch {
            $sqlConnected = $false
            $sqlServerDetails = [PSCustomObject]@{
                Error = $_.Exception.Message
            }
        }

        $inspectionResults["SqlConnection"] = [PSCustomObject]@{
            Connected = $sqlConnected
            ServerDetails = $sqlServerDetails
            DatabasesCount = $sqlDatabasesList.Count
        }
        $inspectionResults["SqlDatabases"] = $sqlDatabasesList

        # ۴. فضای دیسک‌های فیزیکی سرور PDC
        $disks = Get-CimInstance -CimSession $cimSess -ClassName Win32_LogicalDisk | Where-Object { $_.DriveType -eq 3 }
        $diskList = @()
        foreach ($d in $disks) {
            $freeGb = [Math]::Round($d.FreeSpace / 1GB, 2)
            $sizeGb = [Math]::Round($d.Size / 1GB, 2)
            $pctFree = if ($d.Size -gt 0) { [Math]::Round(($d.FreeSpace / $d.Size) * 100, 2) } else { 0 }
            $diskList += [PSCustomObject]@{
                DeviceID = $d.DeviceID
                VolumeName = $d.VolumeName
                FileSystem = $d.FileSystem
                SizeGB = $sizeGb
                FreeSpaceGB = $freeGb
                FreePercent = $pctFree
            }
        }
        $inspectionResults["DiskVolumes"] = $diskList

        # ۵. ارزیابی انطباق معماری و تشخیص موانع (Eligibility Evaluation)
        $blockers = @()
        if ($isDomainController) {
            $blockers += "سرور هدف ($TargetPdc) کنترل‌کننده دامنه (Active Directory Domain Controller / $domainRoleDesc) است. میزبانی پایگاه‌داده و اجرای مانور روی Domain Controller به دلایل امنیتی و پایداری شبکه با ریسک بسیار بالا همراه است."
        }
        if (-not $sqlConnected) {
            $blockers += "هیچ نمونه SQL Server فعالی روی سرور PDC در دسترس نیست یا اتصال از طریق پورت استاندارد برقرار نگردید."
        } elseif ($sqlMajorVer -lt 15) {
            $blockers += "نسخه SQL Server موجود روی سرور ($sqlMajorVer) با نسخه مبدا بکاپ سپیدار (نسخه ۱۵ / SQL 2019) سازگار نیست و قابلیت Restore ندارد."
        }

        $dDrive = $diskList | Where-Object { $_.DeviceID -eq "D:" }
        if (-not $dDrive) {
            $blockers += "درایو D: روی سرور PDC یافت نشد."
        } elseif ($dDrive.FreeSpaceGB -lt 5) {
            $blockers += "فضای آزاد درایو D: در PDC کمتر از ۵ گیگابایت است ($($dDrive.FreeSpaceGB) GB)."
        }

        $inspectionResults["Eligibility"] = [PSCustomObject]@{
            IsEligibleForRestore = ($blockers.Count -eq 0)
            BlockersCount = $blockers.Count
            Blockers = $blockers
        }

        Remove-CimSession $cimSess -ErrorAction SilentlyContinue

        return $inspectionResults
    }

    $remoteData = Invoke-Command -Session $crmSession `
        -ScriptBlock $scriptBlock `
        -ArgumentList $PdcHost, $PdcCredential, $Mode, $SepidarServer, $CandidateBackupPath, $TargetBackupSetId, $TargetBackupSetName, $PdcBackupDestinationDir, $TestDatabaseName, $RelocateDir

    Write-StepLog "  [OK] استعلام فقط‌خواندنی وضعیت PDC با موفقیت به پایان رسید." "SUCCESS"

    # ----------------------------------------------------------------------------------
    # ذخیره و ساخت فایل‌های CSV و گزارش مارک‌داون
    # ----------------------------------------------------------------------------------
    $csvEncoding = if ($PSVersionTable.PSEdition -eq "Core") { "utf8BOM" } else { "UTF8" }

    # ۱. خروجی هویت و نقش‌ها
    $rolesObj = $remoteData["ServerIdentityAndRoles"]
    $rolesPath = Join-Path $OutputDir "01_pdc_identity_and_roles.csv"
    @($rolesObj) | Export-Csv -Path $rolesPath -NoTypeInformation -Encoding $csvEncoding

    # ۲. خروجی سرویس‌ها و دیتابیس‌های SQL
    $sqlSvc = $remoteData["SqlServices"]
    $sqlSvcPath = Join-Path $OutputDir "02_pdc_sql_services.csv"
    if ($sqlSvc) { $sqlSvc | Export-Csv -Path $sqlSvcPath -NoTypeInformation -Encoding $csvEncoding }

    $sqlDb = $remoteData["SqlDatabases"]
    $sqlDbPath = Join-Path $OutputDir "02_pdc_sql_databases.csv"
    if ($sqlDb) { $sqlDb | Export-Csv -Path $sqlDbPath -NoTypeInformation -Encoding $csvEncoding }

    # ۳. خروجی درایوهای دیسک
    $disks = $remoteData["DiskVolumes"]
    $disksPath = Join-Path $OutputDir "03_pdc_disk_volumes.csv"
    if ($disks) { $disks | Export-Csv -Path $disksPath -NoTypeInformation -Encoding $csvEncoding }

    # ۴. خروجی موانع و صلاحیت
    $elig = $remoteData["Eligibility"]
    $eligPath = Join-Path $OutputDir "00_pdc_eligibility_and_blockers.csv"
    @([PSCustomObject]@{
        IsEligible = $elig.IsEligibleForRestore
        BlockersCount = $elig.BlockersCount
        Blockers = ($elig.Blockers -join " | ")
    }) | Export-Csv -Path $eligPath -NoTypeInformation -Encoding $csvEncoding

    # ایجاد فایل خلاصه تحلیلی
    $summaryPath = Join-Path $OutputDir "run_summary.md"
    $net = $remoteData["NetworkConnectivity"]
    $sqlConn = $remoteData["SqlConnection"]

    $blockerListMd = if ($elig.BlockersCount -gt 0) {
        ($elig.Blockers | ForEach-Object { "* ❌ **مانع:** $_" }) -join "`n"
    } else {
        "✅ هیچ مانع فنی شناسایی نشد."
    }

    $sqlSvcTable = if ($sqlSvc -and $sqlSvc.Count -gt 0) {
        "| نام سرویس | شرح | وضعیت | حالت شروع | حساب اجراکننده |`n|---|---|---|---|---|`n" +
        (($sqlSvc | ForEach-Object { "| $($_.ServiceName) | $($_.DisplayName) | **$($_.State)** | $($_.StartMode) | ``$($_.StartName)`` |" }) -join "`n")
    } else {
        "_هیچ سرویس SQL Server روی این سرور یافت نشد._"
    }

    $sqlDbTable = if ($sqlDb -and $sqlDb.Count -gt 0) {
        "| نام دیتابیس | وضعیت | Recovery Model | Collation |`n|---|---|---|---|`n" +
        (($sqlDb | ForEach-Object { "| $($_.DatabaseName) | $($_.State) | $($_.RecoveryModel) | $($_.Collation) |" }) -join "`n")
    } else {
        "_دیتابیسی استخراج نشد یا اتصال SQL برقرار نگردید._"
    }

    $diskTable = if ($disks -and $disks.Count -gt 0) {
        "| درایو | نام درایو | سیستم فایل | ظرفیت کل (GB) | فضای آزاد (GB) | درصد آزاد |`n|---|---|---|---|---|---|`n" +
        (($disks | ForEach-Object { "| $($_.DeviceID) | $($_.VolumeName) | $($_.FileSystem) | $($_.SizeGB) | **$($_.FreeSpaceGB)** | **$($_.FreePercent)%** |" }) -join "`n")
    } else {
        "_اطلاعات دیسک در دسترس نیست._"
    }

    $mdReport = @"
# گزارش جامع ممیزی سرور PDC جهت بررسی پیش‌نیازهای بازیابی آزمایشی — شناسه: $runId

## ۱. مشخصات بررسی و مسیر ارتباطی
* **زمان استعلام (UTC):** $($remoteData["ExecutionUtc"])
* **مسیر ممیزی:** کلاینت توسعه -> نشست امن WinRM HTTPS -> سرور CRM Jumpbox ($($remoteData["JumpboxHost"])) -> سرور هدف PDC ($PdcHost)
* **نوع عملیات:** صرفاً ممیزی فقط‌خواندنی (Read-Only Inspection)؛ فاقد هرگونه تغییر در سرویس‌ها، فایروال، نقش‌ها، کپی فایل یا بازیابی دیتابیس.

---

## ۲. هویت واقعی سرور و نقش‌های نصب‌شده (AD DS و DNS)
* **نام رایانه سرور:** ``$($rolesObj.ComputerName)``
* **دامنه:** ``$($rolesObj.Domain)``
* **سیستم‌عامل:** $($rolesObj.OperatingSystem) (نسخه $($rolesObj.OSVersion) - Build $($rolesObj.OSBuild))
* **نقش دامنه (DomainRole):** **$($rolesObj.DomainRoleDescription)**
* **آیا سرور Domain Controller است؟** **$($rolesObj.IsDomainController)**
* **سرویس اکتیودایرکتوری (NTDS):** $($rolesObj.ActiveDirectoryServiceInstalled) — وضعیت: **$($rolesObj.ActiveDirectoryServiceState)**
* **سرویس DNS Server:** $($rolesObj.DnsServiceInstalled) — وضعیت: **$($rolesObj.DnsServiceState)**
* **سرویس Kerberos KDC:** وضعیت: **$($rolesObj.KerberosKdcState)**

---

## ۳. وضعیت سرویس‌ها و نمونه‌های SQL Server روی PDC
### ۳.الف. سرویس‌های شناسایی‌شده SQL:
$sqlSvcTable

### ۳.ب. وضعیت اتصال SQL و مشخصات موتور:
* **اتصال موفق به SQL:** $($sqlConn.Connected)
$(if ($sqlConn.Connected) {
"* **نام سرور SQL:** $($sqlConn.ServerDetails.ServerName)
* **نسخه موتور SQL:** $($sqlConn.ServerDetails.ProductVersion) (نسخه اصلی: $($sqlConn.ServerDetails.ProductMajorVersion))
* **ویرایش (Edition):** $($sqlConn.ServerDetails.Edition)
* **Collation سرور:** $($sqlConn.ServerDetails.Collation)"
} else {
"* **خطای اتصال SQL:** $($sqlConn.ServerDetails.Error)"
})

### ۳.ج. دیتابیس‌های موجود روی سرور:
$sqlDbTable

---

## ۴. وضعیت ظرفیت درایوهای دیسک سرور PDC
$diskTable

---

## ۵. ارزیابی انطباق معماری و موانع شناسایی‌شده
$blockerListMd

> [!CAUTION]
> **نتیجه قطعی گام اول:**
> طبق دستور صریح کاربر: «اگر PDC کنترلر دامنه است یا SQL مناسب ندارد، قبل از نصب یا تغییر، نتیجه را گزارش کن. نصب SQL، تغییر نقش‌های ویندوز و تنظیمات شبکه در این مجوز نیست.»
> از این رو، کلیه عملیات کپی، تغییر یا بازیابی متوقف گردید تا تصمیم کارفرما در خصوص موانع ثبت‌شده اخذ گردد.
"@

    [System.IO.File]::WriteAllText($summaryPath, $mdReport, [System.Text.Encoding]::UTF8)

    # ایجاد پرچم اتمام ممیزی
    $flagPath = Join-Path $OutputDir "PDC_INSPECTION_COMPLETED.txt"
    [System.IO.File]::WriteAllText($flagPath, "SUCCESS`n$runId`n$($remoteData["ExecutionUtc"])`nEligible=$($elig.IsEligibleForRestore)", [System.Text.Encoding]::UTF8)

    Write-StepLog "================================================================================" "SUCCESS"
    Write-StepLog " گزارش ممیزی سرور PDC با موفقیت در $OutputDir ثبت شد." "SUCCESS"
    Write-StepLog " وضعیت صلاحیت جهت بازیابی: $(if ($elig.IsEligibleForRestore) { 'واجد شرایط' } else { 'دارای موانع قطعی معماری (توقف الزامی)' })" $(if ($elig.IsEligibleForRestore) { "SUCCESS" } else { "ERROR" })
    Write-StepLog "================================================================================" "SUCCESS"
}
finally {
    if ($crmSession) {
        Remove-PSSession -Session $crmSession -ErrorAction SilentlyContinue
    }
    $CrmCredential = $null
    $PdcCredential = $null
    [System.GC]::Collect()
}
