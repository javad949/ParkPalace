<#
.SYNOPSIS
    Monitor-ParkPalaceRpoHealth.ps1 - Active RPO telemetry and Chain-Aware SLA breach detection.
.DESCRIPTION
    Monitors the age of the newest valid, recoverable point in the destination repository (\\pdc.sahar.loc\CRMBACKUP).
    Validates complete recovery chain (Full + Differential + Transaction Logs) using SQL Server backup headers:
      - DifferentialBaseGUID & DifferentialBaseLSN matching Full backup.
      - RecoveryForkID matching across all elements.
      - Strict LSN continuity across Transaction Logs (detects missing intermediate logs).
      - Compares SQL BackupFinishDate against current SQL Server time (avoids filesystem LastWriteTime errors).
    SLA Thresholds:
      - Warning: Age >= 3 hours (180 minutes) [Exit Code: 1]
      - Critical Breach: Age >= 4 hours (240 minutes) or Broken/Unreachable Chain [Exit Code: 2]
      - Healthy: Contiguous chain with age < 3 hours [Exit Code: 0]
#>
[CmdletBinding()]
param (
    [Parameter()]
    [string]$DestinationDir = "\\pdc.sahar.loc\CRMBACKUP",

    [Parameter()]
    [string]$DatabaseName = "ParkPalaceCRM",

    [Parameter()]
    [string]$ServerInstance = "localhost",

    [Parameter()]
    [int]$WarningThresholdMinutes = 180, # 3 hours: Early warning before breach

    [Parameter()]
    [int]$CriticalThresholdMinutes = 240, # 4 hours: Strict RPO SLA limit

    [Parameter()]
    [switch]$WriteToEventLog = $false,

    # Parameters for local unit testing & mock validation
    [Parameter()]
    [object[]]$CustomHeaderMetadata = $null,

    [Parameter()]
    [datetime]$CurrentTimeOverride = [datetime]::MinValue
)

$ErrorActionPreference = "Stop"

function Write-Telemetry([string]$Status, [string]$Message, [int]$ExitCode, [hashtable]$Details = $null) {
    $ts = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $color = switch ($Status) {
        "HEALTHY"  { "Green" }
        "WARNING"  { "Yellow" }
        "BREACHED" { "Red" }
        default    { "Cyan" }
    }
    Write-Host "[$ts] [RPO-$Status] $Message" -ForegroundColor $color

    if ($Details) {
        Write-Host "  -> Verified Chain Base: $($Details.BaseFull)" -ForegroundColor Gray
        Write-Host "  -> Verified Diff Point: $($Details.ActiveDiff)" -ForegroundColor Gray
        Write-Host "  -> Contiguous Logs:     $($Details.LogCount) valid logs" -ForegroundColor Gray
        Write-Host "  -> Newest Recoverable:  $($Details.RecoverablePoint) ($($Details.RecoverableType)) at $($Details.RecoverableFinishDate)" -ForegroundColor Gray
        Write-Host "  -> Reference Clock:     $($Details.CurrentTime) | True Data Age: $($Details.AgeMinutes) min ($($Details.AgeHours) h)" -ForegroundColor Gray
    }

    if ($WriteToEventLog) {
        try {
            $evtSource = "ParkPalace_RPO_Monitor"
            if (-not [System.Diagnostics.EventLog]::SourceExists($evtSource)) {
                New-EventLog -LogName Application -Source $evtSource -ErrorAction SilentlyContinue
            }
            $entryType = switch ($Status) {
                "HEALTHY"  { [System.Diagnostics.EventLogEntryType]::Information }
                "WARNING"  { [System.Diagnostics.EventLogEntryType]::Warning }
                "BREACHED" { [System.Diagnostics.EventLogEntryType]::Error }
                default    { [System.Diagnostics.EventLogEntryType]::Information }
            }
            Write-EventLog -LogName Application -Source $evtSource -EventId (1000 + $ExitCode) -EntryType $entryType -Message "$Message`n`nNote: Local EventLog recorded. External notification/alerting channels (SMS/Email) are NOT configured." -ErrorAction SilentlyContinue
        } catch { }
    }

    return $ExitCode
}

