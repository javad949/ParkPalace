<#
.SYNOPSIS
    Test-MonitorParkPalaceRpoHealth.ps1 - Automated test suite for Monitor-ParkPalaceRpoHealth.ps1.
.DESCRIPTION
    Validates the 5 required failure/boundary scenarios:
      1. Fresh file timestamp with old SQL backup header.
      2. Missing intermediate log in chain (LSN gap).
      3. Destination repository unreachable.
      4. Age crossing 3-hour warning threshold (Age = 3.5 hours).
      5. Age crossing 4-hour critical breach threshold (Age = 4.5 hours).
      6. Baseline Healthy chain (Age = 30 minutes).
#>
$ErrorActionPreference = "Stop"

$scriptPath = Join-Path $PSScriptRoot "..\scripts\Monitor-ParkPalaceRpoHealth.ps1"
if (-not (Test-Path $scriptPath)) {
    throw "Monitor script not found at: $scriptPath"
}

$results = @()

function Run-Scenario([string]$Name, [scriptblock]$Action, [int]$ExpectedExitCode, [string]$ExpectedStatus) {
    Write-Host "`n================================================================================" -ForegroundColor Cyan
    Write-Host " RUNNING SCENARIO: $Name" -ForegroundColor Cyan
    Write-Host " Expected Status: $ExpectedStatus (Exit Code: $ExpectedExitCode)" -ForegroundColor Cyan
    Write-Host "================================================================================" -ForegroundColor Cyan

    $actualCode = & $Action
    $passed = ($actualCode -eq $ExpectedExitCode)
    
    $statusText = if ($passed) { "PASS" } else { "FAIL" }
    $color = if ($passed) { "Green" } else { "Red" }
    Write-Host "RESULT: $statusText (Expected: $ExpectedExitCode, Actual: $actualCode)" -ForegroundColor $color

    $script:results += [PSCustomObject]@{
        Scenario         = $Name
        ExpectedExitCode = $ExpectedExitCode
        ActualExitCode   = $actualCode
        ExpectedStatus   = $ExpectedStatus
        Passed           = $passed
    }
}

$refTime = [datetime]"2026-10-24 12:00:00"
$guidBase = [Guid]::NewGuid().ToString().ToUpper()
$forkId = [Guid]::NewGuid().ToString().ToUpper()

# ------------------------------------------------------------------------------------------------
# Scenario 1: Fresh file timestamp with old SQL backup header (e.g. 5 hours old)
# ------------------------------------------------------------------------------------------------
Run-Scenario "1. Fresh file timestamp with old SQL backup header" {
    # Full backup header finish date is 5 hours ago (07:00:00 vs 12:00:00)
    $mockHeaders = @(
        [PSCustomObject]@{
            FileName              = "ParkPalaceCRM_Full_20261024_070000.bak"
            FilePath              = "C:\fake\ParkPalaceCRM_Full.bak"
            DatabaseName          = "ParkPalaceCRM"
            BackupType            = 1
            BackupTypeDescription = "Database"
            BackupStartDate       = $refTime.AddHours(-5.1)
            BackupFinishDate      = $refTime.AddHours(-5.0)
            FirstLSN              = [decimal]1000
            LastLSN               = [decimal]1050
            CheckpointLSN         = [decimal]1000
            DatabaseBackupLSN     = [decimal]900
            BackupSetGUID         = $guidBase
            DifferentialBaseLSN   = $null
            DifferentialBaseGUID  = $null
            RecoveryForkID        = $forkId
        }
    )

    & $scriptPath -DatabaseName "ParkPalaceCRM" -CustomHeaderMetadata $mockHeaders -CurrentTimeOverride $refTime
} 2 "BREACHED"

