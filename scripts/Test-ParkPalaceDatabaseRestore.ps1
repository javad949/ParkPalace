<#
.SYNOPSIS
    Test-ParkPalaceDatabaseRestore.ps1 - Automated disaster recovery drill and integrity verification
.DESCRIPTION
    Restores a backup file or a Full+Differential chain to an isolated database instance without affecting production.
    Executes DBCC CHECKDB and table health validation.
    Cleans up the drill database after verification.
#>
[CmdletBinding()]
param (
    [Parameter()]
    [string]$BackupFilePath = "",

    [Parameter()]
    [string]$FullBackupPath = "",

    [Parameter()]
    [string]$DiffBackupPath = "",

    [Parameter()]
    [string[]]$LogBackupPaths = @(),

    [Parameter()]
    [string]$ServerInstance = "localhost",

    [Parameter()]
    [string]$TestDatabaseName = "ParkPalaceCRM_RestoreDrill",

    [Parameter()]
    [string]$RelocateDir = "D:\SQLData\RestoreDrill",

    [Parameter()]
    [switch]$KeepDrillDatabase = $false
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

# Resolve target full backup file
$targetFullBackup = if (-not [string]::IsNullOrWhiteSpace($FullBackupPath)) { $FullBackupPath } else { $BackupFilePath }
if ([string]::IsNullOrWhiteSpace($targetFullBackup) -or -not (Test-Path -Path $targetFullBackup)) {
    throw "Target Full backup file not found: '$targetFullBackup'"
}

if (-not [string]::IsNullOrWhiteSpace($DiffBackupPath) -and -not (Test-Path -Path $DiffBackupPath)) {
    throw "Differential backup file specified but not found: '$DiffBackupPath'"
}

if (-not (Test-Path -Path $RelocateDir)) {
    New-Item -ItemType Directory -Path $RelocateDir -Force | Out-Null
}

Write-Log "================================================================================"
Write-Log " STARTING RECOVERY RESTORE DRILL"
Write-Log " Target Full Backup: $targetFullBackup"
if (-not [string]::IsNullOrWhiteSpace($DiffBackupPath)) {
    Write-Log " Target Diff Backup: $DiffBackupPath"
}
Write-Log " Drill Database: $TestDatabaseName"
Write-Log "================================================================================"

Add-Type -AssemblyName 'System.Data'
$connString = "Server=$ServerInstance;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=30"
$conn = New-Object System.Data.SqlClient.SqlConnection($connString)
$conn.Open()

try {
    # 1. Inspect Logical File Names from Full Backup Header
    Write-Log "Reading logical files from Full backup headers..."
    $cmdFileList = $conn.CreateCommand()
    $cmdFileList.CommandText = "RESTORE FILELISTONLY FROM DISK = N'$targetFullBackup';"
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmdFileList)
    $fileListTable = New-Object System.Data.DataTable
    $adapter.Fill($fileListTable) | Out-Null

    $dataLogical = ""
    $logLogical = ""
    foreach ($row in $fileListTable.Rows) {
        if ($row["Type"] -eq "D" -and [string]::IsNullOrEmpty($dataLogical)) {
            $dataLogical = $row["LogicalName"]
        }
        elseif ($row["Type"] -eq "L" -and [string]::IsNullOrEmpty($logLogical)) {
            $logLogical = $row["LogicalName"]
        }
    }

    $drillMdf = Join-Path $RelocateDir "$($TestDatabaseName).mdf"
    $drillLdf = Join-Path $RelocateDir "$($TestDatabaseName).ldf"

    Write-Log "Logical files detected: Data='$dataLogical', Log='$logLogical'."
    Write-Log "Relocating files to: MDF='$drillMdf', LDF='$drillLdf'..."

    # 2. Execute RESTORE Full Backup and chained Diff / Log backups
    $hasDiff = -not [string]::IsNullOrWhiteSpace($DiffBackupPath)
    $hasLogs = ($LogBackupPaths -and $LogBackupPaths.Count -gt 0)

    if ($hasDiff -or $hasLogs) {
        Write-Log "Restoring Full backup WITH NORECOVERY for roll-forward..."
        $sqlRestoreFull = @"
RESTORE DATABASE [$TestDatabaseName]
FROM DISK = N'$targetFullBackup'
WITH REPLACE, NORECOVERY,
     MOVE N'$dataLogical' TO N'$drillMdf',
     MOVE N'$logLogical' TO N'$drillLdf';
"@
        $cmdRestoreFull = $conn.CreateCommand()
        $cmdRestoreFull.CommandTimeout = 1200
        $cmdRestoreFull.CommandText = $sqlRestoreFull
        $cmdRestoreFull.ExecuteNonQuery() | Out-Null
        Write-Log "Full backup restored with NORECOVERY." "SUCCESS"

        if ($hasDiff) {
            $diffRecOption = if ($hasLogs) { "NORECOVERY" } else { "RECOVERY" }
            Write-Log "Applying Differential backup WITH $diffRecOption from: $DiffBackupPath ..."
            $sqlRestoreDiff = @"
RESTORE DATABASE [$TestDatabaseName]
FROM DISK = N'$DiffBackupPath'
WITH $diffRecOption;
"@
            $cmdRestoreDiff = $conn.CreateCommand()
            $cmdRestoreDiff.CommandTimeout = 1200
            $cmdRestoreDiff.CommandText = $sqlRestoreDiff
            $cmdRestoreDiff.ExecuteNonQuery() | Out-Null
            Write-Log "Differential backup applied with $diffRecOption." "SUCCESS"
        }

        if ($hasLogs) {
            for ($i = 0; $i -lt $LogBackupPaths.Count; $i++) {
                $trnPath = $LogBackupPaths[$i]
                if (-not (Test-Path $trnPath)) {
                    throw "Transaction log backup file not found: '$trnPath'"
                }
                $trnRecOption = if ($i -eq $LogBackupPaths.Count - 1) { "RECOVERY" } else { "NORECOVERY" }
                Write-Log "Applying Transaction Log backup ($($i+1)/$($LogBackupPaths.Count)) WITH $trnRecOption from: $trnPath ..."
                $sqlRestoreLog = @"
RESTORE LOG [$TestDatabaseName]
FROM DISK = N'$trnPath'
WITH $trnRecOption;
"@
                $cmdRestoreLog = $conn.CreateCommand()
                $cmdRestoreLog.CommandTimeout = 1200
                $cmdRestoreLog.CommandText = $sqlRestoreLog
                $cmdRestoreLog.ExecuteNonQuery() | Out-Null
                Write-Log "Transaction Log ($($i+1)/$($LogBackupPaths.Count)) applied with $trnRecOption." "SUCCESS"
            }
        }
        Write-Log "Roll-forward sequence completed. Database is ONLINE." "SUCCESS"
    }
    else {
        Write-Log "Restoring standalone Full backup WITH RECOVERY..."
        $sqlRestoreSingle = @"
RESTORE DATABASE [$TestDatabaseName]
FROM DISK = N'$targetFullBackup'
WITH REPLACE, RECOVERY,
     MOVE N'$dataLogical' TO N'$drillMdf',
     MOVE N'$logLogical' TO N'$drillLdf';
"@
        $cmdRestoreSingle = $conn.CreateCommand()
        $cmdRestoreSingle.CommandTimeout = 1200
        $cmdRestoreSingle.CommandText = $sqlRestoreSingle
        $cmdRestoreSingle.ExecuteNonQuery() | Out-Null
        Write-Log "Standalone Full backup restore completed with RECOVERY." "SUCCESS"
    }

    # 3. Integrity Verification via DBCC CHECKDB
    Write-Log "Running DBCC CHECKDB on drill database..."
    $cmdCheck = $conn.CreateCommand()
    $cmdCheck.CommandTimeout = 1200
    $cmdCheck.CommandText = "DBCC CHECKDB ([$TestDatabaseName]) WITH NO_INFOMSGS, ALL_ERRORMSGS;"
    $cmdCheck.ExecuteNonQuery() | Out-Null
    Write-Log "DBCC CHECKDB passed with 0 allocation and 0 consistency errors." "SUCCESS"

    # 4. Query Core Entity Tables for Verification
    Write-Log "Validating core domain entities..."
    $cmdQuery = $conn.CreateCommand()
    $cmdQuery.CommandText = @"
USE [$TestDatabaseName];
SELECT 
    (SELECT COUNT(*) FROM dbo.Users) AS UserCount,
    (SELECT COUNT(*) FROM dbo.Parties) AS PartyCount,
    (SELECT COUNT(*) FROM crm.PurchaseOrders) AS PoCount,
    (SELECT COUNT(*) FROM dbo.Materials) AS MaterialCount,
    (SELECT COUNT(*) FROM crm.PurchaseOrderAudits) AS AuditCount;
"@
    $reader = $cmdQuery.ExecuteReader()
    if ($reader.Read()) {
        Write-Log "Entity Verification -> Users: $($reader['UserCount']), Parties: $($reader['PartyCount']), PurchaseOrders: $($reader['PoCount']), Materials: $($reader['MaterialCount']), Audits: $($reader['AuditCount'])." "SUCCESS"
    }
    $reader.Close()

    # 5. Clean up drill database unless KeepDrillDatabase switch is provided
    if (-not $KeepDrillDatabase) {
        Write-Log "Cleaning up temporary drill database [$TestDatabaseName]..."
        $cmdDrop = $conn.CreateCommand()
        $cmdDrop.CommandText = @"
USE [master];
ALTER DATABASE [$TestDatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [$TestDatabaseName];
"@
        $cmdDrop.ExecuteNonQuery() | Out-Null
        
        # Remove physical relocated files if still present
        if (Test-Path $drillMdf) { Remove-Item $drillMdf -Force -ErrorAction SilentlyContinue }
        if (Test-Path $drillLdf) { Remove-Item $drillLdf -Force -ErrorAction SilentlyContinue }
        Write-Log "Drill database dropped and temporary files cleaned." "SUCCESS"
    }
    else {
        Write-Log "KeepDrillDatabase specified. Database [$TestDatabaseName] retained." "WARN"
    }

    Write-Log "Disaster recovery drill PASSED all verification checks." "SUCCESS"
}
finally {
    if ($conn.State -eq [System.Data.ConnectionState]::Open) {
        $conn.Close()
    }
}