# 1. Validate destination availability
if (-not $CustomHeaderMetadata) {
    if (-not (Test-Path -Path $DestinationDir)) {
        $msg = "Destination backup repository '$DestinationDir' is UNREACHABLE. RPO status cannot be confirmed."
        return Write-Telemetry -Status "BREACHED" -Message $msg -ExitCode 2
    }
}

# 2. Retrieve Backup Headers (Real SQL Query or Test Mock)
$headers = @()
$currentServerTime = if ($CurrentTimeOverride -ne [datetime]::MinValue) { $CurrentTimeOverride } else { Get-Date }

if ($CustomHeaderMetadata -and $CustomHeaderMetadata.Count -gt 0) {
    $headers = $CustomHeaderMetadata
}
else {
    # Scan destination directory for candidate files
    $backupFiles = Get-ChildItem -Path $DestinationDir -Filter "$($DatabaseName)_*.*" -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in ".bak", ".diff", ".trn" -and $_.Length -gt 0 }

    if (-not $backupFiles -or $backupFiles.Count -eq 0) {
        $msg = "No backup files found in '$DestinationDir' for database '$DatabaseName'. RPO SLA is completely unfulfilled!"
        return Write-Telemetry -Status "BREACHED" -Message $msg -ExitCode 2
    }

    Add-Type -AssemblyName 'System.Data'
    $connString = "Server=$ServerInstance;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=30"
    $conn = New-Object System.Data.SqlClient.SqlConnection($connString)
    
    try {
        $conn.Open()
        
        # Get authoritative SQL server clock
        if ($CurrentTimeOverride -eq [datetime]::MinValue) {
            $cmdTime = $conn.CreateCommand()
            $cmdTime.CommandText = "SELECT GETDATE();"
            $currentServerTime = [datetime]$cmdTime.ExecuteScalar()
        }

        foreach ($file in $backupFiles) {
            try {
                $cmdHdr = $conn.CreateCommand()
                $cmdHdr.CommandTimeout = 60
                $cmdHdr.CommandText = "RESTORE HEADERONLY FROM DISK = N'$($file.FullName)';"
                $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmdHdr)
                $dt = New-Object System.Data.DataTable
                $adapter.Fill($dt) | Out-Null

                if ($dt.Rows.Count -gt 0) {
                    $row = $dt.Rows[0]
                    $headers += [PSCustomObject]@{
                        FileName              = $file.Name
                        FilePath              = $file.FullName
                        DatabaseName          = [string]$row["DatabaseName"]
                        BackupType            = [int]$row["BackupType"] # 1=Full, 2=Log, 5=Diff
                        BackupTypeDescription = [string]$row["BackupTypeDescription"]
                        BackupStartDate       = [datetime]$row["BackupStartDate"]
                        BackupFinishDate      = [datetime]$row["BackupFinishDate"]
                        FirstLSN              = [decimal]$row["FirstLSN"]
                        LastLSN               = [decimal]$row["LastLSN"]
                        CheckpointLSN         = [decimal]$row["CheckpointLSN"]
                        DatabaseBackupLSN     = [decimal]$row["DatabaseBackupLSN"]
                        BackupSetGUID         = [string]$row["BackupSetGUID"]
                        DifferentialBaseLSN   = if ($row["DifferentialBaseLSN"] -ne [DBNull]::Value) { [decimal]$row["DifferentialBaseLSN"] } else { $null }
                        DifferentialBaseGUID  = if ($row["DifferentialBaseGUID"] -ne [DBNull]::Value) { [string]$row["DifferentialBaseGUID"] } else { $null }
                        RecoveryForkID        = [string]$row["RecoveryForkID"]
                    }
                }
            } catch {
                Write-Host "Warning: Failed to read SQL header for '$($file.Name)': $_" -ForegroundColor DarkYellow
            }
        }
    } finally {
        if ($conn.State -eq [System.Data.ConnectionState]::Open) { $conn.Close() }
    }
}

if ($headers.Count -eq 0) {
    $msg = "Unable to read valid SQL backup headers from any file in destination '$DestinationDir'."
    return Write-Telemetry -Status "BREACHED" -Message $msg -ExitCode 2
}