# ------------------------------------------------------------------------------------------------
# Scenario 2: Missing intermediate log in chain (LSN gap)
# ------------------------------------------------------------------------------------------------
Run-Scenario "2. Missing intermediate log in chain (LSN gap)" {
    # Full is 5h old. Log 1 is 4.5h old (LSN 1000-2000).
    # Log 2 (LSN 2000-3000) is MISSING.
    # Log 3 is fresh (10 min old, LSN 3000-4000).
    $mockHeaders = @(
        [PSCustomObject]@{
            FileName              = "ParkPalaceCRM_Full.bak"
            FilePath              = "C:\fake\ParkPalaceCRM_Full.bak"
            DatabaseName          = "ParkPalaceCRM"
            BackupType            = 1
            BackupTypeDescription = "Database"
            BackupStartDate       = $refTime.AddHours(-5.1)
            BackupFinishDate      = $refTime.AddHours(-5.0)
            FirstLSN              = [decimal]1000
            LastLSN               = [decimal]1050
            CheckpointLSN         = [decimal]1000
            DatabaseBackupLSN     = [decimal]900
            BackupSetGUID         = $guidBase
            DifferentialBaseLSN   = $null
            DifferentialBaseGUID  = $null
            RecoveryForkID        = $forkId
        },
        [PSCustomObject]@{
            FileName              = "ParkPalaceCRM_Log1.trn"
            FilePath              = "C:\fake\ParkPalaceCRM_Log1.trn"
            DatabaseName          = "ParkPalaceCRM"
            BackupType            = 2
            BackupTypeDescription = "Transaction Log"
            BackupStartDate       = $refTime.AddHours(-4.6)
            BackupFinishDate      = $refTime.AddHours(-4.5)
            FirstLSN              = [decimal]1000
            LastLSN               = [decimal]2000
            CheckpointLSN         = [decimal]1000
            DatabaseBackupLSN     = [decimal]1000
            BackupSetGUID         = [Guid]::NewGuid().ToString().ToUpper()
            DifferentialBaseLSN   = $null
            DifferentialBaseGUID  = $null
            RecoveryForkID        = $forkId
        },
        [PSCustomObject]@{
            FileName              = "ParkPalaceCRM_Log3.trn"
            FilePath              = "C:\fake\ParkPalaceCRM_Log3.trn"
            DatabaseName          = "ParkPalaceCRM"
            BackupType            = 2
            BackupTypeDescription = "Transaction Log"
            BackupStartDate       = $refTime.AddMinutes(-15)
            BackupFinishDate      = $refTime.AddMinutes(-10) # Looks fresh!
            FirstLSN              = [decimal]3000 # GAP! (Log 2 from 2000 to 3000 is missing)
            LastLSN               = [decimal]4000
            CheckpointLSN         = [decimal]3000
            DatabaseBackupLSN     = [decimal]1000
            BackupSetGUID         = [Guid]::NewGuid().ToString().ToUpper()
            DifferentialBaseLSN   = $null
            DifferentialBaseGUID  = $null
            RecoveryForkID        = $forkId
        }
    )

    & $scriptPath -DatabaseName "ParkPalaceCRM" -CustomHeaderMetadata $mockHeaders -CurrentTimeOverride $refTime
} 2 "BREACHED"

# ------------------------------------------------------------------------------------------------
# Scenario 3: Destination unreachable
# ------------------------------------------------------------------------------------------------
Run-Scenario "3. Destination repository unreachable" {
    & $scriptPath -DestinationDir "\\invalid-host-999.sahar.loc\CRMBACKUP" -DatabaseName "ParkPalaceCRM" -CurrentTimeOverride $refTime
} 2 "BREACHED"

