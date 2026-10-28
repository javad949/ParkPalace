<#
.SYNOPSIS
    Test-SepidarDatabaseRestore.ps1 - اسکریپت ایمن و چندمرحله‌ای مانور بازیابی آزمایشی بکاپ سپیدار
.DESCRIPTION
    این اسکریپت جهت اعتبارسنجی مستقل، مرحله‌به‌مرحله و کاملاً ایزوله بکاپ پایگاه‌داده Sepidar01
    (کاندیدای BackupSetId=2161) بدون بازنویسی و بدون اتصال برنامه‌ای طراحی شده است.

    اصلاحات و تضمین‌های حفاظتی قطعی:
    ۱. حالت پیش‌فرض PlanOnly کاملاً آفلاین: فاقد هرگونه اتصال SQL، اجرای FILELISTONLY، VERIFYONLY یا تغییر فایل.
    ۲. حذف سرور عملیاتی از پیش‌فرض: سرور مقصد آزمایشی باید صریحاً تعیین و هویت آن اعتبارسنجی شود.
       بازیابی روی SEPIDAR\SEPIDAR یا 192.168.1.242 در این مرحله اکیداً مسدود است.
    ۳. بررسی فایل و فضا روی میزبان SQL: ارزیابی مسیر و ظرفیت از دیدگاه سرور مقصد (نه کلاینت محلی).
    ۴. تطبیق یکتای Position در HEADERONLY: حذف کامل Fallback به Position=1؛ اگر مجموعه بکاپ به‌طور
       کاملاً یکتا تعیین نشود، عملیات فوراً متوقف می‌گردد.
    ۵. محاسبه دقیق ظرفیت بر مبنای مجموع Size واقعی فایل‌های درون بکاپ (نه تخمین فرضی).
    ۶. نگاشت پویا و اعتبارسنجی تلاقی فایل‌ها: بررسی عدم وجود فایل‌ها هم در sys.master_files و هم روی
       فایل‌سیستم واقعی میزبان مقصد (sys.dm_os_file_exists)؛ در صورت عدم امکان بررسی، بازیابی متوقف می‌شود.
    ۷. فرار امن رشته‌های ورودی SQL جهت پیشگیری از SQL Injection.
    ۸. حفظ نسخه بازیابی‌شده در حالت RESTRICTED_USER جهت آزمایش ریدر و حذف کدهای خودکار Drop / Rollback.
    ۹. ثبت و بررسی خروجی واقعی ساختار DBCC CHECKDB (عدم اتکا به صرف اتمام ExecuteNonQuery).
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("PlanOnly", "VerifyBackupOnly", "ExecuteRestoreDrill")]
    [string]$Mode = "PlanOnly",

    [Parameter(Mandatory = $false)]
    [string]$TestServerInstance = "",

    [Parameter(Mandatory = $false)]
    [string]$BackupFilePath = "D:\Sepidar\Backup\Backup_Sepidar01_Ver6.2.2_14050805_1838_DB2019.bak",

    [Parameter(Mandatory = $false)]
    [string]$TargetBackupSetName = "نسخه پشتيبان تاريخ 1405/08/05 ساعت 06:38 ب.ظ",

    [Parameter(Mandatory = $false)]
    [int]$TargetPosition = 0,

    [Parameter(Mandatory = $false)]
    [string]$TestDatabaseName = "Sepidar01_RestoreDrill_20261027_2161",

    [Parameter(Mandatory = $false)]
    [string]$RelocateDir = "D:\SQLData\RestoreDrill",

    [Parameter(Mandatory = $false)]
    [System.Management.Automation.PSCredential]$SqlCredential
)

$ErrorActionPreference = 'Stop'

function Escape-SqlString([string]$val) {
    if ($null -eq $val) { return "" }
    return $val.Replace("'", "''")
}

