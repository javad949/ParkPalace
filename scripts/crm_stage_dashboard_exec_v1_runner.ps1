#Requires -Version 5.1
<#
.SYNOPSIS
  PreviewOnly (default) / Execute runner for dashboard-exec-v1 on an isolated staging site.
  Default params still point at ParkPalaceCRM-Stage-v1024/51024 for backward compatibility;
  the dedicated dashboard site uses ParkPalaceCRM-Stage-DashboardExec / 51025 (pass explicitly).

.DESCRIPTION
  Gate titles (do not confuse):
  - PackageVerifiedOnly  = ZIP/manifest OK on this machine; staging host gates NOT all green; ExecuteEligible=False
  - StagingPreviewPassed = package OK AND IIS+SQL+Health+identity gates OK on CRM; ExecuteEligible=True
  - Pending              = package or required checks failed
  - StageDashboardExecV1ExecutePassed = Execute finished successfully (never claimed without running Execute)

  Execute NEVER mutates Production. On failure, restores prior staging IIS physical paths.

.PARAMETER PackageDir
  Explicit folder on the machine that contains manifest.json + both ZIPs.
  On CRM use: D:\ParkPlaceCRM\packages\dashboard-exec-v1\latest
  Dev-machine default (only for offline package hash check): <repo>\release\dashboard-exec-v1\latest

.EXAMPLE
  # On CRM (after copying package to CRM packages folder):
  powershell -NoProfile -ExecutionPolicy Bypass -File D:\ParkPlaceCRM\scripts\crm_stage_dashboard_exec_v1_runner.ps1 -Mode PreviewOnly -PackageDir D:\ParkPlaceCRM\packages\dashboard-exec-v1\latest -EvidenceDir D:\ParkPlaceCRM\docs\runs

.EXAMPLE
  # Execute ONLY when Preview printed Gate=StagingPreviewPassed and ExecuteEligible=True:
  powershell -NoProfile -ExecutionPolicy Bypass -File D:\ParkPlaceCRM\scripts\crm_stage_dashboard_exec_v1_runner.ps1 -Mode Execute -PackageDir D:\ParkPlaceCRM\packages\dashboard-exec-v1\latest -EvidenceDir D:\ParkPlaceCRM\docs\runs
#>
[CmdletBinding()]
param(
    [ValidateSet('PreviewOnly', 'Execute')]
    [string]$Mode = 'PreviewOnly',

    [string]$PackageDir = '',
    [string]$EvidenceDir = '',

    [string]$StagingSiteName = 'ParkPalaceCRM-Stage-v1024',
    [string]$StagingApiBase = 'http://127.0.0.1:51024',
    [string]$StagingSql = 'localhost',
    [string]$StagingDb = 'ParkPalaceCRM_Stage_20261106_123740_c98981ae',
    [string]$StagingRoot = 'D:\ParkPlaceCRM\staging\v1024_c98981ae',
    [string]$ExpectedRc3ApiPathHint = 'rc3_api_',
    [string]$FallbackRc3ApiPath = 'D:\ParkPlaceCRM\staging\v1024_c98981ae\rc3_api_20261106_143114_25779b88',
    # Exact API physical path allowed for ExecuteEligible (RC3 original OR a dedicated isolated clone).
    [string]$ExpectedApiBaselinePath = '',
    [int]$ExpectedBindingPort = 0,

    [string]$ProductionApiPath = 'D:\ParkPlaceCRM\api',
    [string]$ProductionWebPath = 'D:\ParkPlaceCRM\web',
    [string]$ForbiddenProdSiteName = 'ParkPalaceCRM',
    [string]$ForbiddenProdAppPoolHint = 'ParkPalaceCRM'
)

if ([string]::IsNullOrWhiteSpace($ExpectedApiBaselinePath)) {
    $ExpectedApiBaselinePath = $FallbackRc3ApiPath
}
if ($ExpectedBindingPort -le 0) {
    try {
        $ExpectedBindingPort = ([uri]$StagingApiBase).Port
    } catch {
        $ExpectedBindingPort = 51024
    }
}

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$repoRoot = $null
try { $repoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path } catch { $repoRoot = $null }