# ------------------------------------------------------------------------------------------------
# Scenario 4: Age crossing 3-hour warning threshold (Age = 3.5 hours)
# ------------------------------------------------------------------------------------------------
Run-Scenario "4. Age crossing 3-hour warning threshold (Age = 3.5h / 210m)" {
    $diffGuid = [Guid]::NewGuid().ToString().ToUpper()
    $mockHeaders = @(
        [PSCustomObject]@{
            FileName              = "ParkPalaceCRM_Full.bak"
            FilePath              = "C:\fake\ParkPalaceCRM_Full.bak"
            DatabaseName          = "ParkPalaceCRM"
            BackupType            = 1
            BackupTypeDescription = "Database"
            BackupStartDate       = $refTime.AddHours(-10)
            BackupFinishDate      = $refTime.AddHours(-9.9)
            FirstLSN              = [decimal]1000
            LastLSN               = [decimal]1050
            CheckpointLSN         = [decimal]1000
            DatabaseBackupLSN     = [decimal]900
            BackupSetGUID         = $guidBase
            DifferentialBaseLSN   = $null
            DifferentialBaseGUID  = $null
            RecoveryForkID        = $forkId
        },
        [PSCustomObject]@{
            FileName              = "ParkPalaceCRM_Diff.diff"
            FilePath              = "C:\fake\ParkPalaceCRM_Diff.diff"
            DatabaseName          = "ParkPalaceCRM"
            BackupType            = 5
            BackupTypeDescription = "Database Differential"
            BackupStartDate       = $refTime.AddHours(-3.6)
            BackupFinishDate      = $refTime.AddHours(-3.5) # 3.5 hours old
            FirstLSN              = [decimal]1000
            LastLSN               = [decimal]2500
            CheckpointLSN         = [decimal]2000
            DatabaseBackupLSN     = [decimal]1000
            BackupSetGUID         = $diffGuid
            DifferentialBaseLSN   = [decimal]1000
            DifferentialBaseGUID  = $guidBase # Perfectly matches Full
            RecoveryForkID        = $forkId
        }
    )

    & $scriptPath -DatabaseName "ParkPalaceCRM" -CustomHeaderMetadata $mockHeaders -CurrentTimeOverride $refTime
} 1 "WARNING"

# ------------------------------------------------------------------------------------------------
# Scenario 5: Age crossing 4-hour critical breach threshold (Age = 4.5 hours)
# ------------------------------------------------------------------------------------------------
Run-Scenario "5. Age crossing 4-hour critical breach threshold (Age = 4.5h / 270m)" {
    $diffGuid = [Guid]::NewGuid().ToString().ToUpper()
    $mockHeaders = @(
        [PSCustomObject]@{
            FileName              = "ParkPalaceCRM_Full.bak"
            FilePath              = "C:\fake\ParkPalaceCRM_Full.bak"
            DatabaseName          = "ParkPalaceCRM"
            BackupType            = 1
            BackupTypeDescription = "Database"
            BackupStartDate       = $refTime.AddHours(-10)
            BackupFinishDate      = $refTime.AddHours(-9.9)
            FirstLSN              = [decimal]1000
            LastLSN               = [decimal]1050
            CheckpointLSN         = [decimal]1000
            DatabaseBackupLSN     = [decimal]900
            BackupSetGUID         = $guidBase
            DifferentialBaseLSN   = $null
            DifferentialBaseGUID  = $null
            RecoveryForkID        = $forkId
        },
        [PSCustomObject]@{
            FileName              = "ParkPalaceCRM_Diff.diff"
            FilePath              = "C:\fake\ParkPalaceCRM_Diff.diff"
            DatabaseName          = "ParkPalaceCRM"
            BackupType            = 5
            BackupTypeDescription = "Database Differential"
            BackupStartDate       = $refTime.AddHours(-4.6)
            BackupFinishDate      = $refTime.AddHours(-4.5) # 4.5 hours old (Exceeds 4h limit)
            FirstLSN              = [decimal]1000
            LastLSN               = [decimal]2500
            CheckpointLSN         = [decimal]2000
            DatabaseBackupLSN     = [decimal]1000
            BackupSetGUID         = $diffGuid
            DifferentialBaseLSN   = [decimal]1000
            DifferentialBaseGUID  = $guidBase
            RecoveryForkID        = $forkId
        }
    )

    & $scriptPath -DatabaseName "ParkPalaceCRM" -CustomHeaderMetadata $mockHeaders -CurrentTimeOverride $refTime
} 2 "BREACHED"