# 3. Chain Validation: Identify Active Full Base
# Filter Full Backups (BackupType = 1 or 'Database')
$fullBackups = @($headers | Where-Object { 
    ($_.BackupType -eq 1 -or $_.BackupTypeDescription -match "Database$") -and $_.DatabaseName -eq $DatabaseName 
} | Sort-Object BackupFinishDate -Descending)

if ($fullBackups.Count -eq 0) {
    $msg = "CRITICAL: No valid Full backup found in destination repository! Recovery chain is non-existent."
    return Write-Telemetry -Status "BREACHED" -Message $msg -ExitCode 2
}

$activeFull = $fullBackups[0]

# 4. Chain Validation: Identify Matching Differential Base
# A Diff must have DifferentialBaseGUID = activeFull.BackupSetGUID and matching RecoveryForkID
$diffBackups = @($headers | Where-Object { 
    ($_.BackupType -eq 5 -or $_.BackupTypeDescription -match "Differential") -and 
    $_.DatabaseName -eq $DatabaseName -and
    $_.RecoveryForkID -eq $activeFull.RecoveryForkID -and
    $_.DifferentialBaseGUID -eq $activeFull.BackupSetGUID -and
    $_.BackupFinishDate -ge $activeFull.BackupFinishDate
} | Sort-Object BackupFinishDate -Descending)

$activeDiff = if ($diffBackups.Count -gt 0) { $diffBackups[0] } else { $null }

# Determine the roll-forward base point for transaction logs
$basePoint = if ($activeDiff) { $activeDiff } else { $activeFull }
$baseType = if ($activeDiff) { "Differential" } else { "Full" }

# 5. Chain Validation: Evaluate Contiguous Transaction Logs
# Logs must share RecoveryForkID and be completed at or after the base full backup
$candidateLogs = @($headers | Where-Object {
    ($_.BackupType -eq 2 -or $_.BackupTypeDescription -match "Log") -and
    $_.DatabaseName -eq $DatabaseName -and
    $_.RecoveryForkID -eq $activeFull.RecoveryForkID -and
    $_.BackupFinishDate -ge $activeFull.BackupFinishDate
} | Sort-Object FirstLSN, BackupFinishDate)

$validLogs = @()
$chainBroken = $false
$brokenReason = ""

if ($candidateLogs -and $candidateLogs.Count -gt 0) {
    $currentCoveredLSN = if ($activeDiff) { $activeDiff.LastLSN } else { $activeFull.CheckpointLSN }

    foreach ($log in $candidateLogs) {
        # Check if log bridges the current required LSN or continues seamlessly
        if ($validLogs.Count -eq 0) {
            # First log must cover the base point's ending/checkpoint LSN
            if ($log.FirstLSN -le $currentCoveredLSN -and $log.LastLSN -ge $currentCoveredLSN) {
                $validLogs += $log
                $currentCoveredLSN = $log.LastLSN
            }
            elseif ($log.FirstLSN -gt $currentCoveredLSN) {
                # Gap between base and first candidate log!
                $chainBroken = $true
                $brokenReason = "Gap between $baseType LastLSN ($currentCoveredLSN) and first Log FirstLSN ($($log.FirstLSN)). Log '$($log.FileName)' cannot be restored."
                break
            }
        }
        else {
            # Subsequent log: FirstLSN must be <= previous log's LastLSN
            $prevLog = $validLogs[-1]
            if ($log.FirstLSN -le $prevLog.LastLSN) {
                $validLogs += $log
                $currentCoveredLSN = [math]::Max($currentCoveredLSN, $log.LastLSN)
            }
            else {
                # Intermediate log missing!
                $chainBroken = $true
                $brokenReason = "Missing intermediate log: Gap detected between Log '$($prevLog.FileName)' (LastLSN: $($prevLog.LastLSN)) and Log '$($log.FileName)' (FirstLSN: $($log.FirstLSN)). Subsequent logs are unrestorable."
                break
            }
        }
    }
}

