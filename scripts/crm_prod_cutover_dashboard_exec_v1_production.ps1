#Requires -Version 5.1
<#
.SYNOPSIS
  Production edition cutover runner for dashboard-exec-v1 (API+Web only).
.DESCRIPTION
  DEFAULT = Preflight/DryRun on Production paths (no mutation).
  -ExecuteCutover mutates Production only when ALL acknowledgment gates pass:
    1) -IAcknowledgeProductionCutover exactly 'I_ACKNOWLEDGE_PRODUCTION_DASHBOARD_EXEC_CUTOVER'
    2) -MaintenanceWindowId non-empty (operator-chosen window id, recorded in evidence)
    3) -BackupPath points to an existing COPY_ONLY .bak (fresh backup taken before execute, or pass -TakeSqlBackup)
    4) Package ZIP SHAs match accepted staging package
    5) Live Sepidar + IssabelARI presence checks pass
  deploy_allowed remains false. Admin Calls.* RolePermissions are never modified.
  This is a separate reviewed file from the DryRun/isolated-drill runner — not an in-file boolean flip.

.EXAMPLE
  # Preflight only (safe):
  powershell -File scripts\crm_prod_cutover_dashboard_exec_v1_production.ps1 -WriteEvidence

  # Execute (ONLY after human approval of this command):
  powershell -File scripts\crm_prod_cutover_dashboard_exec_v1_production.ps1 `
    -ExecuteCutover `
    -IAcknowledgeProductionCutover 'I_ACKNOWLEDGE_PRODUCTION_DASHBOARD_EXEC_CUTOVER' `
    -MaintenanceWindowId '2026-11-07-dashboard-exec' `
    -TakeSqlBackup `
    -WriteEvidence
#>
[CmdletBinding()]
param(
    [string]$SqlInstance = 'localhost',
    [string]$ProdDb = 'ParkPalaceCRM',
    [string]$ApiPath = 'D:\ParkPlaceCRM\api',
    [string]$WebPath = 'D:\ParkPlaceCRM\web',
    [string]$SiteName = 'ParkPalaceCRM',
    [string]$AppPoolName = 'ParkPalaceCRM',
    [string]$PackageDir = 'D:\ParkPlaceCRM\packages\dashboard-exec-v1\latest',
    [string]$BackupRoot = 'D:\ParkPlaceCRM\backups\prod-dashboard-exec-v1',
    [string]$BackupPath,
    [string]$EvidenceDir = 'D:\ParkPlaceCRM\docs\runs',
    [string]$IAcknowledgeProductionCutover = '',
    [string]$MaintenanceWindowId = '',
    [switch]$TakeSqlBackup,
    [switch]$WriteEvidence,
    [switch]$ExecuteCutover
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$common = Join-Path $PSScriptRoot 'crm_prod_cutover_dashboard_exec_v1.common.ps1'
if (-not (Test-Path $common)) { throw "Missing common helpers: $common" }
. $common

$ackExact = 'I_ACKNOWLEDGE_PRODUCTION_DASHBOARD_EXEC_CUTOVER'
$expectedApiZip = $script:DashboardExecExpectedApiZip
$expectedWebZip = $script:DashboardExecExpectedWebZip
$expectedDll = $script:DashboardExecExpectedInstalledApiDll

Write-Host '=== dashboard-exec-v1 PRODUCTION edition (default=preflight; Execute gated) ===' -ForegroundColor Cyan

Assert-ProductionApiAndWebPaths -ApiPath $ApiPath -WebPath $WebPath

$apiZip = Join-Path $PackageDir 'ParkPalace_Api_dashboard-exec-v1.zip'
$webZip = Join-Path $PackageDir 'ParkPalace_Web_dashboard-exec-v1.zip'
$manifestPath = Join-Path $PackageDir 'manifest.json'
foreach ($p in @($apiZip, $webZip, $manifestPath)) {
    if (-not (Test-Path $p)) { throw "Missing package artifact: $p" }
}
$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$liveBefore = Get-LiveConfigPresence (Join-Path $ApiPath 'appsettings.Production.json')
$plan = New-UniqueBackupPlan -Root $BackupRoot -OptionalBakPath $BackupPath -ApiLive $ApiPath -WebLive $WebPath

Import-Module WebAdministration
$site = Get-Website -Name $SiteName
$poolState = (Get-WebAppPoolState -Name $AppPoolName).Value

$report = [ordered]@{
    TimestampUtc = [DateTime]::UtcNow.ToString('o')
    Edition = 'Production'
    Mode = 'Preflight'
    PackageId = 'dashboard-exec-v1'
    DeployAllowed = $false
    ManifestDeployAllowed = [bool]$manifest.deploy_allowed
    AdminCallsPermissionsOutOfScope = $true
    AdminTelephonyDecisionDoc = 'docs/ParkPalace_Admin_Telephony_Sensitive_Permissions_Decision_20261107.md'
    DecisionPackageDoc = 'docs/ParkPalace_DashboardExec_V1_Cutover_Decision_Package_20261107.md'
    DayOfChecklistDoc = 'docs/ParkPalace_DashboardExec_V1_Production_Cutover_DayOf_Checklist_20261107.md'
    CanonicalRoutes = @{ Health = '/api/health'; Me = '/api/me' }
    UniqueBackupPlan = $plan
    Package = [ordered]@{
        Dir = $PackageDir
        ApiZipSha = Sha $apiZip
        WebZipSha = Sha $webZip
        ApiZipMatch = ((Sha $apiZip) -eq $expectedApiZip)
        WebZipMatch = ((Sha $webZip) -eq $expectedWebZip)
    }
    ProductionFingerprint = [ordered]@{
        ApiPath = $ApiPath
        WebPath = $WebPath
        ApiDllSha = Sha (Join-Path $ApiPath 'ParkPalace.Api.dll')
        ExpectedPostDeployApiDll = $expectedDll
        LiveConfigBefore = $liveBefore
        SiteState = $site.State
        AppPoolState = $poolState
    }
    Gates = @()
}

$gateList = [System.Collections.Generic.List[object]]::new()
function Add-Gate([string]$Name, [bool]$Passed, $Detail = $null) {
    $gateList.Add(@{ Name = $Name; Passed = $Passed; Detail = $Detail }) | Out-Null
}

Add-Gate 'DeployAllowedFalse' ($manifest.deploy_allowed -eq $false)
Add-Gate 'PackageShaMatch' ($report.Package.ApiZipMatch -and $report.Package.WebZipMatch)
Add-Gate 'LiveSepidarPresent' ([bool]$liveBefore.SepidarCsPresent)
Add-Gate 'LiveTelephonyIssabelARI' ($liveBefore.TelephonyProvider -eq 'IssabelARI')
Add-Gate 'SiteAndPoolStarted' ($site.State -eq 'Started' -and $poolState -eq 'Started')
Add-Gate 'ProductionPathsExact' $true

$sql = (sqlcmd -S $SqlInstance -E -d $ProdDb -h -1 -W -s '|' -Q @"
SET NOCOUNT ON;
SELECT EmpHist=(SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE N'%EmployeeIdentity%'),
  FinancePlv=(SELECT COUNT(*) FROM dbo.RolePermissions rp JOIN dbo.Roles r ON r.Id=rp.RoleId JOIN dbo.Permissions p ON p.Id=rp.PermissionId WHERE r.Name=N'Finance' AND p.Code=N'Finance.PartyLedgerView'),
  AdminListenSupervise=(SELECT COUNT(*) FROM dbo.RolePermissions rp JOIN dbo.Roles r ON r.Id=rp.RoleId JOIN dbo.Permissions p ON p.Id=rp.PermissionId
    WHERE r.Name=N'Admin' AND p.Code IN (N'Calls.Recordings.Listen',N'Calls.RecordingsListen',N'Calls.Supervise'));
"@).Trim()
$report.SqlReadOnly = $sql
Add-Gate 'SqlReadOnlyOk' ($sql -match '^\d+\|\d+\|\d+$')
$report.Gates = @($gateList)
$preflightOk = (@($gateList | Where-Object { -not $_.Passed }).Count -eq 0)

if (-not $ExecuteCutover) {
    $report.Gate = if ($preflightOk) { 'ProdDashboardExecV1ProductionPreflightReady' } else { 'ProdDashboardExecV1ProductionPreflightPending' }
    $report.ExecuteReady = $false
    $report.ProductionMutated = $false
    $report.Message = 'Production edition preflight only. Pass -ExecuteCutover with acknowledgment gates to mutate. deploy_allowed=false. Admin Calls.* unchanged.'
    $report.ApprovalCommandTemplate = @"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\crm_prod_cutover_dashboard_exec_v1_production.ps1 ``
  -ExecuteCutover ``
  -IAcknowledgeProductionCutover '$ackExact' ``
  -MaintenanceWindowId '<WINDOW_ID>' ``
  -TakeSqlBackup ``
  -WriteEvidence
"@
    $report.FinishedAtUtc = [DateTime]::UtcNow.ToString('o')
    $json = ($report | ConvertTo-Json -Depth 10)
    Write-Output $json
    if ($WriteEvidence) {
        New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null
        Set-Content -Path (Join-Path $EvidenceDir ("prod_dashboard_exec_v1_production_preflight_{0}.json" -f $plan.Stamp)) -Value $json -Encoding UTF8
    }
    Write-Host "Gate=$($report.Gate) ProductionMutated=false" -ForegroundColor Yellow
    exit $(if ($preflightOk) { 0 } else { 2 })
}

# ---- Execute path (gated) ----
$ackOk = ($IAcknowledgeProductionCutover -ceq $ackExact)
$windowOk = -not [string]::IsNullOrWhiteSpace($MaintenanceWindowId)
Add-Gate 'AckPhraseExact' $ackOk
Add-Gate 'MaintenanceWindowIdPresent' $windowOk
Add-Gate 'PreflightStillGreen' $preflightOk
$report.Gates = @($gateList)

if (-not ($ackOk -and $windowOk -and $preflightOk)) {
    $report.Mode = 'ExecuteCutoverRefused_Gates'
    $report.Gate = 'ProdDashboardExecV1ProductionExecuteRefused_IncompleteGates'
    $report.ProductionMutated = $false
    $report.Message = 'Execute refused: acknowledgment / window / preflight gates incomplete. No Production mutation.'
    $report.FinishedAtUtc = [DateTime]::UtcNow.ToString('o')
    $json = ($report | ConvertTo-Json -Depth 10)
    Write-Output $json
    if ($WriteEvidence) {
        New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null
        Set-Content -Path (Join-Path $EvidenceDir $plan.EvidenceName) -Value $json -Encoding UTF8
    }
    throw $report.Message
}

$report.Mode = 'ExecuteCutover'
$report.MaintenanceWindowId = $MaintenanceWindowId
$doSql = [bool]$TakeSqlBackup
if (-not $doSql) {
    if ([string]::IsNullOrWhiteSpace($BackupPath) -or -not (Test-Path $BackupPath)) {
        throw 'Execute requires -TakeSqlBackup or an existing -BackupPath .bak'
    }
    $plan.SqlBakPath = $BackupPath
    $report.ProvidedBackupSha256 = Sha $BackupPath
    Add-Gate 'ProvidedBackupExists' $true
}

$exec = Invoke-DashboardExecCutoverExecution `
    -Plan $plan -ApiLive $ApiPath -WebLive $WebPath `
    -ApiZip $apiZip -WebZip $webZip -LiveConfigBefore $liveBefore `
    -ExpectedDllSha $expectedDll `
    -DoSqlBackup:$doSql -SqlInstance $SqlInstance -ProdDb $ProdDb `
    -DoIis:$true -AppPoolName $AppPoolName `
    -SmokeMode Http -ForceSmokeFail:$false

$report.Execution = [ordered]@{
    Steps = @($exec.Steps)
    Succeeded = [bool]$exec.Succeeded
    RolledBack = [bool]$exec.RolledBack
    Error = $exec.Error
    RollbackError = $exec.RollbackError
    SqlBakPath = $exec.SqlBakPath
    SqlBakSha256 = $exec.SqlBakSha256
    Smoke = $exec.Smoke
}
$report.DeployAllowed = $false
$report.ProductionSiteTouched = $true
$report.ProductionMutated = $true
$report.AdminCallsPermissionsOutOfScope = $true

if ($exec.Succeeded) {
    $report.Gate = 'ProdDashboardExecV1ProductionExecutePassed'
} elseif ($exec.RolledBack) {
    $report.Gate = 'ProdDashboardExecV1ProductionExecuteFailedRolledBack'
} else {
    $report.Gate = 'ProdDashboardExecV1ProductionExecuteFailed'
}

$report.FinishedAtUtc = [DateTime]::UtcNow.ToString('o')
$json = ($report | ConvertTo-Json -Depth 12)
Write-Output $json
if ($WriteEvidence) {
    New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null
    Set-Content -Path (Join-Path $EvidenceDir ("prod_dashboard_exec_v1_production_execute_{0}.json" -f $plan.Stamp)) -Value $json -Encoding UTF8
}
Write-Host "Gate=$($report.Gate)" -ForegroundColor $(if ($exec.Succeeded) { 'Green' } else { 'Red' })
if (-not $exec.Succeeded) { exit 3 }
exit 0
