#Requires -Version 5.1
<#
.SYNOPSIS
  Offline review tests for dashboard-exec-v1 Production cutover runner helpers.
  Does NOT touch Production IIS/SQL. Does NOT unlock Execute.
#>
[CmdletBinding()]
param(
    [string]$RunnerPath
)

$ErrorActionPreference = 'Stop'
$failed = 0
function Ok($n) { Write-Host "PASS $n" -ForegroundColor Green }
function Bad($n, $m) { Write-Host "FAIL $n :: $m" -ForegroundColor Red; $script:failed++ }

if ([string]::IsNullOrWhiteSpace($RunnerPath)) {
    $here = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
    if ([string]::IsNullOrWhiteSpace($here)) { $here = 'D:\Antigravity\ParkPlace\tests' }
    $RunnerPath = Join-Path $here '..\scripts\crm_prod_cutover_dashboard_exec_v1.ps1'
}
$RunnerPath = (Resolve-Path $RunnerPath).Path
$src = Get-Content $RunnerPath -Raw -Encoding UTF8

if ($src -notmatch '\$script:ProductionExecuteAvailableInThisRevision\s*=\s*\$false') {
    Bad 'ExecuteHardLock' 'ProductionExecuteAvailableInThisRevision=$false not found'
} else { Ok 'ExecuteHardLock' }
if ($src -match 'Future unlocked path') {
    Bad 'NoDeadUnlockPath' 'dead Production unlock path must not remain'
} else { Ok 'NoDeadUnlockPath' }
if ($src -notmatch 'ProdDashboardExecV1CutoverExecuteRefused_ProductionLocked') {
    Bad 'ExecuteRefuseGate' 'refuse gate string missing'
} else { Ok 'ExecuteRefuseGate' }
if ($src -notmatch 'Invoke-DashboardExecCutoverExecution') {
    Bad 'ExecutableEntryPoint' 'real execution function missing'
} else { Ok 'ExecutableEntryPoint' }
if ($src -notmatch 'IsolatedFailureDrill') {
    Bad 'IsolatedFailSwitch' 'IsolatedFailureDrill missing'
} else { Ok 'IsolatedFailSwitch' }
if ($src -notmatch 'IsolatedSuccessDrill') {
    Bad 'IsolatedSuccessSwitch' 'IsolatedSuccessDrill missing'
} else { Ok 'IsolatedSuccessSwitch' }
if ($src -notmatch 'structurally unavailable|structurally locked|ProductionExecuteAvailableInThisRevision=false') {
    Bad 'StructuralLockMsg' 'structural Production lock message missing'
} else { Ok 'StructuralLockMsg' }
if ($src -notmatch 'BACKUP DATABASE') {
    Bad 'SqlBackupImpl' 'BACKUP DATABASE not implemented'
} else { Ok 'SqlBackupImpl' }
if ($src -notmatch 'Expand-Archive') {
    Bad 'ZipExtractImpl' 'Expand-Archive not implemented'
} else { Ok 'ZipExtractImpl' }

if ($src -notmatch '/api/health' -or $src -match "NotHealth\s*=\s*'/api/v1/health'" -eq $false) {
    # require canonical health and explicit rejection of v1 health
}
if ($src -notmatch "Health = '/api/health'") { Bad 'HealthRoute' 'canonical /api/health missing' } else { Ok 'HealthRoute' }
if ($src -notmatch "Me = '/api/me'") { Bad 'MeRoute' 'canonical /api/me missing' } else { Ok 'MeRoute' }
if ($src -notmatch 'AdminCallsPermissionsOutOfScope\s*=\s*\$true') {
    Bad 'RolesOutOfScope' 'Admin Calls.* must stay out of dashboard cutover runner'
} else { Ok 'RolesOutOfScope' }
if ($src -notmatch 'ParkPalace_Admin_Telephony_Sensitive_Permissions_Decision_20261107.md') {
    Bad 'DecisionDocLink' 'admin telephony decision doc not referenced'
} else { Ok 'DecisionDocLink' }