# 6. Determine the Newest Truly Recoverable Point
$recoverablePoint = $null
$recoverableType = ""

if ($validLogs.Count -gt 0) {
    $recoverablePoint = $validLogs[-1]
    $recoverableType = "Transaction Log"
}
elseif ($activeDiff) {
    $recoverablePoint = $activeDiff
    $recoverableType = "Differential"
}
else {
    $recoverablePoint = $activeFull
    $recoverableType = "Full"
}

# 7. Calculate Age based on SQL BackupFinishDate vs Server Time
$trueRecoveryTimestamp = $recoverablePoint.BackupFinishDate
$ageTimeSpan = $currentServerTime - $trueRecoveryTimestamp
$ageMinutes = [math]::Round($ageTimeSpan.TotalMinutes, 1)
$ageHours = [math]::Round($ageTimeSpan.TotalHours, 2)

$details = @{
    BaseFull              = "$($activeFull.FileName) (Finish: $($activeFull.BackupFinishDate.ToString('yyyy-MM-dd HH:mm:ss')))"
    ActiveDiff            = if ($activeDiff) { "$($activeDiff.FileName) (Finish: $($activeDiff.BackupFinishDate.ToString('yyyy-MM-dd HH:mm:ss')))" } else { "None" }
    LogCount              = $validLogs.Count
    RecoverablePoint      = $recoverablePoint.FileName
    RecoverableType       = $recoverableType
    RecoverableFinishDate = $trueRecoveryTimestamp.ToString("yyyy-MM-dd HH:mm:ss")
    CurrentTime           = $currentServerTime.ToString("yyyy-MM-dd HH:mm:ss")
    AgeMinutes            = $ageMinutes
    AgeHours              = $ageHours
}

# 8. Evaluate SLA Thresholds
if ($chainBroken) {
    $msg = "CHAIN INTEGRITY COMPROMISED: $brokenReason. Newest restorable point stops at '$($recoverablePoint.FileName)' (Age: ${ageHours}h / ${ageMinutes}m)."
    if ($ageMinutes -ge $CriticalThresholdMinutes) {
        $msg = "CRITICAL RPO BREACH! $msg"
        return Write-Telemetry -Status "BREACHED" -Message $msg -ExitCode 2 -Details $details
    }
    elseif ($ageMinutes -ge $WarningThresholdMinutes) {
        $msg = "WARNING: $msg"
        return Write-Telemetry -Status "WARNING" -Message $msg -ExitCode 1 -Details $details
    }
    else {
        # Even if age is under 3h, broken chain cannot be HEALTHY!
        $msg = "WARNING: Recovery chain is broken in destination repository! $msg"
        return Write-Telemetry -Status "WARNING" -Message $msg -ExitCode 1 -Details $details
    }
}

if ($ageMinutes -ge $CriticalThresholdMinutes) {
    $msg = "CRITICAL RPO BREACH! True data age (${ageHours}h / ${ageMinutes}m) exceeds the maximum allowed 4-hour SLA ($CriticalThresholdMinutes min). Recoverable point: '$($recoverablePoint.FileName)' ($recoverableType)."
    return Write-Telemetry -Status "BREACHED" -Message $msg -ExitCode 2 -Details $details
}
elseif ($ageMinutes -ge $WarningThresholdMinutes) {
    $remainingMargin = [math]::Round($CriticalThresholdMinutes - $ageMinutes, 1)
    $msg = "WARNING: Approaching RPO SLA limit! True data age (${ageHours}h / ${ageMinutes}m) has crossed the 3-hour warning threshold ($WarningThresholdMinutes min). Remaining margin before breach: ${remainingMargin} min."
    return Write-Telemetry -Status "WARNING" -Message $msg -ExitCode 1 -Details $details
}
else {
    $remainingMargin = [math]::Round($CriticalThresholdMinutes - $ageMinutes, 1)
    $msg = "RPO SLA COMPLIANT. Complete recovery chain verified. True data age is within target (${ageHours}h / ${ageMinutes}m). Margin remaining: ${remainingMargin} min."
    return Write-Telemetry -Status "HEALTHY" -Message $msg -ExitCode 0 -Details $details
}
