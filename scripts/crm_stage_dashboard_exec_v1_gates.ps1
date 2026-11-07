#Requires -Version 5.1
<#
.SYNOPSIS
  Post-install HTTP gates only for dashboard-exec-v1 on ParkPalaceCRM-Stage-v1024.
.DESCRIPTION
  Does NOT install the package. Prefer crm_stage_dashboard_exec_v1_runner.ps1 for PreviewOnly/Execute.
  Health URL is /api/health (not /health).

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File D:\Antigravity\ParkPlace\scripts\crm_stage_dashboard_exec_v1_gates.ps1
#>
param(
    [string]$StagingApiBase = 'http://127.0.0.1:51024',
    [string]$EvidenceDir = '',
    [string]$ProductionApiPhysicalPath = 'D:\ParkPalaceCRM\api',
    [string]$StagingSiteName = 'ParkPalaceCRM-Stage-v1024',
    [switch]$IncludeProductionReadOnlyFingerprint
)

$ErrorActionPreference = 'Stop'
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if ([string]::IsNullOrWhiteSpace($EvidenceDir)) {
    $EvidenceDir = Join-Path (Resolve-Path (Join-Path $scriptDir '..')) 'docs\runs'
}
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null
$evidencePath = Join-Path $EvidenceDir "stage_dashboard_exec_v1_gates_$stamp.json"

function Invoke-JsonGet([string]$url, [hashtable]$headers = @{}) {
    try {
        $resp = Invoke-WebRequest -Uri $url -Headers $headers -UseDefaultCredentials -UseBasicParsing -TimeoutSec 60
        return @{ StatusCode = [int]$resp.StatusCode; Body = $resp.Content; Ok = $true }
    } catch {
        $code = 0
        if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
        return @{ StatusCode = $code; Body = $_.ErrorDetails.Message; Ok = $false; Error = $_.Exception.Message }
    }
}

$results = [ordered]@{
    Gate                    = 'Pending'
    ScriptRole              = 'HttpGatesOnly_NoInstall'
    StartedAtUtc            = (Get-Date).ToUniversalTime().ToString('o')
    StagingApiBase          = $StagingApiBase
    StagingSiteName         = $StagingSiteName
    HealthUrl               = "$StagingApiBase/api/health"
    ProductionSiteTouched   = $false
    ProductionMutated       = $false
    deploy_allowed_rc3      = $false
    Checks                  = @()
    Notes                   = @('This script never installs dashboard-exec-v1. A 200 on current RC3 health is not a package install pass.')
}

Write-Host "=== dashboard-exec-v1 HTTP gates only (no install) ===" -ForegroundColor Cyan

$health = Invoke-JsonGet "$StagingApiBase/api/health"
$healthy = ($health.StatusCode -eq 200) -and ($health.Body -match 'Healthy')
$results.Checks += @{ Name = 'StagingHealthHealthy'; StatusCode = $health.StatusCode; Passed = $healthy; Body = $health.Body }
if (-not $healthy) {
    $results.Gate = 'Pending'
    $results.Blocker = "Staging /api/health not Healthy (status=$($health.StatusCode))."
    $results | ConvertTo-Json -Depth 8 | Set-Content $evidencePath -Encoding UTF8
    Write-Host "PENDING: $($results.Blocker)" -ForegroundColor Yellow
    Write-Host "Evidence: $evidencePath"
    exit 2
}

$adminDash = Invoke-JsonGet "$StagingApiBase/api/v1/reports/executive-dashboard" @{ 'X-Active-Role' = 'Admin' }
$adminOk = $adminDash.StatusCode -eq 200
$adminHasLedger = $false
$adminBlockedAr = $false
$endpointExists = $adminDash.StatusCode -ne 404
if ($adminOk -and $adminDash.Body) {
    $j = $adminDash.Body | ConvertFrom-Json
    $adminHasLedger = [bool]$j.viewer.hasPartyLedgerView
    $ar = @($j.cards) | Where-Object { $_.contractId -eq 'ED-06' } | Select-Object -First 1
    $adminBlockedAr = ($null -ne $ar) -and ($null -eq $ar.amount) -and ($ar.status -eq 3 -or "$($ar.status)" -eq 'Blocked')
}
$results.Checks += @{
    Name = 'ExecutiveDashboardEndpointPresent'
    StatusCode = $adminDash.StatusCode
    Passed = $endpointExists
    Note = '404 means dashboard-exec package is NOT installed (RC3 baseline).'
}
$results.Checks += @{
    Name = 'AdminExecutiveDashboard'
    StatusCode = $adminDash.StatusCode
    HasPartyLedgerView = $adminHasLedger
    Ed06BlockedWithoutAmount = $adminBlockedAr
    Passed = ($adminOk -and -not $adminHasLedger -and $adminBlockedAr)
}

# Unassigned role header must not unlock finance; do not use Finance (ops accounts may hold it).
$spoof = Invoke-JsonGet "$StagingApiBase/api/v1/finance/dashboard" @{ 'X-Active-Role' = 'FakeRoleNotAssigned' }
$results.Checks += @{
    Name = 'UnassignedRoleHeader_FinanceDashboardForbidden'
    StatusCode = $spoof.StatusCode
    Passed = ($spoof.StatusCode -eq 403)
}

$finAdmin = Invoke-JsonGet "$StagingApiBase/api/v1/finance/dashboard" @{ 'X-Active-Role' = 'Admin' }
$results.Checks += @{
    Name = 'AdminFinanceDashboardForbidden'
    StatusCode = $finAdmin.StatusCode
    Passed = ($finAdmin.StatusCode -eq 403)
}

if ($IncludeProductionReadOnlyFingerprint -and (Test-Path $ProductionApiPhysicalPath)) {
    $prodDll = Join-Path $ProductionApiPhysicalPath 'ParkPalace.Api.dll'
    $results.ProductionFingerprint = @{
        ApiDllExists = (Test-Path $prodDll)
        ApiDllSha256 = if (Test-Path $prodDll) { (Get-FileHash $prodDll -Algorithm SHA256).Hash } else { $null }
        Note         = 'Read-only fingerprint only; no files written under Production path by this script.'
    }
    $results.Checks += @{ Name = 'ProductionReadOnlyFingerprint'; Passed = $true }
}

$results.ProductionSiteTouched = $false
$results.ProductionMutated = $false
$allPassed = (@($results.Checks) | Where-Object { -not $_.Passed }).Count -eq 0
$results.Gate = if ($allPassed) { 'StageDashboardExecV1HttpGatesPassed' } else { 'Pending' }
$results.FinishedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
$results | ConvertTo-Json -Depth 8 | Set-Content $evidencePath -Encoding UTF8

Write-Host "Gate=$($results.Gate) (HTTP gates only — not an install proof)" -ForegroundColor Yellow
Write-Host "Evidence: $evidencePath"
if ($allPassed) { exit 0 } else { exit 3 }