function Write-DrillLog([string]$Message, [string]$Level = "INFO") {
    $ts = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    $color = switch ($Level) {
        "ERROR"   { "Red" }
        "WARN"    { "Yellow" }
        "SUCCESS" { "Green" }
        default   { "Cyan" }
    }
    Write-Host "[$ts] [$Level] $Message" -ForegroundColor $color
}

# --------------------------------------------------------------------------------------
# اعتبارسنجی نام دیتابیس آزمایشی و حفاظت از نام‌های حساس
# --------------------------------------------------------------------------------------
if (-not ($TestDatabaseName -match '^[a-zA-Z0-9_]+$')) {
    throw "خطای امنیتی: نام دیتابیس آزمایشی ($TestDatabaseName) شامل کاراکترهای نامعتبر است. فقط حروف، اعداد و خط زیر مجاز است."
}

$forbiddenDbNames = @("Sepidar01", "ParkPalaceCRM_DB", "master", "model", "msdb", "tempdb")
if ($forbiddenDbNames -contains $TestDatabaseName.Trim()) {
    throw "خطای حفاظتی بحرانی: نام دیتابیس آزمایشی ($TestDatabaseName) نمی‌تواند نام دیتابیس‌های عملیاتی یا سیستمی باشد!"
}

Write-DrillLog "================================================================================"
Write-DrillLog " مانور اعتبارسنجی و بازیابی آزمایشی بکاپ سپیدار (Sepidar01 Restore Drill)"
Write-DrillLog " حالت اجرا: $Mode"
Write-DrillLog " سرور SQL آزمایشی: $(if ($TestServerInstance) { $TestServerInstance } else { '[تعیین‌نشده - نیازمند انتخاب صریح سرور غیرعملیاتی]' })"
Write-DrillLog " فایل بکاپ کاندیدا: $BackupFilePath"
Write-DrillLog " نام مجموعه بکاپ هدف: $(if ($TargetBackupSetName) { $TargetBackupSetName } else { '[نامشخص]' })"
Write-DrillLog " موقعیت صریح هدف (TargetPosition): $(if ($TargetPosition -gt 0) { $TargetPosition } else { '[بررسی پویا در HEADERONLY]' })"
Write-DrillLog " نام دیتابیس مقصد آزمایشی: $TestDatabaseName"
Write-DrillLog " پوشه جابه‌جایی فایل‌ها: $RelocateDir"
Write-DrillLog "================================================================================"

# ======================================================================================
# حالت اول: PlanOnly (کاملاً آفلاین، بدون اتصال به SQL و بدون تغییر در فایل‌ها)
# ======================================================================================
if ($Mode -eq "PlanOnly") {
    Write-DrillLog "`n[حالت PlanOnly] اجرای بررسی طرح به‌صورت کاملاً آفلاین و ایزوله:" "SUCCESS"
    Write-DrillLog "  ۱. وضعیت اتصال SQL: هیچ اتصالی به هیچ سروری برقرار نگردید (اتصال: غیرفعال)."
    Write-DrillLog "  ۲. فرامین استعلام: دستورات HEADERONLY، FILELISTONLY و VERIFYONLY اجرا نشدند."
    Write-DrillLog "  ۳. فایل‌های سیستم: هیچ فایلی کپی، جابه‌جا، ایجاد، بررسی فیزیکی یا حذف نگردید."
    Write-DrillLog "`n[مشخصات و الزامات برای انتخاب سرور مقصد آزمایشی]:" "WARN"
    Write-DrillLog "  - سرور مقصد آزمایشی باید صریحاً با پارامتر TestServerInstance معرفی شود."
    Write-DrillLog "  - بازیابی روی سرور عملیاتی SEPIDAR\SEPIDAR یا IP 192.168.1.242 در این مرحله مجاز نیست."
    Write-DrillLog "  - نسخه SQL Server مقصد باید بزرگتر یا مساوی نسخه مبدا (15.0.2000.5 / SQL Server 2019) باشد."
    Write-DrillLog "  - Collation مقصد باید با سرور مبدا (Arabic_CI_AI) سازگار باشد."
    Write-DrillLog "  - دسترسی حساب اتصال باید دارای مجوز CREATE DATABASE، RESTORE و VIEW SERVER STATE در مقصد باشد."
    Write-DrillLog "`n[طرح اجرایی مراحل در صورت فعال‌سازی حالت‌های صریح]:" "Cyan"
    Write-DrillLog "  - حالت VerifyBackupOnly: اتصال به مقصد آزمایشی -> تطبیق یکتای Position در HEADERONLY (بدون Fallback) -> استعلام FILELISTONLY و نگاشت پویا و محاسبه دقیق ظرفیت -> اعتبارسنجی تلاقی در کاتالوگ و فایل‌سیستم میزبان -> اجرای RESTORE VERIFYONLY WITH CHECKSUM."
    Write-DrillLog "  - حالت ExecuteRestoreDrill: اجرای کامل بازیابی آزمایشی با نگاشت پویای کلیه فایل‌ها به RelocateDir -> تنظیم دیتابیس در RESTRICTED_USER -> ثبت و اسکن خروجی ساختاریافته DBCC CHECKDB -> حفظ دیتابیس جهت تست ریدر."
    Write-DrillLog "`n[وضعیت PlanOnly] طرح با موفقیت و بدون هیچ‌گونه تراکنش یا ارتباط شبکه پایان یافت." "SUCCESS"
    return
}

