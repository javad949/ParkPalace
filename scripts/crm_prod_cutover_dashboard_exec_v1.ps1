#Requires -Version 5.1
<#
.SYNOPSIS
  dashboard-exec-v1 cutover runner: DryRun by default; executable logic only in isolated drills.
.DESCRIPTION
  DEFAULT = DryRun review (no mutation).
  -IsolatedSuccessDrill  = real extract/swap/preserve + passing file smoke (isolated tree only)
  -IsolatedFailureDrill  = same path with forced smoke fail + automatic rollback
  -ExecuteCutover        = ALWAYS refused in this script revision (Production path hard-blocked).
  Unlocking Production requires a separate reviewed script/PR — not flipping an in-file flag.
  Does NOT flip deploy_allowed. Admin Calls.* out of scope.

  Routes: GET /api/health , GET /api/me

.EXAMPLE
  powershell -File scripts\crm_prod_cutover_dashboard_exec_v1.ps1
  powershell -File scripts\crm_prod_cutover_dashboard_exec_v1.ps1 -IsolatedSuccessDrill -WriteEvidence
  powershell -File scripts\crm_prod_cutover_dashboard_exec_v1.ps1 -IsolatedFailureDrill -WriteEvidence
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
    [string]$IsolatedRoot = 'D:\ParkPlaceCRM\staging\dashboard_exec_v1_cutover_drill',
    [switch]$WriteEvidence,
    [switch]$ExecuteCutover,
    [switch]$IsolatedSuccessDrill,
    [switch]$IsolatedFailureDrill
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$expectedApiZip = '3A2B24F486A914B1A9CAEA47CBCD21FD566342D3DDDBAE8FB4F0B71B4634AD12'
$expectedWebZip = 'E1EC83E2AEDC9868AF03CBC5C9517C91617455C406F22C694F07A3B63494856F'
$expectedInstalledApiDll = '64E20E1397B26026FBD5800050F55078D40EC33742ADDB69B7029A4CDB902134'
$knownCurrentProdApiDll = '7E693A9BDD8BC449BBFA3D037694B86EF46B533BE0DC9C410A36198386B068E0'

# Intentionally NOT a production unlock switch. Do not treat as enablement.
# Production -ExecuteCutover is structurally unavailable in this revision (see Refuse-ProductionExecute).
$script:ProductionExecuteAvailableInThisRevision = $false


$common = Join-Path $PSScriptRoot 'crm_prod_cutover_dashboard_exec_v1.common.ps1'
if (-not (Test-Path $common)) { throw "Missing common helpers: $common" }
. $common