# ------------------------------------------------------------------------------------------------
# Scenario 6: Baseline Healthy (Age = 30 minutes, complete Full + Diff + Log chain)
# ------------------------------------------------------------------------------------------------
Run-Scenario "6. Baseline Healthy (Age = 30 minutes, Full + Diff + Log chain)" {
    $diffGuid = [Guid]::NewGuid().ToString().ToUpper()
    $mockHeaders = @(
        [PSCustomObject]@{
            FileName              = "ParkPalaceCRM_Full.bak"
            FilePath              = "C:\fake\ParkPalaceCRM_Full.bak"
            DatabaseName          = "ParkPalaceCRM"
            BackupType            = 1
            BackupTypeDescription = "Database"
            BackupStartDate       = $refTime.AddHours(-10)
            BackupFinishDate      = $refTime.AddHours(-9.9)
            FirstLSN              = [decimal]1000
            LastLSN               = [decimal]1050
            CheckpointLSN         = [decimal]1000
            DatabaseBackupLSN     = [decimal]900
            BackupSetGUID         = $guidBase
            DifferentialBaseLSN   = $null
            DifferentialBaseGUID  = $null
            RecoveryForkID        = $forkId
        },
        [PSCustomObject]@{
            FileName              = "ParkPalaceCRM_Diff.diff"
            FilePath              = "C:\fake\ParkPalaceCRM_Diff.diff"
            DatabaseName          = "ParkPalaceCRM"
            BackupType            = 5
            BackupTypeDescription = "Database Differential"
            BackupStartDate       = $refTime.AddHours(-2)
            BackupFinishDate      = $refTime.AddHours(-1.9)
            FirstLSN              = [decimal]1000
            LastLSN               = [decimal]2500
            CheckpointLSN         = [decimal]2000
            DatabaseBackupLSN     = [decimal]1000
            BackupSetGUID         = $diffGuid
            DifferentialBaseLSN   = [decimal]1000
            DifferentialBaseGUID  = $guidBase
            RecoveryForkID        = $forkId
        },
        [PSCustomObject]@{
            FileName              = "ParkPalaceCRM_Log1.trn"
            FilePath              = "C:\fake\ParkPalaceCRM_Log1.trn"
            DatabaseName          = "ParkPalaceCRM"
            BackupType            = 2
            BackupTypeDescription = "Transaction Log"
            BackupStartDate       = $refTime.AddMinutes(-35)
            BackupFinishDate      = $refTime.AddMinutes(-30) # 30 min old
            FirstLSN              = [decimal]2500 # Bridges seamlessly from Diff LastLSN
            LastLSN               = [decimal]3500
            CheckpointLSN         = [decimal]2500
            DatabaseBackupLSN     = [decimal]1000
            BackupSetGUID         = [Guid]::NewGuid().ToString().ToUpper()
            DifferentialBaseLSN   = $null
            DifferentialBaseGUID  = $null
            RecoveryForkID        = $forkId
        }
    )

    & $scriptPath -DatabaseName "ParkPalaceCRM" -CustomHeaderMetadata $mockHeaders -CurrentTimeOverride $refTime
} 0 "HEALTHY"

Write-Host "`n================================================================================" -ForegroundColor Cyan
Write-Host " TEST SUITE SUMMARY RESULTS" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
$results | Format-Table -AutoSize | Out-String | Write-Host

$allPassed = ($results | Where-Object { -not $_.Passed }).Count -eq 0
if ($allPassed) {
    Write-Host "ALL 6 SCENARIOS PASSED WITH 100% ACCURACY!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "SOME SCENARIOS FAILED!" -ForegroundColor Red
    exit 1
}