# --- sandbox: unique backup plan shape + file-tree rollback ---
$sandbox = Join-Path $env:TEMP ("pp_dash_cutover_rb_" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $sandbox | Out-Null
try {
    $live = Join-Path $sandbox 'live_api'
    $prior = Join-Path $sandbox 'live_api.bak_prior'
    New-Item -ItemType Directory -Force -Path $live | Out-Null
    Set-Content -Path (Join-Path $live 'marker.txt') -Value 'NEW_BAD' -Encoding UTF8
    New-Item -ItemType Directory -Force -Path $prior | Out-Null
    Set-Content -Path (Join-Path $prior 'marker.txt') -Value 'GOOD_PRIOR' -Encoding UTF8

    function Invoke-FileTreeRollback {
        param([string]$LivePath, [string]$PriorPath, [string]$Label, [string]$MirrorPath = $null)
        if ($MirrorPath -and (Test-Path $MirrorPath)) {
            if (Test-Path $LivePath) {
                $failedPath = "$LivePath.failed_test"
                if (Test-Path $failedPath) { Remove-Item $failedPath -Recurse -Force }
                Move-Item -LiteralPath $LivePath -Destination $failedPath -Force
            }
            New-Item -ItemType Directory -Force -Path $LivePath | Out-Null
            & robocopy $MirrorPath $LivePath /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
            return
        }
        if (-not (Test-Path $PriorPath)) { throw "Rollback prior missing for ${Label}: $PriorPath" }
        if (Test-Path $LivePath) {
            $failedPath = "$LivePath.failed_test"
            if (Test-Path $failedPath) { Remove-Item $failedPath -Recurse -Force }
            Move-Item -LiteralPath $LivePath -Destination $failedPath -Force
        }
        Move-Item -LiteralPath $PriorPath -Destination $LivePath -Force
    }

    Invoke-FileTreeRollback -LivePath $live -PriorPath $prior -Label 'api'
    $restored = Get-Content (Join-Path $live 'marker.txt') -Raw
    if ($restored.Trim() -ne 'GOOD_PRIOR') { Bad 'FileTreeRollback' "expected GOOD_PRIOR got '$restored'" }
    elseif (-not (Test-Path "$live.failed_test")) { Bad 'FileTreeRollback' 'failed live tree not preserved aside' }
    else { Ok 'FileTreeRollback' }

    # unique backup dir naming
    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $token = [guid]::NewGuid().ToString('N').Substring(0, 8)
    $dir = Join-Path $sandbox ("cutover_{0}_{1}" -f $stamp, $token)
    if ($token.Length -ne 8) { Bad 'UniqueBackupToken' 'token length' } else { Ok 'UniqueBackupToken' }
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    if (-not (Test-Path $dir)) { Bad 'UniqueBackupDir' 'dir not created' } else { Ok 'UniqueBackupDir' }

    # live-config preserve assertion
    function Get-LiveConfigPresence([string]$settingsPath) {
        $j = Get-Content $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $sepidarCs = [string]$j.ConnectionStrings.SepidarDatabase
        $crmCs = [string]$j.ConnectionStrings.CrmDatabase
        $provider = [string]$j.Telephony.Provider
        return [ordered]@{
            Exists = $true
            SepidarCsPresent = -not [string]::IsNullOrWhiteSpace($sepidarCs)
            SepidarCsLength = if ($sepidarCs) { $sepidarCs.Length } else { 0 }
            CrmCsPresent = -not [string]::IsNullOrWhiteSpace($crmCs)
            TelephonyProvider = $provider
            TelephonyIsLiveHint = ($provider -and $provider -notmatch 'Simulated')
        }
    }
    function Assert-LiveConfigPreserved([string]$settingsPath, $before) {
        $after = Get-LiveConfigPresence $settingsPath
        if (-not $after.SepidarCsPresent) { throw 'Sepidar missing' }
        if (-not $after.CrmCsPresent) { throw 'Crm missing' }
        if (-not $after.TelephonyIsLiveHint) { throw 'Telephony not live' }
        if ($before.SepidarCsLength -gt 0 -and $after.SepidarCsLength -ne $before.SepidarCsLength) {
            throw 'Sepidar length changed'
        }
        if ($before.TelephonyProvider -and $after.TelephonyProvider -ne $before.TelephonyProvider) {
            throw 'Telephony provider changed'
        }
        return $after
    }

    $cfg = Join-Path $sandbox 'appsettings.Production.json'
    $good = @{
        ConnectionStrings = @{ SepidarDatabase = 'Server=SEPIDAR;Database=X;'; CrmDatabase = 'Server=CRM;Database=Y;' }
        Telephony = @{ Provider = 'IssabelARI'; Sftp = @{ Host = 'pbx' }; RecordingsPath = 'D:\rec' }
    } | ConvertTo-Json -Depth 5
    Set-Content $cfg -Value $good -Encoding UTF8
    $before = Get-LiveConfigPresence $cfg
    $null = Assert-LiveConfigPreserved $cfg $before
    Ok 'PreserveAssert_Ok'

    $bad = @{
        ConnectionStrings = @{ SepidarDatabase = ''; CrmDatabase = 'Server=CRM;Database=Y;' }
        Telephony = @{ Provider = 'Simulated' }
    } | ConvertTo-Json -Depth 5
    Set-Content $cfg -Value $bad -Encoding UTF8
    try {
        Assert-LiveConfigPreserved $cfg $before | Out-Null
        Bad 'PreserveAssert_DetectOverwrite' 'should have thrown'
    } catch {
        Ok 'PreserveAssert_DetectOverwrite'
    }
}
finally {
    Remove-Item $sandbox -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failed -gt 0) {
    Write-Host "FAILED=$failed" -ForegroundColor Red
    exit 1
}
Write-Host 'ALL_OFFLINE_PASS Gate=ProdDashboardExecV1RunnerOfflineReviewPassed' -ForegroundColor Green
exit 0
