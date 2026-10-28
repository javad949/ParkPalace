<#
.SYNOPSIS
    Backup-ParkPalaceDatabase.ps1 - Automated SQL Server backup with off-VM transfer verification
    and chain-aware retention for ParkPalace CRM.
.DESCRIPTION
    Supports Full, Differential, and Transaction Log backups.
    Enforces strict single-instance concurrency locking.
    Validates backup integrity via RESTORE VERIFYONLY and SHA-256 hash matching.
    Strictly forbids administrative share fallbacks (D$).
    Enforces chain-aware retention protecting the last known good backup chain.
#>
[CmdletBinding()]
param (
    [Parameter()]
    [string]$ServerInstance = "localhost",

    [Parameter()]
    [string]$DatabaseName = "ParkPalaceCRM",

    [Parameter()]
    [ValidateSet("Full", "Diff", "Log")]
    [string]$BackupType = "Full",

    [Parameter()]
    [string]$LocalBackupDir = "D:\ParkPlaceCRM\backups\sql",

    [Parameter()]
    [string]$OffVmDestinationDir = "\\pdc.sahar.loc\CRMBACKUP",

    [Parameter()]
    [int]$RetentionDays = 7,

    [Parameter()]
    [int]$MinFreeSpaceMB = 500,

    [Parameter()]
    [switch]$VerifyOnly = $false
)

$ErrorActionPreference = "Stop"

function Write-Log([string]$Message, [string]$Level = "INFO") {
    $ts = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $color = switch ($Level) {
        "ERROR"   { "Red" }
        "WARN"    { "Yellow" }
        "SUCCESS" { "Green" }
        default   { "Cyan" }
    }
    Write-Host "[$ts] [$Level] $Message" -ForegroundColor $color
}

# 1. Single-Instance Mutex Lock (Prevent concurrent overlapping executions)
$mutexName = "Global\ParkPalace_Backup_$($DatabaseName)_Mutex"
$mutexCreated = $false
$mutex = $null