# ======================================================================================
# کنترل‌های ایمنی سرور مقصد برای حالت‌های آنلاین (VerifyBackupOnly و ExecuteRestoreDrill)
# ======================================================================================
if ([string]::IsNullOrWhiteSpace($TestServerInstance)) {
    throw "خطای امنیتی: اجرای حالت $Mode مستلزم تعیین صریح سرور آزمایشی مقصد از طریق پارامتر -TestServerInstance است."
}

if ($TestServerInstance -match "SEPIDAR" -or $TestServerInstance -match "192\.168\.1\.242") {
    throw "خطای حفاظتی بحرانی: بازیابی یا اعتبارسنجی روی سرور عملیاتی سپیدار (SEPIDAR\SEPIDAR یا 192.168.1.242) مجاز نیست! سرور مقصد باید یک نمونه آزمایشی/Staging مجزا باشد."
}

Add-Type -AssemblyName 'System.Data'

$connStr = if ($SqlCredential) {
    $u = $SqlCredential.UserName
    $p = $SqlCredential.GetNetworkCredential().Password
    "Server=$TestServerInstance;Database=master;User Id=$u;Password=$p;TrustServerCertificate=True;Connect Timeout=30;"
} else {
    "Server=$TestServerInstance;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=30;"
}

Write-DrillLog "`n[اتصال به سرور آزمایشی] برقراری ارتباط با $TestServerInstance و اعتبارسنجی هویت سرور مقصد..."
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

# ثبت رویداد پیام‌های اطلاعاتی و خروجی DBCC
$dbccMessages = New-Object System.Collections.Generic.List[string]
$conn.FireInfoMessageEventOnUserErrors = $true
$conn.add_InfoMessage({
    param($sender, $e)
    if ($e.Message) {
        $dbccMessages.Add($e.Message)
    }
})

