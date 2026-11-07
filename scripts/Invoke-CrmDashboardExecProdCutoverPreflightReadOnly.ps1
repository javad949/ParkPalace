#Requires -Version 5.1
<#
.SYNOPSIS
  Read-only Production preflight for a future dashboard-exec-v1 cutover plan.
.DESCRIPTION
  WinRM HTTPS to CRM: fingerprint Production IIS/API/Web, preserve-check live
  Sepidar/Issabel config keys (presence only), compare package SHA on CRM vs local
  accepted package. NEVER stops AppPools, NEVER copies into Production paths,
  NEVER changes SQL, NEVER flips deploy_allowed.
#>
[CmdletBinding()]
param(
    [string]$RemoteHost = 'crm-mgmt.sahar.loc',
    [int]$RemotePort = 5987,
    [string]$LocalRepo = 'D:\Antigravity\ParkPlace',
    [string]$PackageDirLocal = 'D:\Antigravity\ParkPlace\release\dashboard-exec-v1\latest',
    [string]$PackageDirRemote = 'D:\ParkPlaceCRM\packages\dashboard-exec-v1\latest',
    [string]$ProductionApiPath = 'D:\ParkPlaceCRM\api',
    [string]$ProductionWebPath = 'D:\ParkPlaceCRM\web',
    [string]$ProdSiteName = 'ParkPalaceCRM',
    [string]$ProdAppPool = 'ParkPalaceCRM',
    [string]$ProdDb = 'ParkPalaceCRM'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

. (Join-Path $LocalRepo 'scripts\_CrmSecrets.ps1')

function Get-LocalSha([string]$path) {
    if (-not (Test-Path $path)) { return $null }
    return (Get-FileHash -Path $path -Algorithm SHA256).Hash
}

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$localEvidenceDir = Join-Path $LocalRepo 'docs\runs'
New-Item -ItemType Directory -Force -Path $localEvidenceDir | Out-Null
$localEvidence = Join-Path $localEvidenceDir "prod_dashboard_exec_v1_cutover_preflight_readonly_$stamp.json"

$localManifestPath = Join-Path $PackageDirLocal 'manifest.json'
if (-not (Test-Path $localManifestPath)) { throw "Missing local manifest: $localManifestPath" }
$localManifest = Get-Content $localManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$localApiZip = Join-Path $PackageDirLocal 'ParkPalace_Api_dashboard-exec-v1.zip'
$localWebZip = Join-Path $PackageDirLocal 'ParkPalace_Web_dashboard-exec-v1.zip'

$secPass = Get-CrmAdminSecurePassword -Purpose 'WinRM dashboard-exec Production cutover preflight READ-ONLY'
$cred = New-Object System.Management.Automation.PSCredential ('CRM\Administrator', $secPass)
$so = New-PSSessionOption -SkipCACheck:$false -SkipCNCheck:$false -SkipRevocationCheck:$false -OpenTimeout 30000 -OperationTimeout 180000
$uri = "https://${RemoteHost}:${RemotePort}/wsman"
Write-Host "Connecting WinRM $uri (READ-ONLY preflight) ..." -ForegroundColor Cyan
$session = New-PSSession -ConnectionUri $uri -Credential $cred -SessionOption $so -Authentication Negotiate

$bag = [ordered]@{
    StartedAtUtc           = (Get-Date).ToUniversalTime().ToString('o')
    Mode                   = 'ProductionCutoverPreflightReadOnly'
    ProductionMutated      = $false
    ProductionSiteTouched  = $false
    DeployAllowed          = $false
    TransferMethod         = "WinRM HTTPS ${RemoteHost}:${RemotePort}"
    AcceptedPackage        = [ordered]@{
        packageId          = $localManifest.packageId
        deploy_allowed     = [bool]$localManifest.deploy_allowed
        productionTouched  = [bool]$localManifest.productionTouched
        apiZipSha256       = Get-LocalSha $localApiZip
        webZipSha256       = Get-LocalSha $localWebZip
        expectedApiZipSha  = $localManifest.files[0].sha256
        expectedWebZipSha  = $localManifest.files[1].sha256
    }
}

try {
    $remote = Invoke-Command -Session $session -ArgumentList @(
        $PackageDirRemote, $ProductionApiPath, $ProductionWebPath, $ProdSiteName, $ProdAppPool, $ProdDb
    ) -ScriptBlock {
        param($PackageDirRemote, $ProductionApiPath, $ProductionWebPath, $ProdSiteName, $ProdAppPool, $ProdDb)
        $ErrorActionPreference = 'Stop'
        Import-Module WebAdministration -ErrorAction Stop

        function ShaOrNull([string]$p) {
            if (-not (Test-Path $p)) { return $null }
            return (Get-FileHash -Path $p -Algorithm SHA256).Hash
        }
        function Read-JsonFile([string]$p) {
            if (-not (Test-Path $p)) { return $null }
            return Get-Content $p -Raw -Encoding UTF8 | ConvertFrom-Json
        }
        function ConfigPresence($json) {
            if ($null -eq $json) { return $null }
            $sepidarCs = $null
            $provider = $null
            $sftpHost = $null
            $recPath = $null
            try { $sepidarCs = [string]$json.ConnectionStrings.SepidarDatabase } catch {}
            try { if (-not $sepidarCs) { $sepidarCs = [string]$json.Sepidar.Database } } catch {}
            try { if (-not $sepidarCs) { $sepidarCs = [string]$json.Sepidar.SepidarDatabase } } catch {}
            try { $provider = [string]$json.Telephony.Provider } catch {}
            try { $sftpHost = [string]$json.Telephony.Sftp.Host } catch {}
            try { $recPath = [string]$json.Telephony.RecordingsPath } catch {}
            return [ordered]@{
                SepidarConnectionStringPresent = -not [string]::IsNullOrWhiteSpace($sepidarCs)
                SepidarConnectionStringLength  = if ($sepidarCs) { $sepidarCs.Length } else { 0 }
                SepidarLooksLikeServer         = if ($sepidarCs) { ($sepidarCs -match 'Server=|Data Source=') } else { $false }
                TelephonyProvider              = $provider
                TelephonyProviderIsLiveHint    = ($provider -and $provider -notmatch 'Simulated')
                SftpHostPresent                = -not [string]::IsNullOrWhiteSpace($sftpHost)
                RecordingsPathPresent          = -not [string]::IsNullOrWhiteSpace($recPath)
                # Never return connection strings / passwords
            }
        }

        $site = Get-Website -Name $ProdSiteName -ErrorAction SilentlyContinue
        $poolState = $null
        if (Test-Path "IIS:\AppPools\$ProdAppPool") {
            $poolState = (Get-WebAppPoolState -Name $ProdAppPool).Value
        }

        $apiDll = Join-Path $ProductionApiPath 'ParkPalace.Api.dll'
        $webIndex = Join-Path $ProductionWebPath 'index.html'
        $prodSettings = Read-JsonFile (Join-Path $ProductionApiPath 'appsettings.json')
        $prodProdSettings = Read-JsonFile (Join-Path $ProductionApiPath 'appsettings.Production.json')

        $pkgManifest = Read-JsonFile (Join-Path $PackageDirRemote 'manifest.json')
        $pkgApiZip = Join-Path $PackageDirRemote 'ParkPalace_Api_dashboard-exec-v1.zip'
        $pkgWebZip = Join-Path $PackageDirRemote 'ParkPalace_Web_dashboard-exec-v1.zip'

        # Optional: installed staging dashboard-exec API for comparison baseline
        $stageRoot = 'D:\ParkPlaceCRM\staging\dashboard_exec_v1'
        $stageApiDirs = @()
        if (Test-Path $stageRoot) {
            $stageApiDirs = @(Get-ChildItem $stageRoot -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -like 'dashboardexec_api_*' } |
                Sort-Object LastWriteTime -Descending |
                Select-Object -First 3 |
                ForEach-Object {
                    $dll = Join-Path $_.FullName 'ParkPalace.Api.dll'
                    [ordered]@{
                        Path = $_.FullName
                        ApiDllSha256 = ShaOrNull $dll
                        LastWriteTimeUtc = $_.LastWriteTimeUtc.ToString('o')
                    }
                })
        }

        $health = $null
        try {
            # Canonical route is /api/health (HealthController). /api/v1/health is NOT valid.
            $resp = Invoke-WebRequest -Uri 'https://crm.sahar.loc/api/health' -UseBasicParsing -TimeoutSec 20
            $health = [ordered]@{
                Url = 'https://crm.sahar.loc/api/health'
                AuthMode = 'Anonymous'
                StatusCode = [int]$resp.StatusCode
                BodyPreview = ($resp.Content.Substring(0, [Math]::Min(400, $resp.Content.Length)))
            }
        } catch {
            $health = [ordered]@{ Url = 'https://crm.sahar.loc/api/health'; AuthMode = 'Anonymous'; StatusCode = $null; Error = $_.Exception.Message }
        }
        try {
            $respAuth = Invoke-WebRequest -Uri 'https://crm.sahar.loc/api/health' -UseBasicParsing -TimeoutSec 20 -UseDefaultCredentials
            $health = [ordered]@{
                Url = 'https://crm.sahar.loc/api/health'
                AuthMode = 'DefaultCredentials'
                StatusCode = [int]$respAuth.StatusCode
                BodyPreview = ($respAuth.Content.Substring(0, [Math]::Min(400, $respAuth.Content.Length)))
                AnonymousAlsoOk = ($null -ne $health -and $health.StatusCode -eq 200)
            }
        } catch {
            if ($null -eq $health) {
                $health = [ordered]@{ Url = 'https://crm.sahar.loc/api/health'; AuthMode = 'DefaultCredentials'; StatusCode = $null; Error = $_.Exception.Message }
            } else {
                $health.AuthFollowUpError = $_.Exception.Message
            }
        }

        # SQL read-only counts (no DDL/DML)
        $sqlOut = $null
        try {
            $sqlOut = (sqlcmd -S localhost -E -d $ProdDb -h -1 -W -s '|' -Q @"
SET NOCOUNT ON;
SELECT
  EmpHist=(SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE N'%EmployeeIdentity%'),
  FinancePlv=(SELECT COUNT(*) FROM dbo.RolePermissions rp JOIN dbo.Roles r ON r.Id=rp.RoleId JOIN dbo.Permissions p ON p.Id=rp.PermissionId WHERE r.Name=N'Finance' AND p.Code=N'Finance.PartyLedgerView'),
  AdminSens=(SELECT COUNT(*) FROM dbo.RolePermissions rp JOIN dbo.Roles r ON r.Id=rp.RoleId JOIN dbo.Permissions p ON p.Id=rp.PermissionId
             WHERE r.Name=N'Admin' AND p.Code IN (N'Calls.Recordings.Listen',N'Calls.RecordingsListen',N'Calls.Supervise',N'Finance.MatchingResolve',N'Finance.MatchingView',N'Finance.Reconcile',N'Finance.View',N'Invoices.Manage',N'Invoices.View',N'Matching.Resolve',N'Matching.View',N'Reports.Finance')),
  RolesDisplayName=(SELECT CASE WHEN COL_LENGTH('dbo.Roles','DisplayName') IS NULL THEN 0 ELSE 1 END),
  Users=(SELECT COUNT(*) FROM dbo.Users),
  Calls=(SELECT COUNT(*) FROM crm.CallRecords);
"@).Trim()
        } catch {
            $sqlOut = "SQL_ERROR: $($_.Exception.Message)"
        }

        return [ordered]@{
            Site = [ordered]@{
                Name = $ProdSiteName
                State = if ($site) { $site.State } else { 'MISSING' }
                PhysicalPath = if ($site) { $site.physicalPath } else { $null }
                AppPool = $ProdAppPool
                AppPoolState = $poolState
            }
            ProductionPaths = [ordered]@{
                ApiPath = $ProductionApiPath
                WebPath = $ProductionWebPath
                ApiDllExists = (Test-Path $apiDll)
                ApiDllSha256 = ShaOrNull $apiDll
                WebIndexExists = (Test-Path $webIndex)
                WebIndexSha256 = ShaOrNull $webIndex
                ApiDllBytes = if (Test-Path $apiDll) { (Get-Item $apiDll).Length } else { $null }
                ApiDllLastWriteUtc = if (Test-Path $apiDll) { (Get-Item $apiDll).LastWriteTimeUtc.ToString('o') } else { $null }
            }
            LiveConfigPresence = [ordered]@{
                appsettings_json = ConfigPresence $prodSettings
                appsettings_Production_json = ConfigPresence $prodProdSettings
                Note = 'Values not returned; only presence / provider name for preserve planning.'
            }
            PackageOnCrm = [ordered]@{
                DirExists = (Test-Path $PackageDirRemote)
                ManifestDeployAllowed = if ($pkgManifest) { [bool]$pkgManifest.deploy_allowed } else { $null }
                ApiZipSha256 = ShaOrNull $pkgApiZip
                WebZipSha256 = ShaOrNull $pkgWebZip
            }
            RecentIsolatedStageApis = $stageApiDirs
            HealthProbe = $health
            SqlReadOnly = $sqlOut
            ProductionMutated = $false
            FilesWrittenUnderProduction = @()
        }
    }

    $bag.Remote = $remote
    $bag.Comparison = [ordered]@{
        LocalApiZipMatchesManifest = ($bag.AcceptedPackage.apiZipSha256 -eq $bag.AcceptedPackage.expectedApiZipSha)
        LocalWebZipMatchesManifest = ($bag.AcceptedPackage.webZipSha256 -eq $bag.AcceptedPackage.expectedWebZipSha)
        CrmApiZipMatchesAccepted   = ($remote.PackageOnCrm.ApiZipSha256 -eq $bag.AcceptedPackage.expectedApiZipSha)
        CrmWebZipMatchesAccepted   = ($remote.PackageOnCrm.WebZipSha256 -eq $bag.AcceptedPackage.expectedWebZipSha)
        ProdApiDllDiffersFromStageLatest = $null
        ProdApiDllSha256 = $remote.ProductionPaths.ApiDllSha256
        StageLatestApiDllSha256 = if ($remote.RecentIsolatedStageApis -and $remote.RecentIsolatedStageApis.Count -gt 0) { $remote.RecentIsolatedStageApis[0].ApiDllSha256 } else { $null }
    }
    if ($bag.Comparison.StageLatestApiDllSha256) {
        $bag.Comparison.ProdApiDllDiffersFromStageLatest = ($bag.Comparison.ProdApiDllSha256 -ne $bag.Comparison.StageLatestApiDllSha256)
    }

    $checks = @(
        @{ Name = 'DeployAllowedRemainsFalse'; Passed = ($bag.AcceptedPackage.deploy_allowed -eq $false) }
        @{ Name = 'LocalPackageShaMatchManifest'; Passed = ($bag.Comparison.LocalApiZipMatchesManifest -and $bag.Comparison.LocalWebZipMatchesManifest) }
        @{ Name = 'ProductionSiteStarted'; Passed = ($remote.Site.State -eq 'Started' -and $remote.Site.AppPoolState -eq 'Started') }
        @{ Name = 'ProductionApiDllPresent'; Passed = [bool]$remote.ProductionPaths.ApiDllExists }
        @{ Name = 'LiveSepidarConnectionPresent'; Passed = [bool]$remote.LiveConfigPresence.appsettings_Production_json.SepidarConnectionStringPresent }
        @{ Name = 'LiveTelephonyNotSimulated'; Passed = [bool]$remote.LiveConfigPresence.appsettings_Production_json.TelephonyProviderIsLiveHint }
        @{ Name = 'NoProductionMutation'; Passed = ($remote.ProductionMutated -eq $false -and (@($remote.FilesWrittenUnderProduction).Count -eq 0)) }
    )
    $bag.Checks = $checks
    $allOk = (@($checks | Where-Object { -not $_.Passed }).Count -eq 0)
    $bag.Gate = if ($allOk) { 'ProdDashboardExecV1CutoverPreflightReadOnlyPassed' } else { 'ProdDashboardExecV1CutoverPreflightReadOnlyPending' }
    $bag.FinishedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    $bag.ProductionSiteTouched = $false
    $bag.ProductionMutated = $false

    ($bag | ConvertTo-Json -Depth 10) | Set-Content -Path $localEvidence -Encoding UTF8
    Write-Host "Gate=$($bag.Gate)" -ForegroundColor $(if ($allOk) { 'Green' } else { 'Yellow' })
    Write-Host "Evidence: $localEvidence"
    Write-Host "Prod Api.dll SHA: $($remote.ProductionPaths.ApiDllSha256)"
    Write-Host "Accepted API ZIP SHA: $($bag.AcceptedPackage.expectedApiZipSha)"
    Write-Host "CRM package API ZIP match: $($bag.Comparison.CrmApiZipMatchesAccepted)"
}
finally {
    if ($session) { Remove-PSSession $session }
}