try {
    $mutex = New-Object System.Threading.Mutex($true, $mutexName, [ref]$mutexCreated)
    if (-not $mutexCreated) {
        Write-Log "Another backup instance is currently executing for '$DatabaseName'. Aborting to avoid concurrency conflict." "WARN"
        exit 0
    }

    Write-Log "================================================================================"
    Write-Log " STARTING $($BackupType.ToUpper()) BACKUP FOR DATABASE '$DatabaseName'"
    Write-Log " Server: $ServerInstance | Target: $OffVmDestinationDir | Retention: $RetentionDays days"
    Write-Log "================================================================================"

    # 2. Strict Destination Validation (NO D$ FALLBACK ALLOWED)
    if (-not [string]::IsNullOrWhiteSpace($OffVmDestinationDir)) {
        if ($OffVmDestinationDir -match '\$[\\/]?$') {
            throw "Security policy violation: Administrative shares (e.g. D$) are strictly prohibited. Destination must be a designated share."
        }

        if (-not (Test-Path -Path $OffVmDestinationDir)) {
            Write-Log "Off-VM destination share '$OffVmDestinationDir' is not accessible. Halting execution without falling back to administrative credentials." "ERROR"
            throw "Destination share '$OffVmDestinationDir' is not reachable. Ensure share exists and SAHAR\CRM$ has permissions."
        }
    }

    # 3. Ensure local directory exists
    if (-not (Test-Path -Path $LocalBackupDir)) {
        New-Item -ItemType Directory -Path $LocalBackupDir -Force | Out-Null
        Write-Log "Created local backup directory: $LocalBackupDir"
    }

    # 4. Check Free Disk Space (Source and Destination)
    $localDrive = (Get-Item $LocalBackupDir).PSDrive
    if ($localDrive -and $localDrive.Free) {
        $localFreeMB = [math]::Round($localDrive.Free / 1MB, 2)
        if ($localFreeMB -lt $MinFreeSpaceMB) {
            throw "Insufficient disk space on local drive '$($localDrive.Name)'. Free: ${localFreeMB}MB, Required: ${MinFreeSpaceMB}MB."
        }
        Write-Log "Local disk free space: ${localFreeMB}MB (Healthy)."
    }

    # 5. Generate collision-proof unique filename
    $tsFile = (Get-Date).ToString("yyyyMMdd_HHmmss")
    $entropy = [Guid]::NewGuid().ToString("N").Substring(0, 6)
    $ext = switch ($BackupType) {
        "Log"   { "trn" }
        "Diff"  { "diff" }
        default { "bak" }
    }
    $backupFileName = "$($DatabaseName)_$($BackupType)_$($tsFile)_$($entropy).$ext"
    $localBackupFilePath = Join-Path -Path $LocalBackupDir -ChildPath $backupFileName

    Write-Log "Generated target backup file: $backupFileName"

    # 6. Build T-SQL Command with Native Compression & Checksum
    $sqlBackupCommand = switch ($BackupType) {
        "Log" {
            @"
BACKUP LOG [$DatabaseName]
TO DISK = N'$localBackupFilePath'
WITH CHECKSUM, COMPRESSION, FORMAT, INIT, STATS = 10;
"@
        }
        "Diff" {
            @"
BACKUP DATABASE [$DatabaseName]
TO DISK = N'$localBackupFilePath'
WITH DIFFERENTIAL, CHECKSUM, COMPRESSION, FORMAT, INIT, STATS = 10;
"@
        }
        default {
            @"
BACKUP DATABASE [$DatabaseName]
TO DISK = N'$localBackupFilePath'
WITH CHECKSUM, COMPRESSION, FORMAT, INIT, STATS = 10;
"@
        }
    }

    # 7. Execute Backup via Windows Authentication (Least Privilege: db_backupoperator)
    Write-Log "Executing T-SQL backup command..."
    Add-Type -AssemblyName 'System.Data'
    $connString = "Server=$ServerInstance;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=30"
    $conn = New-Object System.Data.SqlClient.SqlConnection($connString)
    $conn.Open()

    $cmd = $conn.CreateCommand()
    $cmd.CommandTimeout = 600
    $cmd.CommandText = $sqlBackupCommand
    $cmd.ExecuteNonQuery() | Out-Null
    $conn.Close()
    Write-Log "T-SQL backup executed successfully." "SUCCESS"

    # 8. Verify Media Integrity via RESTORE VERIFYONLY
    Write-Log "Validating backup media with RESTORE VERIFYONLY..."
    $conn.Open()
    $cmdVerify = $conn.CreateCommand()
    $cmdVerify.CommandTimeout = 600
    $cmdVerify.CommandText = "RESTORE VERIFYONLY FROM DISK = N'$localBackupFilePath' WITH CHECKSUM;"
    $cmdVerify.ExecuteNonQuery() | Out-Null
    $conn.Close()
    Write-Log "RESTORE VERIFYONLY passed with CHECKSUM." "SUCCESS"

    if ($VerifyOnly) {
        Write-Log "VerifyOnly flag set; skipping off-VM transfer." "SUCCESS"
        return
    }

    # 9. Off-VM Transfer via Robocopy (Reliable restartable transfer)
    if (-not [string]::IsNullOrWhiteSpace($OffVmDestinationDir)) {
        Write-Log "Transferring backup file to off-VM destination: $OffVmDestinationDir ..."

        $robocopyArgs = @(
            $LocalBackupDir,
            $OffVmDestinationDir,
            $backupFileName,
            "/Z",     # Restartable mode
            "/R:3",   # 3 Retries on failure
            "/W:5",   # 5 Seconds wait between retries
            "/NP",    # No progress bar clutter
            "/NFL",   # No file list
            "/NDL"    # No directory list
        )

        $proc = Start-Process -FilePath "robocopy.exe" -ArgumentList $robocopyArgs -Wait -NoNewWindow -PassThru
        $exitCode = $proc.ExitCode

        # Robocopy exit code < 8 indicates success
        if ($exitCode -ge 8) {
            throw "Robocopy off-VM transfer failed with fatal exit code $exitCode."
        }
        Write-Log "Robocopy transfer completed successfully (ExitCode: $exitCode)." "SUCCESS"

        # 10. End-to-End Integrity Verification via SHA-256
        $destBackupFilePath = Join-Path -Path $OffVmDestinationDir -ChildPath $backupFileName
        Write-Log "Calculating and verifying SHA-256 checksums..."
        
        $srcHash = (Get-FileHash -Path $localBackupFilePath -Algorithm SHA256).Hash
        $dstHash = (Get-FileHash -Path $destBackupFilePath -Algorithm SHA256).Hash

        if ($srcHash -ne $dstHash) {
            Remove-Item -Path $destBackupFilePath -Force -ErrorAction SilentlyContinue
            throw "Integrity error: SHA-256 checksum mismatch between source and destination! Source: $srcHash, Dest: $dstHash."
        }
        Write-Log "SHA-256 integrity match confirmed: $srcHash" "SUCCESS"

        # 11. Chain-Aware Retention Policy (Protecting Full 7-Day Point-in-Time Recovery Window)
        Write-Log "Evaluating chain-aware retention policy (Window: $RetentionDays days)..."
        
        $allDestBackups = @(Get-ChildItem -Path $OffVmDestinationDir -Filter "$($DatabaseName)_*.*" | 
            Where-Object { $_.Extension -in ".bak", ".diff", ".trn" } |
            Sort-Object LastWriteTime -Descending)

        # Separate Full backups sorted chronologically ascending
        $fullBackupsChronological = @($allDestBackups | Where-Object { $_.Name -match "_Full_" -or $_.Extension -eq ".bak" } | Sort-Object LastWriteTime)

        if ($fullBackupsChronological.Count -le 1) {
            Write-Log "Retention protection: Only 1 Full backup chain exists in destination; skipping purge to guarantee disaster recoverability." "WARN"
        }
        else {
            $cutoffDate = (Get-Date).AddDays(-$RetentionDays)
            
            # To recover to any point in [$cutoffDate, Now], we need the last Full backup taken on or before $cutoffDate.
            # This Full backup is the "Retention Anchor Full". It and all subsequent logs/diffs are critical.
            $anchorFull = $fullBackupsChronological | Where-Object { $_.LastWriteTime -le $cutoffDate } | Select-Object -Last 1

            if (-not $anchorFull) {
                # All Full backups are newer than cutoffDate; the oldest Full backup is the anchor.
                $anchorFull = $fullBackupsChronological[0]
            }

            Write-Log "Retention Anchor Full identified: $($anchorFull.Name) (Timestamp: $($anchorFull.LastWriteTime)). All backups from this anchor forward are preserved for 7-day SLA." "INFO"

            # 1. Purge Full backups strictly older than the anchor Full
            $expiredFulls = @($fullBackupsChronological | Where-Object { $_.LastWriteTime -lt $anchorFull.LastWriteTime })
            foreach ($oldFull in $expiredFulls) {
                Write-Log "Purging expired full backup: $($oldFull.Name) (Date: $($oldFull.LastWriteTime))." "INFO"
                Remove-Item -Path $oldFull.FullName -Force -ErrorAction SilentlyContinue
            }

            # 2. Purge Differential and Transaction Log files strictly older than the anchor Full
            # (Because any point before the anchor Full is older than the 7-day SLA and its base Full is gone).
            $expiredSubBackups = @($allDestBackups | Where-Object { 
                $_.Extension -in ".diff", ".trn" -and $_.LastWriteTime -lt $anchorFull.LastWriteTime 
            })
            foreach ($sub in $expiredSubBackups) {
                Write-Log "Purged expired chain element: $($sub.Name) (Date: $($sub.LastWriteTime))." "INFO"
                Remove-Item -Path $sub.FullName -Force -ErrorAction SilentlyContinue
            }
        }
    }

    Write-Log "Backup and transfer procedure completed with full integrity verification." "SUCCESS"
}
finally {
    if ($mutex -and $mutexCreated) {
        $mutex.ReleaseMutex()
        $mutex.Dispose()
    }
}