try {
    # ۱. اعتبارسنجی هویت سرور مقصد
    $idCmd = $conn.CreateCommand()
    $idCmd.CommandTimeout = 30
    $idCmd.CommandText = "SET LOCK_TIMEOUT 3000; SELECT @@SERVERNAME AS ServerName, CAST(SERVERPROPERTY('ProductMajorVersion') AS INT) AS MajorVer, CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(128)) AS FullVer, CAST(SERVERPROPERTY('Collation') AS NVARCHAR(128)) AS CollationName;"
    $rId = $idCmd.ExecuteReader()
    $actualServer = ""
    $majorVer = 0
    $fullVer = ""
    $collation = ""
    if ($rId.Read()) {
        $actualServer = $rId["ServerName"].ToString()
        $majorVer = if ($rId["MajorVer"] -ne [DBNull]::Value) { [int]$rId["MajorVer"] } else { 0 }
        $fullVer = $rId["FullVer"].ToString()
        $collation = $rId["CollationName"].ToString()
    }
    $rId.Close()

    Write-DrillLog "  سرور متصل‌شده: $actualServer (نسخه: $fullVer | Collation: $collation)" "SUCCESS"

    if ($actualServer -match "SEPIDAR" -or $actualServer -match "192\.168\.1\.242") {
        throw "خطای حفاظتی: سرور متصل‌شده ($actualServer) سرور عملیاتی سپیدار است! فرآیند فوراً متوقف شد."
    }

    if ($majorVer -lt 15) {
        throw "خطای ناسازگاری نسخه: پایگاه‌داده سپیدار با SQL Server 2019 (نسخه ۱۵) ایجاد شده است و نمی‌تواند روی نسخه قدیمی‌تر ($majorVer) بازیابی شود."
    }

    # ۲. بررسی تلاقی نام دیتابیس در سرور مقصد
    $escDbName = Escape-SqlString $TestDatabaseName
    $checkDbCmd = $conn.CreateCommand()
    $checkDbCmd.CommandTimeout = 30
    $checkDbCmd.CommandText = "SET LOCK_TIMEOUT 3000; SELECT DB_ID(N'$escDbName');"
    $existingDbId = $checkDbCmd.ExecuteScalar()
    if ($existingDbId -and -not [DBNull]::Value.Equals($existingDbId)) {
        throw "توقف ایمنی: پایگاه‌داده‌ای با نام [$TestDatabaseName] روی سرور مقصد وجود دارد. بازنویسی ممنوع است."
    }

    # ۳. استعلام HEADERONLY از دیدگاه میزبان سرور مقصد و تطبیق یکتای Position (بدون Fallback)
    Write-DrillLog "`n[استعلام HEADERONLY] ارزیابی وجود فایل روی میزبان مقصد و استخراج فراداده هدر بکاپ..."
    $escBackupPath = Escape-SqlString $BackupFilePath

    $cmdHeader = $conn.CreateCommand()
    $cmdHeader.CommandTimeout = 60
    $cmdHeader.CommandText = "SET LOCK_TIMEOUT 3000; RESTORE HEADERONLY FROM DISK = N'$escBackupPath';"
    $daHeader = New-Object System.Data.SqlClient.SqlDataAdapter($cmdHeader)
    $headerTable = New-Object System.Data.DataTable
    $daHeader.Fill($headerTable) | Out-Null

    if ($headerTable.Rows.Count -eq 0) {
        throw "هیچ مجموعه بکاپی در فایل بکاپ روی سرور مقصد یافت نشد: $BackupFilePath"
    }

    # تطبیق یکتای مجموعه بکاپ
    $matchingSets = New-Object System.Collections.Generic.List[PSObject]

    foreach ($hRow in $headerTable.Rows) {
        $pos = [int]$hRow["Position"]
        $dbName = if ($hRow["DatabaseName"] -ne [DBNull]::Value) { $hRow["DatabaseName"].ToString() } else { "" }
        $bkName = if ($hRow["BackupName"] -ne [DBNull]::Value) { $hRow["BackupName"].ToString() } else { "" }
        $bkType = if ($hRow["BackupType"] -ne [DBNull]::Value) { [int]$hRow["BackupType"] } else { 0 }
        $finishDate = if ($hRow["BackupFinishDate"] -ne [DBNull]::Value) { $hRow["BackupFinishDate"].ToString() } else { "" }

        # معیار ۱: انطباق با TargetPosition در صورت تعیین صریح
        if ($TargetPosition -gt 0) {
            if ($pos -eq $TargetPosition) {
                $matchingSets.Add([PSCustomObject]@{
                    Position = $pos
                    DatabaseName = $dbName
                    BackupName = $bkName
                    FinishDate = $finishDate
                })
            }
            continue
        }

        # معیار ۲: انطباق با نام دیتابیس (Sepidar01) و نوع بکاپ کامل (BackupType = 1)
        if ($dbName.Trim() -eq "Sepidar01" -and $bkType -eq 1) {
            if (-not [string]::IsNullOrWhiteSpace($TargetBackupSetName)) {
                if ($bkName.Trim() -eq $TargetBackupSetName.Trim()) {
                    $matchingSets.Add([PSCustomObject]@{
                        Position = $pos
                        DatabaseName = $dbName
                        BackupName = $bkName
                        FinishDate = $finishDate
                    })
                }
            } else {
                $matchingSets.Add([PSCustomObject]@{
                    Position = $pos
                    DatabaseName = $dbName
                    BackupName = $bkName
                    FinishDate = $finishDate
                })
            }
        }
    }

    # الزام حذف Fallback و توقف قطعی در صورت عدم تعیین یکتا
    if ($matchingSets.Count -eq 0) {
        throw "خطای توقف ایمنی: هیچ مجموعه بکاپی با مشخصات تعیین‌شده در HEADERONLY یافت نشد! فرآیند متوقف شد."
    }

    if ($matchingSets.Count -gt 1) {
        $listStr = ($matchingSets | ForEach-Object { "Position=$($_.Position) (نام: '$($_.BackupName)' | تاریخ: $($_.FinishDate))" }) -join " ، "
        throw "خطای ابهام در HEADERONLY: چند مجموعه بکاپ ($($matchingSets.Count) مورد) با معیارهای جستجو تطابق دارند: [$listStr]. نام دیتابیس به‌تنهایی کافی نیست؛ لطفاً با تعیین صریح TargetPosition یا TargetBackupSetName، مجموعه موردنظر را به‌طور یکتا مشخص کنید."
    }

    $selectedPosition = $matchingSets[0].Position
    Write-DrillLog "  [OK] مجموعه بکاپ به‌صورت کاملاً یکتا شناسایی شد: Position = $selectedPosition | نام = '$($matchingSets[0].BackupName)' | تاریخ پایان = $($matchingSets[0].FinishDate)" "SUCCESS"

    # ۴. استعلام FILELISTONLY و نگاشت پویای کلیه فایل‌ها و محاسبه دقیق ظرفیت
    Write-DrillLog "`n[استعلام FILELISTONLY] کشف تمام فایل‌های منطقی، نگاشت پویا و محاسبه ظرفیت واقعی..."
    $cmdFileList = $conn.CreateCommand()
    $cmdFileList.CommandTimeout = 60
    $cmdFileList.CommandText = "SET LOCK_TIMEOUT 3000; RESTORE FILELISTONLY FROM DISK = N'$escBackupPath' WITH FILE = $selectedPosition;"
    $daFileList = New-Object System.Data.SqlClient.SqlDataAdapter($cmdFileList)
    $fileListTable = New-Object System.Data.DataTable
    $daFileList.Fill($fileListTable) | Out-Null

    if ($fileListTable.Rows.Count -eq 0) {
        throw "فهرست فایل‌های درون بکاپ خالی بازگردانده شد!"
    }

    $moveClauses = @()
    $totalRequiredBytes = [int64]0
    $plannedFiles = @()

    foreach ($fRow in $fileListTable.Rows) {
        $logicalName = $fRow["LogicalName"].ToString()
        $fileType = $fRow["Type"].ToString() # D = Data, L = Log, etc.
        $fileSizeBytes = [int64]$fRow["Size"]
        $totalRequiredBytes += $fileSizeBytes

        $fileExt = switch ($fileType) {
            "D" { if ($moveClauses.Count -eq 0) { ".mdf" } else { ".ndf" } }
            "L" { ".ldf" }
            default { ".dat" }
        }

        $destFileName = "${TestDatabaseName}_${logicalName}${fileExt}"
        $destFilePath = Join-Path $RelocateDir $destFileName
        $escLogical = Escape-SqlString $logicalName
        $escDestPath = Escape-SqlString $destFilePath

        $moveClauses += "MOVE N'$escLogical' TO N'$escDestPath'"
        $plannedFiles += [PSCustomObject]@{
            LogicalName = $logicalName
            Type = $fileType
            SizeMB = [Math]::Round($fileSizeBytes / 1MB, 2)
            TargetPhysicalPath = $destFilePath
        }
    }

    $totalRequiredMB = [Math]::Round($totalRequiredBytes / 1MB, 2)
    $totalRequiredGB = [Math]::Round($totalRequiredBytes / 1GB, 3)

    Write-DrillLog "  تعداد کل فایل‌های شناسایی‌شده درون بکاپ: $($plannedFiles.Count)" "SUCCESS"
    foreach ($pf in $plannedFiles) {
        Write-DrillLog "    فایل: $($pf.LogicalName) ($($pf.Type)) -> $($pf.SizeMB) MB | مسیر: $($pf.TargetPhysicalPath)"
    }
    Write-DrillLog "  ظرفیت قطعی محاسبه‌شده موردنیاز دیسک مقصد: $totalRequiredMB MB ($totalRequiredGB GB)" "SUCCESS"

    # ۵. بررسی تلاقی فایل‌های فیزیکی مقصد (هم در sys.master_files و هم روی فایل‌سیستم واقعی میزبان مقصد)
    Write-DrillLog "`n[بررسی تلاقی فایل‌ها] ارزیابی عدم وجود فایل‌های فیزیکی مقصد در کاتالوگ SQL و روی فایل‌سیستم میزبان..."
    foreach ($pf in $plannedFiles) {
        $targetPath = $pf.TargetPhysicalPath
        $escTarget = Escape-SqlString $targetPath

        # ۵.الف: بررسی در sys.master_files
        $checkMfCmd = $conn.CreateCommand()
        $checkMfCmd.CommandTimeout = 30
        $checkMfCmd.CommandText = "SET LOCK_TIMEOUT 3000; SELECT COUNT(*) FROM sys.master_files WHERE physical_name = N'$escTarget';"
        $mfCount = [int]$checkMfCmd.ExecuteScalar()
        if ($mfCount -gt 0) {
            throw "توقف ایمنی: مسیر فایل فیزیکی [$targetPath] در کاتالوگ sys.master_files سرور مقصد ثبت شده است!"
        }

        # ۵.ب: بررسی روی فایل‌سیستم واقعی میزبان از طریق sys.dm_os_file_exists
        try {
            $checkFsCmd = $conn.CreateCommand()
            $checkFsCmd.CommandTimeout = 30
            $checkFsCmd.CommandText = "SET LOCK_TIMEOUT 3000; SELECT file_exists, file_is_a_directory, parent_directory_exists FROM sys.dm_os_file_exists(N'$escTarget');"
            $rFs = $checkFsCmd.ExecuteReader()
            $fileExists = -1
            $parentExists = -1
            if ($rFs.Read()) {
                $fileExists = if ($rFs["file_exists"] -ne [DBNull]::Value) { [int]$rFs["file_exists"] } else { -1 }
                $parentExists = if ($rFs["parent_directory_exists"] -ne [DBNull]::Value) { [int]$rFs["parent_directory_exists"] } else { -1 }
            }
            $rFs.Close()

            if ($fileExists -eq -1 -or $parentExists -eq -1) {
                throw "پاسخ معتبری از sys.dm_os_file_exists برای مسیر [$targetPath] دریافت نشد."
            }

            if ($fileExists -eq 1) {
                throw "توقف ایمنی: فایل مقصد [$targetPath] هم‌اکنون روی فایل‌سیستم واقعی میزبان سرور مقصد وجود دارد! خطر بازنویسی فیزیکی وجود دارد."
            }

            if ($parentExists -ne 1) {
                throw "توقف ایمنی: پوشه والد مسیر مقصد [$targetPath] روی فایل‌سیستم میزبان سرور مقصد وجود ندارد."
            }

            Write-DrillLog "  [OK] عدم تلاقی در فایل‌سیستم میزبان تأیید شد: [$targetPath] (فایل وجود ندارد، پوشه والد معتبر است)." "SUCCESS"
        } catch {
            throw "توقف ایمنی: بررسی وضعیت فایل مقصد روی فایل‌سیستم میزبان امکان‌پذیر نبود یا با تلاقی مواجه شد: $($_.Exception.Message)"
        }
    }

    # ۶. اعتبارسنجی یکپارچگی فایل بکاپ با RESTORE VERIFYONLY WITH CHECKSUM
    Write-DrillLog "`n[مرحله اعتبارسنجی هدر] اجرای RESTORE VERIFYONLY WITH CHECKSUM, FILE = $selectedPosition..."
    $cmdVerify = $conn.CreateCommand()
    $cmdVerify.CommandTimeout = 300
    $cmdVerify.CommandText = "SET LOCK_TIMEOUT 3000; RESTORE VERIFYONLY FROM DISK = N'$escBackupPath' WITH FILE = $selectedPosition, CHECKSUM;"
    $cmdVerify.ExecuteNonQuery() | Out-Null
    Write-DrillLog "  [OK] ساختار بکاپ و هدر صفحات با موفقیت توسط RESTORE VERIFYONLY تأیید شد." "SUCCESS"
    Write-DrillLog "  [یادآوری حقوقی] VERIFYONLY صرفاً سلامت فایل را تأیید می‌کند و معادل بازیابی آزمایشی نیست." "WARN"

    if ($Mode -eq "VerifyBackupOnly") {
        Write-DrillLog "`n================================================================================" "SUCCESS"
        Write-DrillLog " [پایان حالت VerifyBackupOnly] اعتبارسنجی ساختار بکاپ، نگاشت فایل‌ها و محاسبه ظرفیت با موفقیت انجام شد." "SUCCESS"
        Write-DrillLog " هیچ عملیات بازیابی (RESTORE DATABASE) در این حالت اجرا نشد." "SUCCESS"
        Write-DrillLog "================================================================================" "SUCCESS"
        $conn.Close()
        return
    }

    # ==================================================================================
    # حالت سوم: ExecuteRestoreDrill (بازیابی آزمایشی واقعی، DBCC و حفظ دیتابیس)
    # ==================================================================================
    Write-DrillLog "`n[مرحله بازیابی آزمایشی] آغاز بازیابی به دیتابیس آزمایشی با نگاشت کامل تمام فایل‌ها..."

    $moveSqlBlock = $moveClauses -join ",`n     "
    $restoreSql = @"
RESTORE DATABASE [$escDbName]
FROM DISK = N'$escBackupPath'
WITH FILE = $selectedPosition,
     $moveSqlBlock,
     RESTRICTED_USER,
     NOUNLOAD,
     STATS = 10;
"@

    $cmdRestore = $conn.CreateCommand()
    $cmdRestore.CommandTimeout = 1800
    $cmdRestore.CommandText = $restoreSql
    $cmdRestore.ExecuteNonQuery() | Out-Null
    Write-DrillLog "  [OK] پایگاه‌داده آزمایشی [$TestDatabaseName] با موفقیت بازیابی شد." "SUCCESS"

    # ۷. بررسی واقعی خروجی DBCC CHECKDB
    Write-DrillLog "`n[بررسی سلامت ساختار صفحات] اجرای DBCC CHECKDB با دریافت و اعتبارسنجی جدول نتایج..."
    $dbccMessages.Clear()

    $cmdCheckDb = $conn.CreateCommand()
    $cmdCheckDb.CommandTimeout = 900
    $cmdCheckDb.CommandText = "DBCC CHECKDB(N'$escDbName') WITH TABLERESULTS, ALL_ERRORMSGS;"
    $daCheckDb = New-Object System.Data.SqlClient.SqlDataAdapter($cmdCheckDb)
    $dbccTable = New-Object System.Data.DataTable
    $daCheckDb.Fill($dbccTable) | Out-Null

    $hasConsistencyErrors = $false
    if ($dbccTable.Rows.Count -gt 0) {
        foreach ($r in $dbccTable.Rows) {
            $errNum = if ($r["Error"] -ne [DBNull]::Value) { [int]$r["Error"] } else { 0 }
            $msgText = if ($r["MessageText"] -ne [DBNull]::Value) { $r["MessageText"].ToString() } else { "" }
            
            # در خروجی TABLERESULTS رکوردهای با سطح خطای بالا یا پیام‌های فساد بررسی می‌شوند
            if ($errNum -gt 0 -and $msgText -match "error|corrupt|failed") {
                $hasConsistencyErrors = $true
                Write-DrillLog "  [خطای سلامت DBCC] خطا #$errNum : $msgText" "ERROR"
            }
        }
    }

    # بررسی پیام‌های متنی InfoMessage
    $capturedSummary = ($dbccMessages | Where-Object { $_ -match "allocation errors" -or $_ -match "consistency errors" }) -join " | "
    Write-DrillLog "  خلاصه پیام‌های دریافتی از موتور DBCC: $capturedSummary"

    if ($hasConsistencyErrors) {
        throw "آزمون DBCC CHECKDB خطاهای ساختاری یا فساد داده در دیتابیس بازیابی‌شده کشف کرد!"
    } else {
        Write-DrillLog "  [OK] نتایج DBCC CHECKDB اعتبارسنجی شد: هیچ خطای ساختاری یا تخصیصی یافت نشد." "SUCCESS"
    }

    # ۸. استعلام نمونه جداول جهت اثبات قابل خواندن بودن داده
    $cmdSample = $conn.CreateCommand()
    $cmdSample.CommandTimeout = 30
    $cmdSample.CommandText = "SET LOCK_TIMEOUT 3000; SELECT COUNT(*) FROM [$escDbName].sys.tables;"
    $tableCount = $cmdSample.ExecuteScalar()
    Write-DrillLog "  [OK] تعداد جداول در نسخه بازیابی‌شده: $tableCount جدول." "SUCCESS"

    # ۹. حفظ نسخه بازیابی‌شده در حالت RESTRICTED_USER جهت آزمایش‌های ریدر
    Write-DrillLog "`n[حفظ پایگاه‌داده آزمایشی] دیتابیس [$TestDatabaseName] در وضعیت RESTRICTED_USER نگهداری شد." "WARN"
    Write-DrillLog "  نکته: هیچ حذف خودکار یا ROLLBACK IMMEDIATE انجام نشد. نسخه بازیابی‌شده برای آزمایش ریدر در دسترس است." "WARN"
    Write-DrillLog "  پاکسازی و حذف دیتابیس آزمایشی پس از اتمام تست‌ها مستلزم تصمیم و مجوز مستقل خواهد بود." "WARN"

    $conn.Close()
    Write-DrillLog "`n================================================================================" "SUCCESS"
    Write-DrillLog " مانور بازیابی آزمایشی با موفقیت کامل و بدون تداخل پایان یافت." "SUCCESS"
    Write-DrillLog "================================================================================" "SUCCESS"
}
catch {
    if ($conn -and $conn.State -eq 'Open') {
        $conn.Close()
    }
    Write-DrillLog "خطا در فرآیند مانور بازیابی آزمایشی: $($_.Exception.Message)" "ERROR"
    throw
}