# Prefer explicit CRM package path when present; else repo release (dev offline only).
$crmDefaultPackage = 'D:\ParkPlaceCRM\packages\dashboard-exec-v1\latest'
$crmDefaultEvidence = 'D:\ParkPlaceCRM\docs\runs'
if ([string]::IsNullOrWhiteSpace($PackageDir)) {
    if (Test-Path (Join-Path $crmDefaultPackage 'manifest.json')) {
        $PackageDir = $crmDefaultPackage
    } elseif ($repoRoot -and (Test-Path (Join-Path $repoRoot 'release\dashboard-exec-v1\latest\manifest.json'))) {
        $PackageDir = Join-Path $repoRoot 'release\dashboard-exec-v1\latest'
    } else {
        $PackageDir = $crmDefaultPackage
    }
}
if ([string]::IsNullOrWhiteSpace($EvidenceDir)) {
    if (Test-Path 'D:\ParkPlaceCRM') {
        $EvidenceDir = $crmDefaultEvidence
    } elseif ($repoRoot) {
        $EvidenceDir = Join-Path $repoRoot 'docs\runs'
    } else {
        $EvidenceDir = $crmDefaultEvidence
    }
}

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null
$evidencePath = Join-Path $EvidenceDir "stage_dashboard_exec_v1_runner_${Mode}_$stamp.json"

function Write-Check([object]$bag, [string]$name, [bool]$passed, [hashtable]$extra = @{}) {
    $row = @{ Name = $name; Passed = $passed }
    foreach ($k in $extra.Keys) { $row[$k] = $extra[$k] }
    $bag.Checks += $row
    $color = if ($passed) { 'Green' } else { 'Yellow' }
    Write-Host ("[{0}] {1}" -f ($(if ($passed) { 'PASS' } else { 'FAIL' }), $name)) -ForegroundColor $color
}

function Get-Sha256([string]$path) { (Get-FileHash -Path $path -Algorithm SHA256).Hash }

function Invoke-ApiGet([string]$url, [hashtable]$headers = @{}) {
    try {
        $resp = Invoke-WebRequest -Uri $url -Headers $headers -UseDefaultCredentials -UseBasicParsing -TimeoutSec 45
        return [pscustomobject]@{ Ok = $true; Status = [int]$resp.StatusCode; Body = $resp.Content }
    } catch {
        $status = 0
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $status = [int]$_.Exception.Response.StatusCode
        }
        $body = $null
        try {
            if ($_.Exception.Response) {
                $stream = $_.Exception.Response.GetResponseStream()
                if ($stream) { $body = (New-Object System.IO.StreamReader($stream)).ReadToEnd() }
            }
        } catch { }
        return [pscustomobject]@{ Ok = $false; Status = $status; Body = $body; Error = $_.Exception.Message }
    }
}