function Invoke-IsolatedDrill {
    param([bool]$ForceFail, [string]$ModeName)
    $drill = Initialize-IsolatedDrillTrees -Root $IsolatedRoot -PackageDir $PackageDir
    $apiLive = $drill.ApiPath
    $webLive = $drill.WebPath
    $pkgDir = $drill.PackageDir
    $bakRoot = $drill.BackupRoot
    $evDir = if ($WriteEvidence) { $EvidenceDir } else { Join-Path $drill.Base 'evidence' }

    $apiZip = Join-Path $pkgDir 'ParkPalace_Api_dashboard-exec-v1.zip'
    $webZip = Join-Path $pkgDir 'ParkPalace_Web_dashboard-exec-v1.zip'
    $liveBefore = Get-LiveConfigPresence (Join-Path $apiLive 'appsettings.Production.json')
    $plan = New-UniqueBackupPlan -Root $bakRoot -OptionalBakPath $null -ApiLive $apiLive -WebLive $webLive

    $exec = Invoke-DashboardExecCutoverExecution `
        -Plan $plan -ApiLive $apiLive -WebLive $webLive `
        -ApiZip $apiZip -WebZip $webZip -LiveConfigBefore $liveBefore `
        -ExpectedDllSha $drill.SyntheticPackageDllSha `
        -DoSqlBackup:$false -SqlInstance $SqlInstance -ProdDb $ProdDb `
        -DoIis:$false -AppPoolName $AppPoolName `
        -SmokeMode Files -ForceSmokeFail:$ForceFail

    $afterApiMarker = $null
    if (Test-Path (Join-Path $apiLive 'marker_prior.txt')) {
        $afterApiMarker = (Get-Content (Join-Path $apiLive 'marker_prior.txt') -TotalCount 1).Trim()
    }
    $afterCfg = Get-LiveConfigPresence (Join-Path $apiLive 'appsettings.Production.json')
    $hasPackageMarker = Test-Path (Join-Path $apiLive 'package_only_marker.txt')
    $hasPriorMarker = ($afterApiMarker -eq 'PRIOR_LIVE_API')

    if ($ForceFail) {
        $rv = [ordered]@{
            RolledBack = [bool]$exec.RolledBack
            RollbackVerified = [bool]$exec.RollbackVerified
            RollbackBinaryHashMatch = [bool]$exec.RollbackBinaryHashMatch
            ApiMarkerRestored = $hasPriorMarker
            PackageOnlyMarkerRemoved = (-not $hasPackageMarker)
            LiveSepidarStillPresent = [bool]$afterCfg.SepidarCsPresent
            TelephonyStillIssabelARI = ($afterCfg.TelephonyProvider -eq 'IssabelARI')
        }
        $pass = $exec.RolledBack -and $exec.RollbackVerified -and -not $exec.Succeeded -and $rv.ApiMarkerRestored -and $rv.PackageOnlyMarkerRemoved -and $rv.LiveSepidarStillPresent -and $rv.TelephonyStillIssabelARI
        $gate = if ($pass) { 'ProdDashboardExecV1IsolatedFailureRollbackPassed' } else { 'ProdDashboardExecV1IsolatedFailureRollbackFailed' }
        $evName = "prod_dashboard_exec_v1_isolated_failure_rollback_$($plan.Stamp).json"
    } else {
        $rv = [ordered]@{
            Succeeded = [bool]$exec.Succeeded
            RolledBack = [bool]$exec.RolledBack
            PackageMarkerPresent = $hasPackageMarker
            PriorMarkerGone = (-not $hasPriorMarker)
            LiveSepidarStillPresent = [bool]$afterCfg.SepidarCsPresent
            TelephonyStillIssabelARI = ($afterCfg.TelephonyProvider -eq 'IssabelARI')
            WebIndexIsPackage = ((Test-Path (Join-Path $webLive 'index.html')) -and ((Get-Content (Join-Path $webLive 'index.html') -TotalCount 1) -match 'package-web'))
        }
        $pass = $exec.Succeeded -and -not $exec.RolledBack -and $rv.PackageMarkerPresent -and $rv.PriorMarkerGone -and $rv.LiveSepidarStillPresent -and $rv.TelephonyStillIssabelARI -and $rv.WebIndexIsPackage
        $gate = if ($pass) { 'ProdDashboardExecV1IsolatedSuccessInstallPassed' } else { 'ProdDashboardExecV1IsolatedSuccessInstallFailed' }
        $evName = "prod_dashboard_exec_v1_isolated_success_install_$($plan.Stamp).json"
    }

    $report = [ordered]@{
        TimestampUtc = [DateTime]::UtcNow.ToString('o')
        Mode = $ModeName
        DeployAllowed = $false
        ExecuteReady = $false
        ProductionExecuteAvailableInThisRevision = [bool]$script:ProductionExecuteAvailableInThisRevision
        ProductionSiteTouched = $false
        ProductionMutated = $false
        IsolatedRoot = $drill.Base
        UniqueBackupPlan = @{ BackupDir = $plan.BackupDir; Token = $plan.Token; Stamp = $plan.Stamp }
        ExecutionSteps = @($exec.Steps)
        ExecutionError = $exec.Error
        ExecutionSucceeded = [bool]$exec.Succeeded
        ExecutionRolledBack = [bool]$exec.RolledBack
        Verification = $rv
        RunnerKind = 'ExecutableLogic_IsolatedDrillsOnly'
        Gate = $gate
        AdminCallsPermissionsOutOfScope = $true
        Note = 'Isolated drill only. Production -ExecuteCutover is structurally unavailable in this revision.'
        FinishedAtUtc = [DateTime]::UtcNow.ToString('o')
    }
    $json = ($report | ConvertTo-Json -Depth 8)
    Write-Output $json
    if ($WriteEvidence) {
        New-Item -ItemType Directory -Force -Path $evDir | Out-Null
        Set-Content -Path (Join-Path $evDir $evName) -Value $json -Encoding UTF8
        Write-Host "Evidence: $(Join-Path $evDir $evName)"
    }
    Write-Host "Gate=$gate" -ForegroundColor $(if ($pass) { 'Green' } else { 'Red' })
    if (-not $pass) { exit 2 }
    exit 0
}

