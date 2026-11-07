#Requires -Version 5.1
<#
.SYNOPSIS
  Provision a NEW isolated IIS staging site for dashboard-exec-v1.
  Does NOT retarget ParkPalaceCRM-Stage-v1024 / sepidarsearch. Does NOT touch Production.

.DESCRIPTION
  Clones RC3 API + current Stage-v1024 web into a dedicated StagingRoot, writes isolated
  appsettings (staging DB only, empty Sepidar, Telephony=Simulated), creates AppPool + site
  on a free HTTP port (default 51025), and leaves Stage-v1024 untouched.

  Run ON the CRM host (or via WinRM). After success, run PreviewOnly against the new site
  with ExpectedApiBaselinePath = the cloned API path. Execute is NOT performed here.
#>
[CmdletBinding()]
param(
    [string]$NewSiteName = 'ParkPalaceCRM-Stage-DashboardExec',
    [string]$NewAppPoolName = 'ParkPalaceCRM-Stage-DashboardExec',
    [int]$HttpPort = 51025,
    [string]$StagingRoot = 'D:\ParkPlaceCRM\staging\dashboard_exec_v1',
    [string]$SourceRc3ApiPath = 'D:\ParkPlaceCRM\staging\v1024_c98981ae\rc3_api_20261106_143114_25779b88',
    [string]$SourceWebPath = 'D:\ParkPlaceCRM\staging\v1024_c98981ae\web',
    [string]$StagingSql = 'localhost',
    [string]$StagingDb = 'ParkPalaceCRM_Stage_20261106_123740_c98981ae',
    [string]$ForbiddenSiteName = 'ParkPalaceCRM',
    [string]$LeaveUntouchedSiteName = 'ParkPalaceCRM-Stage-v1024',
    [string]$EvidenceDir = 'D:\ParkPlaceCRM\docs\runs',
    [string]$StateFile = 'D:\ParkPlaceCRM\staging\dashboard_exec_v1\site_state.json'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null
New-Item -ItemType Directory -Force -Path $StagingRoot | Out-Null
$evidencePath = Join-Path $EvidenceDir "provision_dashboard_exec_isolated_stage_$stamp.json"

$results = [ordered]@{
    Gate                  = 'Pending'
    StartedAtUtc          = (Get-Date).ToUniversalTime().ToString('o')
    HostName              = $env:COMPUTERNAME
    NewSiteName           = $NewSiteName
    NewAppPoolName        = $NewAppPoolName
    HttpPort              = $HttpPort
    StagingRoot           = $StagingRoot
    SourceRc3ApiPath      = $SourceRc3ApiPath
    SourceWebPath         = $SourceWebPath
    StagingDb             = $StagingDb
    LeaveUntouchedSite    = $LeaveUntouchedSiteName
    ProductionTouched     = $false
    StageV1024Retargeted  = $false
    Checks                = @()
    Notes                 = @()
}

function Write-Check([object]$bag, [string]$name, [bool]$passed, [hashtable]$extra = @{}) {
    $row = @{ Name = $name; Passed = $passed }
    foreach ($k in $extra.Keys) { $row[$k] = $extra[$k] }
    $bag.Checks += $row
    $color = if ($passed) { 'Green' } else { 'Yellow' }
    Write-Host ("[{0}] {1}" -f ($(if ($passed) { 'PASS' } else { 'FAIL' }), $name)) -ForegroundColor $color
}

function Assert-NotForbiddenPath([string]$label, [string]$path) {
    $full = [System.IO.Path]::GetFullPath($path).TrimEnd('\')
    foreach ($bad in @('D:\ParkPlaceCRM\api', 'D:\ParkPlaceCRM\web')) {
        $b = [System.IO.Path]::GetFullPath($bad).TrimEnd('\')
        if ($full -ieq $b -or $full.StartsWith($b + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "STOP: $label resolves under Production path '$path'."
        }
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

function Read-IsolatedFlags([string]$apiDir, [string]$stagingDb) {
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
    $dbOk = $cs -match [regex]::Escape("Database=$stagingDb")
    $sepOk = [string]::IsNullOrWhiteSpace($sep)
    $telOk = $prov -eq 'Simulated'
    $sftpOk = [string]::IsNullOrWhiteSpace($sftp)
    return @{
        Ok           = ($dbOk -and $sepOk -and $telOk -and $sftpOk)
        CrmCs        = $cs
        DbOk         = $dbOk
        SepidarEmpty = $sepOk
        TelephonySim = $telOk
        SftpEmpty    = $sftpOk
        Path         = $prodPath
    }
}

Write-Host "=== Provision isolated dashboard-exec staging site ===" -ForegroundColor Cyan
Write-Host "NewSite=$NewSiteName Port=$HttpPort Root=$StagingRoot"
Write-Host "Will NOT touch $LeaveUntouchedSiteName or $ForbiddenSiteName"

try {
    if ($NewSiteName -eq $ForbiddenSiteName -or $NewAppPoolName -eq $ForbiddenSiteName) {
        throw 'STOP: refused Production site/pool name.'
    }
    if ($NewSiteName -eq $LeaveUntouchedSiteName) {
        throw 'STOP: refused to reuse Stage-v1024 site name.'
    }

    Import-Module WebAdministration -ErrorAction Stop

    $leaveSite = Get-Website -Name $LeaveUntouchedSiteName -ErrorAction SilentlyContinue
    if (-not $leaveSite) { throw "STOP: reference site '$LeaveUntouchedSiteName' missing — aborting (unexpected CRM layout)." }
    $leaveBefore = [pscustomobject]@{
        Web = $leaveSite.physicalPath
        Api = (Get-WebApplication -Site $LeaveUntouchedSiteName | Where-Object { $_.path -eq '/api' } | Select-Object -First 1).physicalPath
        Bindings = @($leaveSite.bindings.Collection | ForEach-Object { $_.bindingInformation })
    }
    $results.LeaveUntouchedBefore = $leaveBefore
    Write-Check $results 'LeaveUntouchedSitePresent' $true @{
        Web = $leaveBefore.Web; Api = $leaveBefore.Api; Bindings = ($leaveBefore.Bindings -join ';')
    }

    $prodSite = Get-Website -Name $ForbiddenSiteName -ErrorAction SilentlyContinue
    if ($prodSite) {
        $results.ProductionFingerprint = @{
            Web = $prodSite.physicalPath
            Api = (Get-WebApplication -Site $ForbiddenSiteName | Where-Object { $_.path -eq '/api' } | Select-Object -First 1).physicalPath
        }
    }

    if (-not (Test-Path $SourceRc3ApiPath)) { throw "STOP: RC3 API source missing: $SourceRc3ApiPath" }
    if (-not (Test-Path $SourceWebPath)) { throw "STOP: web source missing: $SourceWebPath" }
    if (-not (Test-Path (Join-Path $SourceRc3ApiPath 'web.config'))) { throw "STOP: RC3 API web.config missing" }
    if (-not (Test-Path (Join-Path $SourceWebPath 'web.config'))) { throw "STOP: web web.config missing" }
    Write-Check $results 'SourcePathsExist' $true @{ Api = $SourceRc3ApiPath; Web = $SourceWebPath }

    # Prefer existing healthy clone if state file already points to valid paths under StagingRoot.
    $apiDest = Join-Path $StagingRoot ("rc3_clone_api_" + $stamp)
    $webDest = Join-Path $StagingRoot ("rc3_clone_web_" + $stamp)
    Assert-NotForbiddenPath 'apiDest' $apiDest
    Assert-NotForbiddenPath 'webDest' $webDest

    Write-Host "Robocopy RC3 API -> $apiDest" -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $apiDest | Out-Null
    & robocopy $SourceRc3ApiPath $apiDest /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "STOP: robocopy API failed exit=$LASTEXITCODE" }

    Write-Host "Robocopy Web -> $webDest" -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $webDest | Out-Null
    & robocopy $SourceWebPath $webDest /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "STOP: robocopy Web failed exit=$LASTEXITCODE" }
    Write-Check $results 'RobocopyCloneOk' $true @{ Api = $apiDest; Web = $webDest }

    $crmCs = "Server=$StagingSql;Database=$StagingDb;Integrated Security=True;TrustServerCertificate=True;Application Name=ParkPalaceCRM_DashboardExecIsolated;"
    Write-IsolatedStagingAppsettings -apiDir $apiDest -crmCs $crmCs
    $iso = Read-IsolatedFlags -apiDir $apiDest -stagingDb $StagingDb
    if (-not $iso.Ok) { throw "STOP: isolation write failed: $($iso | ConvertTo-Json -Compress)" }
    Write-Check $results 'IsolatedAppsettingsOk' $true $iso

    # Port conflict check (do not steal 51024 from Stage-v1024).
    if ($HttpPort -eq 51024) { throw 'STOP: port 51024 reserved for Stage-v1024.' }
    $portInUse = Get-Website | ForEach-Object {
        $_.bindings.Collection | ForEach-Object {
            $parts = $_.bindingInformation -split ':'
            if ($parts.Count -ge 2 -and $parts[1] -eq "$HttpPort") { $_ }
        }
    }
    if ($portInUse) { throw "STOP: HTTP port $HttpPort already bound." }
    Write-Check $results 'HttpPortAvailable' $true @{ Port = $HttpPort }

    $poolPath = "IIS:\AppPools\$NewAppPoolName"
    if (-not (Test-Path $poolPath)) {
        New-WebAppPool -Name $NewAppPoolName | Out-Null
        Set-ItemProperty $poolPath -Name managedRuntimeVersion -Value ''
        Set-ItemProperty $poolPath -Name startMode -Value 'AlwaysRunning'
        Write-Check $results 'AppPoolCreated' $true @{ Name = $NewAppPoolName }
    } else {
        Write-Check $results 'AppPoolExists' $true @{ Name = $NewAppPoolName }
    }

    $existing = Get-Website -Name $NewSiteName -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "Site already exists — retargeting physical paths under new StagingRoot only..." -ForegroundColor Yellow
        Set-ItemProperty "IIS:\Sites\$NewSiteName" -Name physicalPath -Value $webDest
        $apiApp = Get-WebApplication -Site $NewSiteName | Where-Object { $_.path -eq '/api' } | Select-Object -First 1
        if (-not $apiApp) {
            New-WebApplication -Site $NewSiteName -Name 'api' -PhysicalPath $apiDest -ApplicationPool $NewAppPoolName | Out-Null
        } else {
            Set-ItemProperty "IIS:\Sites\$NewSiteName\api" -Name physicalPath -Value $apiDest
            Set-ItemProperty "IIS:\Sites\$NewSiteName\api" -Name applicationPool -Value $NewAppPoolName
        }
        Set-ItemProperty "IIS:\Sites\$NewSiteName" -Name applicationPool -Value $NewAppPoolName
        Write-Check $results 'ExistingSiteRetargeted' $true @{ Site = $NewSiteName }
    } else {
        New-Website -Name $NewSiteName -Port $HttpPort -PhysicalPath $webDest -ApplicationPool $NewAppPoolName | Out-Null
        New-WebApplication -Site $NewSiteName -Name 'api' -PhysicalPath $apiDest -ApplicationPool $NewAppPoolName | Out-Null
        Write-Check $results 'SiteCreated' $true @{ Site = $NewSiteName; Port = $HttpPort }
    }

    Start-Website -Name $NewSiteName -ErrorAction SilentlyContinue
    Restart-WebAppPool -Name $NewAppPoolName
    Start-Sleep -Seconds 5

    $site = Get-Website -Name $NewSiteName
    $apiApp = Get-WebApplication -Site $NewSiteName | Where-Object { $_.path -eq '/api' } | Select-Object -First 1
    $bindings = @($site.bindings.Collection | ForEach-Object { $_.bindingInformation })
    $bindOk = ($bindings -join ';') -match (':' + $HttpPort + ':')
    Write-Check $results 'NewSiteBindingExact' $bindOk @{ Bindings = $bindings; ExpectedPort = $HttpPort }
    Write-Check $results 'NewSitePathsExact' (
        ($site.physicalPath -ieq $webDest) -and ($apiApp.physicalPath -ieq $apiDest)
    ) @{ Web = $site.physicalPath; Api = $apiApp.physicalPath }

    # Confirm Stage-v1024 unchanged
    $leaveAfterSite = Get-Website -Name $LeaveUntouchedSiteName
    $leaveAfterApi = (Get-WebApplication -Site $LeaveUntouchedSiteName | Where-Object { $_.path -eq '/api' } | Select-Object -First 1).physicalPath
    $leaveUnchanged = ($leaveAfterSite.physicalPath -ieq $leaveBefore.Web) -and ($leaveAfterApi -ieq $leaveBefore.Api)
    Write-Check $results 'StageV1024Untouched' $leaveUnchanged @{
        WebBefore = $leaveBefore.Web; WebAfter = $leaveAfterSite.physicalPath
        ApiBefore = $leaveBefore.Api; ApiAfter = $leaveAfterApi
    }
    $results.StageV1024Retargeted = -not $leaveUnchanged
    if (-not $leaveUnchanged) { throw 'STOP: Stage-v1024 paths changed unexpectedly — aborting.' }

    $healthUrl = "http://127.0.0.1:$HttpPort/api/health"
    try {
        $resp = Invoke-WebRequest -Uri $healthUrl -UseDefaultCredentials -UseBasicParsing -TimeoutSec 45
        $body = $resp.Content
        $status = [int]$resp.StatusCode
    } catch {
        $status = 0
        $body = $_.Exception.Message
        if ($_.Exception.Response) {
            try { $status = [int]$_.Exception.Response.StatusCode } catch { }
        }
    }
    $healthOk = ($status -eq 200) -and ($body -match 'Healthy')
    $sepidarLive = ($body -match 'Active \(Read-Only\)') -or ($body -match '"Sepidar".*"Active"')
    Write-Check $results 'NewSiteHealthHealthy' $healthOk @{ Url = $healthUrl; Status = $status; Body = $body }
    Write-Check $results 'NewSiteHealthNotLiveSepidar' (-not $sepidarLive) @{ Note = 'Isolation requires no live Sepidar in health' }

    $dbId = (sqlcmd -S $StagingSql -E -d $StagingDb -h -1 -W -Q "SET NOCOUNT ON; SELECT DB_NAME();" 2>&1 | Select-Object -First 1).ToString().Trim()
    Write-Check $results 'StagingDatabaseIdentity' ($dbId -eq $StagingDb) @{ Connected = $dbId; Expected = $StagingDb }

    $failed = @($results.Checks | Where-Object { -not $_.Passed })
    if ($failed.Count -gt 0) {
        throw "Provision checks failed: $(($failed | ForEach-Object { $_.Name }) -join ', ')"
    }

    $state = [ordered]@{
        ProvisionedAtUtc        = (Get-Date).ToUniversalTime().ToString('o')
        SiteName                = $NewSiteName
        AppPool                 = $NewAppPoolName
        HttpPort                = $HttpPort
        StagingApiBase          = "http://127.0.0.1:$HttpPort"
        StagingRoot             = $StagingRoot
        ApiPhysicalPath         = $apiDest
        WebPhysicalPath         = $webDest
        ExpectedApiBaselinePath = $apiDest
        StagingDb               = $StagingDb
        SourceRc3ApiPath        = $SourceRc3ApiPath
        LeaveUntouchedSiteName  = $LeaveUntouchedSiteName
    }
    ($state | ConvertTo-Json -Depth 6) | Set-Content -Path $StateFile -Encoding UTF8
    $results.StateFile = $StateFile
    $results.State = $state
    $results.Gate = 'IsolatedDashboardStageProvisioned'
    $results.Notes += 'Site ready for PreviewOnly with ExpectedApiBaselinePath from state file. Execute not run.'
}
catch {
    $results.Notes += $_.Exception.Message
    $results.Gate = 'Pending'
    Write-Host "PROVISION FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

$results.FinishedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
($results | ConvertTo-Json -Depth 10) | Set-Content -Path $evidencePath -Encoding UTF8
Write-Host "Gate=$($results.Gate)" -ForegroundColor $(if ($results.Gate -eq 'IsolatedDashboardStageProvisioned') { 'Green' } else { 'Yellow' })
Write-Host "Evidence: $evidencePath"
if ($results.Gate -eq 'IsolatedDashboardStageProvisioned') { exit 0 } else { exit 2 }