function Test-PathIsUnderProduction([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) { return $false }
    $full = [System.IO.Path]::GetFullPath($path).TrimEnd('\')
    $prodApi = [System.IO.Path]::GetFullPath($ProductionApiPath).TrimEnd('\')
    $prodWeb = [System.IO.Path]::GetFullPath($ProductionWebPath).TrimEnd('\')
    return ($full -ieq $prodApi) -or ($full -ieq $prodWeb) -or
           ($full.StartsWith($prodApi + '\', [StringComparison]::OrdinalIgnoreCase)) -or
           ($full.StartsWith($prodWeb + '\', [StringComparison]::OrdinalIgnoreCase))
}

function Test-PathIsUnderStagingRoot([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) { return $false }
    $full = [System.IO.Path]::GetFullPath($path).TrimEnd('\')
    $root = [System.IO.Path]::GetFullPath($StagingRoot).TrimEnd('\')
    return ($full -ieq $root) -or $full.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NotProductionPath([string]$label, [string]$path) {
    if (Test-PathIsUnderProduction $path) {
        throw "STOP: $label resolves to Production path '$path'. Refusing."
    }
}

function Get-StagingIisSnapshot {
    Import-Module WebAdministration -ErrorAction Stop
    $site = Get-Website -Name $StagingSiteName -ErrorAction SilentlyContinue
    if (-not $site) { throw "STOP: IIS site '$StagingSiteName' not found." }
    if ($site.Name -eq $ForbiddenProdSiteName) { throw "STOP: refused to operate on Production site name." }

    $bindings = @($site.bindings.Collection | ForEach-Object { $_.bindingInformation })
    $apiApp = Get-WebApplication -Site $StagingSiteName | Where-Object { $_.path -eq '/api' } | Select-Object -First 1
    if (-not $apiApp) { throw "STOP: site '$StagingSiteName' has no /api application." }

    return [pscustomobject]@{
        SiteName        = $site.Name
        SiteState       = "$($site.state)"
        AppPool         = $site.applicationPool
        ApiAppPool      = $apiApp.applicationPool
        Bindings        = $bindings
        WebPhysicalPath = $site.physicalPath
        ApiPhysicalPath = $apiApp.physicalPath
        HasApiApp       = $true
        ApiAppPath      = $apiApp.path
    }
}

function Read-IsolatedFlags([string]$apiDir) {
    $prodPath = Join-Path $apiDir 'appsettings.Production.json'
    if (-not (Test-Path $prodPath)) {
        return @{ Ok = $false; Reason = "missing $prodPath" }
    }
    $j = Get-Content $prodPath -Raw | ConvertFrom-Json
    $cs = [string]$j.ConnectionStrings.CrmDatabase
    $sep = [string]$j.ConnectionStrings.SepidarDatabase
    $prov = if ($j.Telephony) { [string]$j.Telephony.Provider } else { '' }
    $sftp = ''
    if ($j.Telephony -and $j.Telephony.Sftp) { $sftp = [string]$j.Telephony.Sftp.Host }

    $dbOk = $cs -match [regex]::Escape("Database=$StagingDb")
    $sepOk = [string]::IsNullOrWhiteSpace($sep)
    $telOk = $prov -eq 'Simulated'
    $sftpOk = [string]::IsNullOrWhiteSpace($sftp)
    return @{
        Ok            = ($dbOk -and $sepOk -and $telOk -and $sftpOk)
        CrmCs         = $cs
        DbOk          = $dbOk
        SepidarEmpty  = $sepOk
        TelephonySim  = $telOk
        SftpEmpty     = $sftpOk
        Path          = $prodPath
    }
}

function Write-IsolatedStagingAppsettings([string]$apiDir, [string]$crmCs) {
    $prodPath = Join-Path $apiDir 'appsettings.Production.json'
    $isolated = [ordered]@{
        Logging = @{
            LogLevel = @{
                Default = 'Information'
                'Microsoft.AspNetCore' = 'Warning'
                'Microsoft.EntityFrameworkCore.Database.Command' = 'Warning'
            }
        }
        AllowedHosts = '*'
        ConnectionStrings = @{
            CrmDatabase     = $crmCs
            SepidarDatabase = ''
        }
        Auth = @{ Provider = 'Windows'; Domain = 'sahar.loc' }
        Telephony = @{
            Provider = 'Simulated'
            Sftp     = @{ Host = ''; Username = ''; Password = ''; KeyPath = '' }
        }
        Integrations = @{
            Sepidar = @{ Enabled = $false; Mode = 'Simulated' }
            Issabel = @{ Enabled = $false; Mode = 'Simulated' }
        }
    }
    ($isolated | ConvertTo-Json -Depth 8) | Set-Content -Path $prodPath -Encoding UTF8

    $basePath = Join-Path $apiDir 'appsettings.json'
    if (Test-Path $basePath) {
        $base = Get-Content $basePath -Raw | ConvertFrom-Json
        if (-not $base.ConnectionStrings) {
            $base | Add-Member -NotePropertyName ConnectionStrings -NotePropertyValue ([pscustomobject]@{})
        }
        $base.ConnectionStrings | Add-Member -NotePropertyName CrmDatabase -NotePropertyValue $crmCs -Force
        $base.ConnectionStrings | Add-Member -NotePropertyName SepidarDatabase -NotePropertyValue '' -Force
        if (-not $base.Telephony) {
            $base | Add-Member -NotePropertyName Telephony -NotePropertyValue ([pscustomobject]@{}) -Force
        }
        $base.Telephony | Add-Member -NotePropertyName Provider -NotePropertyValue 'Simulated' -Force
        if (-not $base.Telephony.Sftp) {
            $base.Telephony | Add-Member -NotePropertyName Sftp -NotePropertyValue ([pscustomobject]@{ Host = '' }) -Force
        } else {
            $base.Telephony.Sftp | Add-Member -NotePropertyName Host -NotePropertyValue '' -Force
        }
        ($base | ConvertTo-Json -Depth 12) | Set-Content -Path $basePath -Encoding UTF8
    }
}

function Set-StagingPhysicalPaths([string]$webPath, [string]$apiPath) {
    Import-Module WebAdministration -ErrorAction Stop
    Assert-NotProductionPath 'webPath' $webPath
    Assert-NotProductionPath 'apiPath' $apiPath
    if (-not (Test-PathIsUnderStagingRoot $webPath)) { throw "STOP: webPath not under StagingRoot: $webPath" }
    if (-not (Test-PathIsUnderStagingRoot $apiPath)) { throw "STOP: apiPath not under StagingRoot: $apiPath" }
    Set-ItemProperty "IIS:\Sites\$StagingSiteName" -Name physicalPath -Value $webPath
    Set-ItemProperty "IIS:\Sites\$StagingSiteName\api" -Name physicalPath -Value $apiPath
}

function Expand-ZipTo([string]$zip, [string]$dest) {
    if (Test-Path $dest) { throw "STOP: destination already exists: $dest" }
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Expand-Archive -Path $zip -DestinationPath $dest -Force
}

function Copy-RequiredWebConfig([string]$sourceDir, [string]$destDir, [string]$label, [object]$bag) {
    $src = Join-Path $sourceDir 'web.config'
    if (-not (Test-Path $src)) {
        Write-Check $bag ("PreserveWebConfig_" + $label) $false @{ Source = $src; Note = 'Required staging web.config missing — refusing package web.config (may force HTTPS).' }
        throw "STOP: missing required staging web.config for $label at $src"
    }
    # Reject package redirect-heavy configs by always overwriting after extract
    Copy-Item $src (Join-Path $destDir 'web.config') -Force
    Write-Check $bag ("PreserveWebConfig_" + $label) $true @{ From = $src; To = (Join-Path $destDir 'web.config') }
}

$results = [ordered]@{
    Gate                   = 'Pending'
    Mode                   = $Mode
    StartedAtUtc           = (Get-Date).ToUniversalTime().ToString('o')
    HostName               = $env:COMPUTERNAME
    PackageDir             = $PackageDir
    EvidenceDir            = $EvidenceDir
    StagingSiteName        = $StagingSiteName
    StagingApiBase         = $StagingApiBase
    StagingDb              = $StagingDb
    StagingRoot            = $StagingRoot
    ProductionSiteTouched  = $false
    ProductionMutated      = $false
    deploy_allowed_package = $false
    deploy_allowed_rc3     = $false
    ExecuteEligible        = $false
    Checks                 = @()
    Notes                  = @()
}

Write-Host "=== dashboard-exec-v1 runner Mode=$Mode ===" -ForegroundColor Cyan
Write-Host "PackageDir=$PackageDir"
Write-Host "Evidence -> $evidencePath"
Write-Host "Production mutate: FORBIDDEN"

# --- Package identity (explicit path) ---
if (-not (Test-Path $PackageDir)) {
    Write-Check $results 'PackageDirExists' $false @{ Path = $PackageDir }
    $results.Notes += "PackageDir missing. On CRM copy ZIPs+manifest to D:\ParkPlaceCRM\packages\dashboard-exec-v1\latest"
    $results.Gate = 'Pending'
    $results.FinishedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    ($results | ConvertTo-Json -Depth 10) | Set-Content $evidencePath -Encoding UTF8
    Write-Host "Gate=Pending ExecuteEligible=False"
    exit 2
}
Write-Check $results 'PackageDirExists' $true @{ Path = (Resolve-Path $PackageDir).Path }
$results.PackageDir = (Resolve-Path $PackageDir).Path

$manifestPath = Join-Path $PackageDir 'manifest.json'
if (-not (Test-Path $manifestPath)) { throw "STOP: manifest missing: $manifestPath" }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$results.PackageManifest = $manifest
Write-Check $results 'ManifestDeployAllowedFalse' ($manifest.deploy_allowed -eq $false) @{ deploy_allowed = $manifest.deploy_allowed }
Write-Check $results 'ManifestIndependentOfRc3' ($manifest.independentOf -eq 'v1.0.24-rc3') @{ independentOf = $manifest.independentOf }
Write-Check $results 'ManifestPackageId' ($manifest.packageId -eq 'dashboard-exec-v1') @{ packageId = $manifest.packageId }

$packageOk = $true
foreach ($f in @($manifest.files)) {
    $zipPath = Join-Path $PackageDir $f.relativePath
    $exists = Test-Path $zipPath
    $actualSha = if ($exists) { Get-Sha256 $zipPath } else { $null }
    $actualLen = if ($exists) { (Get-Item $zipPath).Length } else { -1 }
    $ok = $exists -and ($actualSha -eq $f.sha256) -and ([int64]$actualLen -eq [int64]$f.bytes)
    if (-not $ok) { $packageOk = $false }
    Write-Check $results ("ZipMatch_" + ($f.relativePath -replace '[^A-Za-z0-9]', '_')) $ok @{
        Path = $zipPath; ExpectedSha = $f.sha256; ActualSha = $actualSha; ExpectedBytes = $f.bytes; ActualBytes = $actualLen
    }
}
$manifestOk = (@($results.Checks) | Where-Object {
        $_.Name -in @('ManifestDeployAllowedFalse', 'ManifestIndependentOfRc3', 'ManifestPackageId') -and -not $_.Passed
    }).Count -eq 0
$packageVerified = $packageOk -and $manifestOk
Write-Check $results 'PackageVerified' $packageVerified @{ Note = 'ZIP SHA256+bytes + manifest identity only' }

# --- Production fingerprint (read-only) ---
$prodDll = Join-Path $ProductionApiPath 'ParkPalace.Api.dll'
$prodBefore = $null
$prodFpOk = $false
if (Test-Path $prodDll) {
    $prodBefore = Get-Sha256 $prodDll
    $results.ProductionApiDllSha256Before = $prodBefore
    $prodFpOk = $true
    Write-Check $results 'ProductionApiDllFingerprintReadable' $true @{ Sha256 = $prodBefore; Path = $prodDll }
} else {
    $results.Notes += "Production Api.dll not visible ($prodDll)."
    Write-Check $results 'ProductionApiDllFingerprintReadable' $false @{ Path = $prodDll }
}

# --- Staging identity gates ---
$iisOk = $false
$dbOk = $false
$healthOk = $false
$identityOk = $false
$snap = $null
$priorIsolationOk = $false

try {
    $snap = Get-StagingIisSnapshot
    $results.IisSnapshot = $snap

    $nameOk = ($snap.SiteName -eq $StagingSiteName)
    Write-Check $results 'StagingSiteNameExact' $nameOk @{ SiteName = $snap.SiteName; Expected = $StagingSiteName }

    $apiAppOk = ($snap.HasApiApp -and $snap.ApiAppPath -eq '/api')
    Write-Check $results 'StagingApiApplicationExact' $apiAppOk @{ ApiAppPath = $snap.ApiAppPath }

    $bindPattern = ':' + $ExpectedBindingPort + ':'
    $bindOk = ($snap.Bindings -join ';') -match [regex]::Escape($bindPattern)
    Write-Check $results 'StagingBindingPortExact' $bindOk @{
        Bindings = $snap.Bindings; ExpectedPort = $ExpectedBindingPort
    }

    $poolOk = -not [string]::IsNullOrWhiteSpace($snap.AppPool) -and ($snap.AppPool -ne $ForbiddenProdSiteName)
    Write-Check $results 'StagingAppPoolPresent' $poolOk @{ AppPool = $snap.AppPool; ApiAppPool = $snap.ApiAppPool }

    Assert-NotProductionPath 'IIS web' $snap.WebPhysicalPath
    Assert-NotProductionPath 'IIS api' $snap.ApiPhysicalPath
    $webUnder = Test-PathIsUnderStagingRoot $snap.WebPhysicalPath
    $apiUnder = Test-PathIsUnderStagingRoot $snap.ApiPhysicalPath
    Write-Check $results 'StagingPathsUnderStagingRoot' ($webUnder -and $apiUnder) @{
        Web = $snap.WebPhysicalPath; Api = $snap.ApiPhysicalPath; StagingRoot = $StagingRoot
    }

    # Exact baseline path: original RC3 folder OR a dedicated isolated clone (never sepidarsearch).
    $rc3BaselineOk = ($snap.ApiPhysicalPath -ieq $ExpectedApiBaselinePath)
    Write-Check $results 'ExpectedApiBaselineExact' $rc3BaselineOk @{
        Actual = $snap.ApiPhysicalPath; Expected = $ExpectedApiBaselinePath
    }
    if ($snap.ApiPhysicalPath -match 'sepidarsearch') {
        Write-Check $results 'NotSepidarSearchStaging' $false @{
            Api = $snap.ApiPhysicalPath; Note = 'dashboard-exec must not target sepidarsearch staging'
        }
        $rc3BaselineOk = $false
    } else {
        Write-Check $results 'NotSepidarSearchStaging' $true @{ Api = $snap.ApiPhysicalPath }
    }
    $currentApiExists = Test-Path $snap.ApiPhysicalPath
    $currentWebExists = Test-Path $snap.WebPhysicalPath
    Write-Check $results 'CurrentStagingApiPathExists' $currentApiExists @{ Path = $snap.ApiPhysicalPath; Rc3HintMatch = ($snap.ApiPhysicalPath -match [regex]::Escape($ExpectedRc3ApiPathHint)) }
    Write-Check $results 'CurrentStagingWebPathExists' $currentWebExists @{ Path = $snap.WebPhysicalPath }

    $apiWc = Join-Path $snap.ApiPhysicalPath 'web.config'
    $webWc = Join-Path $snap.WebPhysicalPath 'web.config'
    Write-Check $results 'CurrentApiWebConfigExists' (Test-Path $apiWc) @{ Path = $apiWc }
    Write-Check $results 'CurrentWebWebConfigExists' (Test-Path $webWc) @{ Path = $webWc }

    $iso = Read-IsolatedFlags $snap.ApiPhysicalPath
    $priorIsolationOk = [bool]$iso.Ok
    Write-Check $results 'CurrentStagingIsolationReadable' $priorIsolationOk $iso

    $iisOk = $nameOk -and $apiAppOk -and $bindOk -and $poolOk -and $webUnder -and $apiUnder -and $rc3BaselineOk -and $currentApiExists -and $currentWebExists -and (Test-Path $apiWc) -and (Test-Path $webWc)
    $identityOk = $iisOk
    Write-Check $results 'StagingIisIdentityBundle' $identityOk @{ Note = 'site+/api+pool+bindings+paths+web.config' }
}
catch {
    $results.Notes += "IIS discovery unavailable: $($_.Exception.Message)"
    Write-Check $results 'StagingIisDiscovery' $false @{ Error = $_.Exception.Message }
}

try {
    $dbId = (sqlcmd -S $StagingSql -E -d $StagingDb -h -1 -W -Q "SET NOCOUNT ON; SELECT DB_NAME();" 2>&1 | Select-Object -First 1).ToString().Trim()
    $dbOk = ($dbId -eq $StagingDb)
    Write-Check $results 'StagingDatabaseIdentity' $dbOk @{ Connected = $dbId; Expected = $StagingDb }
}
catch {
    $results.Notes += "sqlcmd staging DB check unavailable: $($_.Exception.Message)"
    Write-Check $results 'StagingDatabaseIdentity' $false @{ Error = $_.Exception.Message }
}

$healthNow = Invoke-ApiGet "$StagingApiBase/api/health"
$results.PreInstallHealth = @{ Status = $healthNow.Status; Body = $healthNow.Body; Error = $healthNow.Error }
$healthOk = ($healthNow.Status -eq 200) -and ($healthNow.Body -match 'Healthy')
Write-Check $results 'StagingHealthHealthy_PreInstall' $healthOk @{
    Url = "$StagingApiBase/api/health"; Status = $healthNow.Status
    Note = 'Pre-install health of CURRENT staging (likely RC3). Not a dashboard-exec install pass.'
}

$executeEligible = [bool]($packageVerified -and $identityOk -and $priorIsolationOk -and $dbOk -and $healthOk -and $prodFpOk)
$results.ExecuteEligible = $executeEligible

if ($Mode -eq 'PreviewOnly') {
    if ($executeEligible) {
        $results.Gate = 'StagingPreviewPassed'
        $results.Notes += 'All CRM staging identity gates green. Execute may be run on THIS host with the same PackageDir.'
    } elseif ($packageVerified) {
        $results.Gate = 'PackageVerifiedOnly'
        $results.Notes += 'Package ZIP/manifest verified; exact RC3 baseline, isolation, IIS, SQL, Health or Production fingerprint is not green. ExecuteEligible=False.'
    } else {
        $results.Gate = 'Pending'
        $results.Notes += 'Package verification failed.'
    }

    $results.FinishedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    ($results | ConvertTo-Json -Depth 10) | Set-Content -Path $evidencePath -Encoding UTF8
    Write-Host ""
    Write-Host "Gate=$($results.Gate)" -ForegroundColor Yellow
    Write-Host "ExecuteEligible=$($results.ExecuteEligible)" -ForegroundColor Yellow
    Write-Host "Evidence: $evidencePath"
    if ($results.Gate -eq 'StagingPreviewPassed') { exit 0 }
    if ($results.Gate -eq 'PackageVerifiedOnly') { exit 0 }
    exit 2
}

# ===================== Execute =====================
if (-not $executeEligible) {
    throw "STOP: Execute refused. Require Gate path StagingPreviewPassed / ExecuteEligible=True on CRM. Current ExecuteEligible=$executeEligible"
}
if (-not $snap) { throw 'STOP: Execute refused — no IIS snapshot.' }

$priorWeb = $snap.WebPhysicalPath
$priorApi = $snap.ApiPhysicalPath
$results.RollbackTargets = @{ PriorWeb = $priorWeb; PriorApi = $priorApi; FallbackRc3Api = $FallbackRc3ApiPath }
Assert-NotProductionPath 'prior web' $priorWeb
Assert-NotProductionPath 'prior api' $priorApi

$apiZip = Join-Path $PackageDir 'ParkPalace_Api_dashboard-exec-v1.zip'
$webZip = Join-Path $PackageDir 'ParkPalace_Web_dashboard-exec-v1.zip'
$newApi = Join-Path $StagingRoot ("dashboardexec_api_" + $stamp)
$newWeb = Join-Path $StagingRoot ("dashboardexec_web_" + $stamp)
Assert-NotProductionPath 'new api' $newApi
Assert-NotProductionPath 'new web' $newWeb

$crmCs = "Server=$StagingSql;Database=$StagingDb;Integrated Security=True;TrustServerCertificate=True;Application Name=ParkPalaceCRM_StagingRehearsal;"
$rolledBack = $false

try {
    Write-Host "Extracting package to unique staging folders under $StagingRoot ..." -ForegroundColor Cyan
    Expand-ZipTo $apiZip $newApi
    Expand-ZipTo $webZip $newWeb
    $results.NewApiPath = $newApi
    $results.NewWebPath = $newWeb

    # Always preserve CURRENT staging web.config for API and Web (package may redirect HTTPS).
    Copy-RequiredWebConfig -sourceDir $priorApi -destDir $newApi -label 'Api' -bag $results
    Copy-RequiredWebConfig -sourceDir $priorWeb -destDir $newWeb -label 'Web' -bag $results

    Write-Host "Applying / validating isolated staging appsettings..." -ForegroundColor Cyan
    if ($priorIsolationOk) {
        Copy-Item (Join-Path $priorApi 'appsettings.Production.json') (Join-Path $newApi 'appsettings.Production.json') -Force
        $priorBase = Join-Path $priorApi 'appsettings.json'
        if (Test-Path $priorBase) { Copy-Item $priorBase (Join-Path $newApi 'appsettings.json') -Force }
        Write-Check $results 'CopiedPriorIsolatedAppsettings' $true @{ From = $priorApi }
    } else {
        Write-IsolatedStagingAppsettings -apiDir $newApi -crmCs $crmCs
        Write-Check $results 'WroteFreshIsolatedAppsettings' $true @{ Reason = 'prior isolation not valid' }
    }
    $isoAfter = Read-IsolatedFlags $newApi
    if (-not $isoAfter.Ok) {
        Write-IsolatedStagingAppsettings -apiDir $newApi -crmCs $crmCs
        $isoAfter = Read-IsolatedFlags $newApi
    }
    if (-not $isoAfter.Ok) { throw "STOP: isolated appsettings validation failed after write." }
    Write-Check $results 'IsolatedAppsettingsValidatedPreStart' $true $isoAfter

    Write-Host "Retargeting IIS staging site ONLY ($StagingSiteName)..." -ForegroundColor Cyan
    Set-StagingPhysicalPaths -webPath $newWeb -apiPath $newApi
    if ($snap.AppPool) {
        Restart-WebAppPool -Name $snap.AppPool
        Start-Sleep -Seconds 4
    }

    $health = Invoke-ApiGet "$StagingApiBase/api/health"
    $healthy = ($health.Status -eq 200) -and ($health.Body -match 'Healthy')
    Write-Check $results 'PostInstall_HealthHealthy' $healthy @{ Status = $health.Status; Body = $health.Body }

    $dash = Invoke-ApiGet "$StagingApiBase/api/v1/reports/executive-dashboard" @{ 'X-Active-Role' = 'Admin' }
    $dashOk = $dash.Status -eq 200
    $hasLedger = $false
    $ed06Blocked = $false
    $ed06AmountNull = $false
    if ($dashOk -and $dash.Body) {
        $j = $dash.Body | ConvertFrom-Json
        $hasLedger = [bool]$j.viewer.hasPartyLedgerView
        $ar = @($j.cards) | Where-Object { $_.contractId -eq 'ED-06' } | Select-Object -First 1
        if ($null -ne $ar) {
            $ed06AmountNull = ($null -eq $ar.amount)
            $st = $ar.status
            $ed06Blocked = ($st -eq 3) -or ("$st" -eq 'Blocked')
        }
    }
    Write-Check $results 'PostInstall_ExecutiveDashboard_Http200' $dashOk @{ Status = $dash.Status }
    Write-Check $results 'PostInstall_Admin_NoPartyLedgerView' ($dashOk -and -not $hasLedger) @{ HasPartyLedgerView = $hasLedger }
    Write-Check $results 'PostInstall_Admin_ED06_BlockedNoAmount' ($dashOk -and $ed06Blocked -and $ed06AmountNull) @{
        Ed06Blocked = $ed06Blocked; AmountNull = $ed06AmountNull
    }

    # Header cannot invent an unassigned role. Do NOT use X-Active-Role:Finance here:
    # CRM\Administrator (and similar ops accounts) may legitimately hold Finance+PartyLedgerView,
    # which would correctly return 200 and is not a spoof.
    $spoof = Invoke-ApiGet "$StagingApiBase/api/v1/finance/dashboard" @{ 'X-Active-Role' = 'FakeRoleNotAssigned' }
    Write-Check $results 'PostInstall_UnassignedRoleHeader_Finance403' ($spoof.Status -eq 403) @{
        Status = $spoof.Status; Header = 'FakeRoleNotAssigned'
        Note = 'Falls back to assigned role; Admin without PartyLedgerView must stay 403'
    }

    $fin = Invoke-ApiGet "$StagingApiBase/api/v1/finance/dashboard" @{ 'X-Active-Role' = 'Admin' }
    Write-Check $results 'PostInstall_AdminFinanceDashboard_403' ($fin.Status -eq 403) @{ Status = $fin.Status }

    $prodAfter = if (Test-Path $prodDll) { Get-Sha256 $prodDll } else { $null }
    $results.ProductionApiDllSha256After = $prodAfter
    $prodUnchanged = ($null -ne $prodBefore) -and ($prodBefore -eq $prodAfter)
    Write-Check $results 'ProductionApiDllUnchanged' $prodUnchanged @{ Before = $prodBefore; After = $prodAfter }
    $results.ProductionSiteTouched = $false
    $results.ProductionMutated = -not $prodUnchanged

    $failed = @($results.Checks | Where-Object { -not $_.Passed -and ($_.Name -like 'PostInstall_*' -or $_.Name -like 'PreserveWebConfig_*' -or $_.Name -eq 'IsolatedAppsettingsValidatedPreStart' -or $_.Name -eq 'ProductionApiDllUnchanged') })
    if ($failed.Count -gt 0) {
        throw "Post-install / preserve gates failed: $(($failed | ForEach-Object { $_.Name }) -join ', ')"
    }

    $results.Gate = 'StageDashboardExecV1ExecutePassed'
}
catch {
    $results.Notes += "Execute failure: $($_.Exception.Message)"
    Write-Host "EXECUTE FAILED — restoring prior staging IIS paths..." -ForegroundColor Red
    try {
        $rollbackApi = if ($priorApi -and (Test-Path $priorApi)) { $priorApi } elseif (Test-Path $FallbackRc3ApiPath) { $FallbackRc3ApiPath } else { $null }
        $rollbackWeb = if ($priorWeb -and (Test-Path $priorWeb)) { $priorWeb } else { $null }
        if ($rollbackApi -and $rollbackWeb) {
            Set-StagingPhysicalPaths -webPath $rollbackWeb -apiPath $rollbackApi
            if ($snap.AppPool) { Restart-WebAppPool -Name $snap.AppPool }
            $rolledBack = $true
            Write-Check $results 'RollbackToPriorStagingPaths' $true @{ Web = $rollbackWeb; Api = $rollbackApi }
        } else {
            Write-Check $results 'RollbackToPriorStagingPaths' $false @{ Note = 'Missing prior/fallback paths' }
        }
    } catch {
        Write-Check $results 'RollbackToPriorStagingPaths' $false @{ Error = $_.Exception.Message }
    }
    $results.Gate = 'Pending'
    $results.RolledBack = $rolledBack
}

$results.FinishedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
($results | ConvertTo-Json -Depth 10) | Set-Content -Path $evidencePath -Encoding UTF8
Write-Host "Gate=$($results.Gate) ExecuteEligible=$($results.ExecuteEligible)" -ForegroundColor $(if ($results.Gate -eq 'StageDashboardExecV1ExecutePassed') { 'Green' } else { 'Yellow' })
Write-Host "Evidence: $evidencePath"
if ($results.Gate -eq 'StageDashboardExecV1ExecutePassed') { exit 0 } else { exit 3 }