# -------------------- main --------------------
if ($IsolatedSuccessDrill -and $IsolatedFailureDrill) {
    throw 'Specify only one of -IsolatedSuccessDrill or -IsolatedFailureDrill.'
}

$modeLabel = if ($IsolatedSuccessDrill) { 'IsolatedSuccessDrill' } elseif ($IsolatedFailureDrill) { 'IsolatedFailureDrill' } elseif ($ExecuteCutover) { 'ExecuteCutover' } else { 'DryRunReview' }
Write-Host "=== dashboard-exec-v1 cutover runner mode=$modeLabel ProductionExecuteAvailable=$script:ProductionExecuteAvailableInThisRevision ===" -ForegroundColor Cyan

if ($IsolatedSuccessDrill) {
    Invoke-IsolatedDrill -ForceFail:$false -ModeName 'IsolatedSuccessDrill'
}
if ($IsolatedFailureDrill) {
    Invoke-IsolatedDrill -ForceFail:$true -ModeName 'IsolatedFailureDrill'
}

# DryRun / refused Production Execute
$plan = New-UniqueBackupPlan -Root $BackupRoot -OptionalBakPath $BackupPath -ApiLive $ApiPath -WebLive $WebPath
$apiZip = Join-Path $PackageDir 'ParkPalace_Api_dashboard-exec-v1.zip'
$webZip = Join-Path $PackageDir 'ParkPalace_Web_dashboard-exec-v1.zip'
$manifestPath = Join-Path $PackageDir 'manifest.json'
$manifest = $null
if (Test-Path $manifestPath) { $manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json }
$liveBefore = Get-LiveConfigPresence (Join-Path $ApiPath 'appsettings.Production.json')

$report = [ordered]@{
    TimestampUtc = [DateTime]::UtcNow.ToString('o')
    Mode = 'DryRunReview'
    PackageId = 'dashboard-exec-v1'
    DeployAllowed = $false
    ExecuteReady = $false
    ProductionExecuteAvailableInThisRevision = [bool]$script:ProductionExecuteAvailableInThisRevision
    ProductionSiteTouched = $false
    ProductionMutated = $false
    RunnerKind = 'DryRunReview_PlusIsolatedExecutableLogic'
    PlanDoc = 'docs/ParkPalace_DashboardExec_V1_Production_Cutover_Plan_20261107.md'
    DayOfChecklistDoc = 'docs/ParkPalace_DashboardExec_V1_Production_Cutover_DayOf_Checklist_20261107.md'
    AdminTelephonyDecisionDoc = 'docs/ParkPalace_Admin_Telephony_Sensitive_Permissions_Decision_20261107.md'
    AdminCallsPermissionsOutOfScope = $true
    OfflineRollbackNote = 'Isolated success/failure drills do not replace release-window Production rollback.'
    CanonicalRoutes = [ordered]@{ Health = '/api/health'; Me = '/api/me'; NotHealth = '/api/v1/health'; NotMe = '/api/v1/me' }
    UniqueBackupPlan = $plan
    ImplementationComplete = [ordered]@{
        SqlBackupFunction = $true
        ZipExtractAndSwap = $true
        PreserveLiveSettings = $true
        AutoRollback = $true
        IsolatedSuccessDrill = $true
        IsolatedFailureDrill = $true
        ProductionExecuteInThisRevision = $false
    }
}

if ($manifest) {
    $report.Package = [ordered]@{
        Dir = $PackageDir
        ManifestDeployAllowed = [bool]$manifest.deploy_allowed
        ApiZipSha = Sha $apiZip
        WebZipSha = Sha $webZip
        ApiZipMatch = ((Sha $apiZip) -eq $expectedApiZip)
        WebZipMatch = ((Sha $webZip) -eq $expectedWebZip)
    }
}

$report.ProductionFingerprint = [ordered]@{
    ApiDllSha = Sha (Join-Path $ApiPath 'ParkPalace.Api.dll')
    LiveConfigBefore = $liveBefore
    ExpectedPostDeployApiDll = $expectedInstalledApiDll
    IsProductionApiPath = (Test-IsProductionApiPath $ApiPath)
    IsProductionWebPath = (Test-IsProductionWebPath $WebPath)
}

if (Test-IsProductionApiPath $ApiPath) {
    try {
        Import-Module WebAdministration
        $site = Get-Website -Name $SiteName
        $report.Iis = [ordered]@{
            Site = $SiteName; State = $site.State; PhysicalPath = $site.physicalPath
            AppPool = $AppPoolName; AppPoolState = (Get-WebAppPoolState -Name $AppPoolName).Value
        }
        $sql = (sqlcmd -S $SqlInstance -E -d $ProdDb -h -1 -W -s '|' -Q @"
SET NOCOUNT ON;
SELECT EmpHist=(SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE N'%EmployeeIdentity%'),
  FinancePlv=(SELECT COUNT(*) FROM dbo.RolePermissions rp JOIN dbo.Roles r ON r.Id=rp.RoleId JOIN dbo.Permissions p ON p.Id=rp.PermissionId WHERE r.Name=N'Finance' AND p.Code=N'Finance.PartyLedgerView');
"@).Trim()
        $report.SqlReadOnly = $sql
    } catch {
        $report.LiveProbeWarning = $_.Exception.Message
    }
}

if ($ExecuteCutover) {
    # Structural refuse: no in-file flag can enable Production mutation in this revision.
    $report.Mode = 'ExecuteCutoverRequested_Refused'
    $report.Gate = 'ProdDashboardExecV1CutoverExecuteRefused_ProductionLocked'
    $report.Message = 'Production -ExecuteCutover is unavailable in this script revision (ProductionExecuteAvailableInThisRevision=false). Use -IsolatedSuccessDrill / -IsolatedFailureDrill to exercise executable logic. Unlock requires a separate reviewed change that adds an explicit Production path — not a boolean flip. deploy_allowed stays false. Admin Calls.* out of scope.'
    $report.ExecutableEntryPointIsolatedOnly = 'Invoke-DashboardExecCutoverExecution via Isolated* drills'
    $report.FinishedAtUtc = [DateTime]::UtcNow.ToString('o')
    $json = ($report | ConvertTo-Json -Depth 10)
    Write-Output $json
    if ($WriteEvidence) {
        New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null
        Set-Content -Path (Join-Path $EvidenceDir $plan.EvidenceName) -Value $json -Encoding UTF8
    }
    throw $report.Message
}

$report.Gate = 'ProdDashboardExecV1CutoverDryRunReviewReady'
$report.Message = 'DryRun review only. Executable install/rollback proven via isolated drills; Production Execute structurally locked; deploy_allowed=false.'
$report.FinishedAtUtc = [DateTime]::UtcNow.ToString('o')
$json = ($report | ConvertTo-Json -Depth 10)
Write-Output $json
Write-Host "Gate=$($report.Gate) ExecuteReady=false" -ForegroundColor Yellow
if ($WriteEvidence) {
    New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null
    Set-Content -Path (Join-Path $EvidenceDir $plan.EvidenceName) -Value $json -Encoding UTF8
}
