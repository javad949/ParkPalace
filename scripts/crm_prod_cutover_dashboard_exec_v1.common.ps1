#Requires -Version 5.1
# Shared cutover helpers for dashboard-exec-v1 (DryRun drills + Production edition).
# Do not run this file directly.
$script:DashboardExecExpectedApiZip = '3A2B24F486A914B1A9CAEA47CBCD21FD566342D3DDDBAE8FB4F0B71B4634AD12'
$script:DashboardExecExpectedWebZip = 'E1EC83E2AEDC9868AF03CBC5C9517C91617455C406F22C694F07A3B63494856F'
$script:DashboardExecExpectedInstalledApiDll = '64E20E1397B26026FBD5800050F55078D40EC33742ADDB69B7029A4CDB902134'
$script:DashboardExecKnownCurrentProdApiDll = '7E693A9BDD8BC449BBFA3D037694B86EF46B533BE0DC9C410A36198386B068E0'
function Sha([string]$p) {
    if (-not (Test-Path $p)) { return $null }
    return (Get-FileHash -Path $p -Algorithm SHA256).Hash
}

function Get-NormalizedFullPath([string]$path) {
    return [System.IO.Path]::GetFullPath($path).TrimEnd('\')
}

function Test-IsProductionApiPath([string]$path) {
    return (Get-NormalizedFullPath $path) -eq (Get-NormalizedFullPath 'D:\ParkPlaceCRM\api')
}

function Test-IsProductionWebPath([string]$path) {
    return (Get-NormalizedFullPath $path) -eq (Get-NormalizedFullPath 'D:\ParkPlaceCRM\web')
}

function Test-IsProductionLivePath([string]$path) {
    # True for either live root. Prefer Test-IsProductionApiPath / Test-IsProductionWebPath
    # when validating -ApiPath / -WebPath arguments (swapped paths must fail).
    return (Test-IsProductionApiPath $path) -or (Test-IsProductionWebPath $path)
}

function Assert-ProductionApiAndWebPaths([string]$ApiPath, [string]$WebPath) {
    if (-not (Test-IsProductionApiPath $ApiPath)) {
        throw "ApiPath must be exactly Production API live path D:\ParkPlaceCRM\api (got: $ApiPath)"
    }
    if (-not (Test-IsProductionWebPath $WebPath)) {
        throw "WebPath must be exactly Production Web live path D:\ParkPlaceCRM\web (got: $WebPath)"
    }
    if ((Get-NormalizedFullPath $ApiPath) -eq (Get-NormalizedFullPath $WebPath)) {
        throw "ApiPath and WebPath must differ."
    }
}

function Get-LiveConfigPresence([string]$settingsPath) {
    if (-not (Test-Path $settingsPath)) {
        return [ordered]@{ Exists = $false }
    }
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
        SftpHostPresent = -not [string]::IsNullOrWhiteSpace([string]$j.Telephony.Sftp.Host)
        RecordingsPathPresent = -not [string]::IsNullOrWhiteSpace([string]$j.Telephony.RecordingsPath)
    }
}

function Assert-LiveConfigPreserved([string]$settingsPath, $before) {
    $after = Get-LiveConfigPresence $settingsPath
    if (-not $after.Exists) { throw "Missing settings after deploy: $settingsPath" }
    if (-not $after.SepidarCsPresent) { throw 'Sepidar ConnectionString missing after deploy — Start forbidden.' }
    if (-not $after.CrmCsPresent) { throw 'Crm ConnectionString missing after deploy — Start forbidden.' }
    if (-not $after.TelephonyIsLiveHint) { throw 'Telephony provider not live (Simulated/empty) after deploy — Start forbidden.' }
    if ($before.SepidarCsLength -gt 0 -and $after.SepidarCsLength -ne $before.SepidarCsLength) {
        throw "Sepidar ConnectionString length changed ($($before.SepidarCsLength) -> $($after.SepidarCsLength)); treat as overwrite risk."
    }
    if ($before.TelephonyProvider -and $after.TelephonyProvider -ne $before.TelephonyProvider) {
        throw "Telephony provider changed ($($before.TelephonyProvider) -> $($after.TelephonyProvider))."
    }
    return $after
}

function New-UniqueBackupPlan {
    param(
        [string]$Root,
        [string]$OptionalBakPath,
        [string]$ApiLive,
        [string]$WebLive
    )
    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $token = [guid]::NewGuid().ToString('N').Substring(0, 8)
    $dir = Join-Path $Root ("cutover_{0}_{1}" -f $stamp, $token)
    $sqlBak = if ($OptionalBakPath) { $OptionalBakPath } else { Join-Path $dir ("ParkPalaceCRM_COPY_ONLY_{0}_{1}.bak" -f $stamp, $token) }
    return [ordered]@{
        Stamp = $stamp
        Token = $token
        BackupDir = $dir
        SqlBakPath = $sqlBak
        ApiMirror = Join-Path $dir 'files_api'
        WebMirror = Join-Path $dir 'files_web'
        ConfigSnapshot = Join-Path $dir 'config_snapshot'
        PriorApiRename = "$ApiLive.bak_dashboardexec_$stamp`_$token"
        PriorWebRename = "$WebLive.bak_dashboardexec_$stamp`_$token"
        StagingApiExtract = Join-Path $dir 'extract_api'
        StagingWebExtract = Join-Path $dir 'extract_web'
        EvidenceName = "prod_dashboard_exec_v1_cutover_runner_$stamp.json"
    }
}

function Invoke-FileTreeRollback {
    param(
        [string]$LivePath,
        [string]$PriorPath,
        [string]$Label,
        [string]$MirrorPath = $null
    )
    # Prefer robocopy /MIR from pre-swap file mirror — no locked-folder rename required.
    if ($MirrorPath -and (Test-Path $MirrorPath)) {
        if (-not (Test-Path $LivePath)) { New-Item -ItemType Directory -Force -Path $LivePath | Out-Null }
        Write-Host "Rollback MIR $Label <- $MirrorPath" -ForegroundColor Yellow
        & robocopy $MirrorPath $LivePath /MIR /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
        $code = $LASTEXITCODE
        if ($code -ge 8) { throw "Rollback robocopy /MIR failed for ${Label}: exit=$code" }
        # Leave PriorPath in place for forensics; do not Remove-Item (can lock/hang under AV).
        Write-Host "Rollback MIR $Label done exit=$code" -ForegroundColor Yellow
        return
    }

    if (-not (Test-Path $PriorPath)) { throw "Rollback prior missing for ${Label}: $PriorPath" }
    if (Test-Path $LivePath) {
        $failed = "$LivePath.failed_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
        Move-Item -LiteralPath $LivePath -Destination $failed -Force
    }
    Move-Item -LiteralPath $PriorPath -Destination $LivePath -Force
}

function Copy-Tree([string]$src, [string]$dst) {
    New-Item -ItemType Directory -Force -Path $dst | Out-Null
    & robocopy $src $dst /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
    $code = $LASTEXITCODE
    if ($code -ge 8) { throw "robocopy failed $src -> $dst exit=$code" }
}

function Invoke-SqlCopyOnlyBackup {
    param([string]$SqlInstance, [string]$Db, [string]$BakPath)
    $bakDir = Split-Path $BakPath -Parent
    New-Item -ItemType Directory -Force -Path $bakDir | Out-Null
    # INIT reinitializes media and would destroy an existing .bak at this path — refuse overwrite.
    if (Test-Path -LiteralPath $BakPath) {
        throw "Refusing BACKUP DATABASE: target .bak already exists and INIT must not overwrite it: $BakPath"
    }
    # WITH options are comma-separated; do NOT place ';' before SKIP/STATS (that aborted the statement).
    $q = @"
BACKUP DATABASE [$Db]
TO DISK = N'$($BakPath.Replace("'","''"))'
WITH COPY_ONLY, COMPRESSION, INIT, NAME = N'dashboard-exec-v1-cutover', SKIP, STATS = 10;
"@
    # Capture/discard sqlcmd STATS chatter so only the bak SHA is returned on the success stream.
    $null = sqlcmd -S $SqlInstance -E -Q $q -b 2>&1
    if ($LASTEXITCODE -ne 0) { throw "BACKUP DATABASE failed exit=$LASTEXITCODE" }
    if (-not (Test-Path -LiteralPath $BakPath)) { throw "Backup file missing after BACKUP: $BakPath" }
    return [string](Sha $BakPath)
}

function Wait-WebAppPoolState {
    param(
        [Parameter(Mandatory = $true)][string]$AppPoolName,
        [Parameter(Mandatory = $true)][ValidateSet('Started', 'Stopped')][string]$DesiredState,
        [int]$TimeoutSeconds = 60
    )
    Import-Module WebAdministration
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $st = $null
    do {
        Start-Sleep -Seconds 1
        $st = (Get-WebAppPoolState -Name $AppPoolName).Value
    } while ($st -ne $DesiredState -and (Get-Date) -lt $deadline)
    if ($st -ne $DesiredState) {
        throw "AppPool '$AppPoolName' did not reach $DesiredState within ${TimeoutSeconds}s (state=$st). Refusing to continue."
    }
    return $st
}

function Invoke-ProductionHealthProbe {
    # Production probes must use the public hostname with normal TLS validation — never loopback host or disabled cert checks.
    param([string]$HealthUrl = 'https://crm.sahar.loc/api/health')
    $h = Invoke-WebRequest -Uri $HealthUrl -UseDefaultCredentials -UseBasicParsing -TimeoutSec 30
    $body = $h.Content
    return [ordered]@{
        Url = $HealthUrl
        StatusCode = [int]$h.StatusCode
        BodyPreview = $body.Substring(0, [Math]::Min(400, $body.Length))
        DatabaseConnected = ($body -match '"databaseConnected"\s*:\s*true')
        SepidarActiveReadOnly = ($body -match 'Active \(Read-Only\)')
        TlsBypassUsed = $false
    }
}

function Test-PostInstallSmokeHttp([string]$apiPath, [string]$expectedDllSha) {
    $result = [ordered]@{
        Mode = 'Http'
        ApiDllSha = Sha (Join-Path $apiPath 'ParkPalace.Api.dll')
        ApiDllMatchExpected = $false
        Health = $null
        Me = $null
        Passed = $false
        TlsBypassUsed = $false
    }
    $result.ApiDllMatchExpected = ($result.ApiDllSha -eq $expectedDllSha)
    try {
        $result.Health = Invoke-ProductionHealthProbe -HealthUrl 'https://crm.sahar.loc/api/health'
    } catch {
        $result.Health = [ordered]@{ Url = 'https://crm.sahar.loc/api/health'; Error = $_.Exception.Message; TlsBypassUsed = $false }
    }
    try {
        $me = Invoke-WebRequest -Uri 'https://crm.sahar.loc/api/me' -UseDefaultCredentials -UseBasicParsing -TimeoutSec 30
        $result.Me = [ordered]@{ Url = 'https://crm.sahar.loc/api/me'; StatusCode = [int]$me.StatusCode }
    } catch {
        $status = $null
        if ($_.Exception.Response) { try { $status = [int]$_.Exception.Response.StatusCode } catch {} }
        $result.Me = [ordered]@{ Url = 'https://crm.sahar.loc/api/me'; StatusCode = $status; Error = $_.Exception.Message }
    }
    $result.Passed = (
        $result.ApiDllMatchExpected -and
        $result.Health -and $result.Health.StatusCode -eq 200 -and
        $result.Health.DatabaseConnected -and $result.Health.SepidarActiveReadOnly -and
        $result.Me -and $result.Me.StatusCode -eq 200 -and
        -not $result.TlsBypassUsed
    )
    return $result
}

function Test-PostInstallSmokeFiles([string]$apiPath, [string]$expectedDllSha, [bool]$forceFail) {
    $dll = Join-Path $apiPath 'ParkPalace.Api.dll'
    $cfg = Join-Path $apiPath 'appsettings.Production.json'
    $result = [ordered]@{
        Mode = 'FilesIsolated'
        ApiDllExists = (Test-Path $dll)
        ConfigExists = (Test-Path $cfg)
        ForcedFailure = [bool]$forceFail
        Passed = $false
    }
    # Skip Get-FileHash on dll in isolated mode (AV hang risk); presence + config is enough before ForceFail.
    $result.Passed = ($result.ApiDllExists -and $result.ConfigExists -and -not $forceFail)
    if ($forceFail) { $result.FailReason = 'ForceSmokeFail for isolated rollback drill' }
    return $result
}

function Invoke-DashboardExecCutoverExecution {
    param(
        $Plan,
        [string]$ApiLive,
        [string]$WebLive,
        [string]$ApiZip,
        [string]$WebZip,
        $LiveConfigBefore,
        [string]$ExpectedDllSha,
        [bool]$DoSqlBackup,
        [string]$SqlInstance,
        [string]$ProdDb,
        [bool]$DoIis,
        [string]$AppPoolName,
        [ValidateSet('Http','Files')][string]$SmokeMode,
        [bool]$ForceSmokeFail
    )

    $exec = [ordered]@{
        StartedAtUtc = [DateTime]::UtcNow.ToString('o')
        Steps = @()
        RolledBack = $false
        Succeeded = $false
        RollbackVerified = $false
    }
    function Step($n) { $exec.Steps += $n; Write-Host "=== $n ===" -ForegroundColor Cyan }

    try {
        New-Item -ItemType Directory -Force -Path $Plan.BackupDir | Out-Null
        New-Item -ItemType Directory -Force -Path $Plan.ConfigSnapshot | Out-Null

        $priorDllPath = Join-Path $ApiLive 'ParkPalace.Api.dll'
        $exec.PriorApiDllSha = Sha $priorDllPath

        Step 'Backup-file-mirrors'
        Copy-Tree $ApiLive $Plan.ApiMirror
        Copy-Tree $WebLive $Plan.WebMirror
        Copy-Item (Join-Path $ApiLive 'appsettings.json') (Join-Path $Plan.ConfigSnapshot 'appsettings.json') -Force -ErrorAction SilentlyContinue
        Copy-Item (Join-Path $ApiLive 'appsettings.Production.json') (Join-Path $Plan.ConfigSnapshot 'appsettings.Production.json') -Force

        if ($DoSqlBackup) {
            Step 'BACKUP-DATABASE-COPY_ONLY'
            $exec.SqlBakSha256 = Invoke-SqlCopyOnlyBackup -SqlInstance $SqlInstance -Db $ProdDb -BakPath $Plan.SqlBakPath
            $exec.SqlBakPath = $Plan.SqlBakPath
        } else {
            Step 'SKIP-SQL-backup (isolated drill)'
            $exec.SqlBackupSkipped = $true
        }

        if ($DoIis) {
            Step 'Stop-WebAppPool'
            Import-Module WebAdministration
            Stop-WebAppPool -Name $AppPoolName
            $stopped = Wait-WebAppPoolState -AppPoolName $AppPoolName -DesiredState Stopped -TimeoutSeconds 60
            $exec.AppPoolStoppedConfirmed = ($stopped -eq 'Stopped')
            if (-not $exec.AppPoolStoppedConfirmed) {
                throw 'AppPool stop not confirmed; refusing file swap.'
            }
            Step 'AppPool-Stopped-confirmed'
        } else {
            Step 'SKIP-IIS (isolated drill)'
        }

        Step 'Rename-live-to-Prior'
        if (Test-Path $Plan.PriorApiRename) { throw "Prior api path already exists: $($Plan.PriorApiRename)" }
        if (Test-Path $Plan.PriorWebRename) { throw "Prior web path already exists: $($Plan.PriorWebRename)" }
        Rename-Item $ApiLive $Plan.PriorApiRename
        Rename-Item $WebLive $Plan.PriorWebRename

        Step 'Extract-ZIPs-to-staging'
        if (Test-Path $Plan.StagingApiExtract) { Remove-Item $Plan.StagingApiExtract -Recurse -Force }
        if (Test-Path $Plan.StagingWebExtract) { Remove-Item $Plan.StagingWebExtract -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $Plan.StagingApiExtract, $Plan.StagingWebExtract | Out-Null
        Expand-Archive -Path $ApiZip -DestinationPath $Plan.StagingApiExtract -Force
        Expand-Archive -Path $WebZip -DestinationPath $Plan.StagingWebExtract -Force

        Step 'Promote-staging-to-live-paths'
        # Staging extract lives under BackupDir; live paths are siblings elsewhere — must Move-Item (not Rename-Item).
        if (Test-Path $ApiLive) { throw "Live api path unexpectedly exists before promote: $ApiLive" }
        if (Test-Path $WebLive) { throw "Live web path unexpectedly exists before promote: $WebLive" }
        Move-Item -Path $Plan.StagingApiExtract -Destination $ApiLive
        Move-Item -Path $Plan.StagingWebExtract -Destination $WebLive

        Step 'Restore-live-appsettings-over-package'
        $snapProd = Join-Path $Plan.ConfigSnapshot 'appsettings.Production.json'
        $snapApp = Join-Path $Plan.ConfigSnapshot 'appsettings.json'
        if (Test-Path $snapProd) { Copy-Item $snapProd (Join-Path $ApiLive 'appsettings.Production.json') -Force }
        if (Test-Path $snapApp) { Copy-Item $snapApp (Join-Path $ApiLive 'appsettings.json') -Force }

        Step 'Assert-LiveConfigPreserved'
        $null = Assert-LiveConfigPreserved (Join-Path $ApiLive 'appsettings.Production.json') $LiveConfigBefore

        if ($DoIis) {
            Step 'Start-WebAppPool'
            Start-WebAppPool -Name $AppPoolName
            $started = Wait-WebAppPoolState -AppPoolName $AppPoolName -DesiredState Started -TimeoutSeconds 60
            $exec.AppPoolStartedConfirmed = ($started -eq 'Started')
            if (-not $exec.AppPoolStartedConfirmed) {
                throw 'AppPool start not confirmed; treating as deploy failure.'
            }
        }

        Step 'PostInstall-Smoke'
        if ($SmokeMode -eq 'Http') {
            $smoke = Test-PostInstallSmokeHttp -apiPath $ApiLive -expectedDllSha $ExpectedDllSha
        } else {
            $smoke = Test-PostInstallSmokeFiles -apiPath $ApiLive -expectedDllSha $ExpectedDllSha -forceFail:$ForceSmokeFail
        }
        $exec.Smoke = $smoke
        if (-not $smoke.Passed) {
            $reason = if ($smoke.FailReason) { $smoke.FailReason } else { 'smoke Passed=false' }
            throw "Post-install smoke failed: $reason"
        }

        $exec.Succeeded = $true
        $exec.FinishedAtUtc = [DateTime]::UtcNow.ToString('o')
        return $exec
    }
    catch {
        $exec.Error = $_.Exception.Message
        Step 'AUTO-ROLLBACK'
        try {
            if ($DoIis) {
                try {
                    Stop-WebAppPool -Name $AppPoolName
                    Wait-WebAppPoolState -AppPoolName $AppPoolName -DesiredState Stopped -TimeoutSeconds 60 | Out-Null
                } catch {}
            }
            Invoke-FileTreeRollback -LivePath $ApiLive -PriorPath $Plan.PriorApiRename -Label 'api' -MirrorPath $Plan.ApiMirror
            Invoke-FileTreeRollback -LivePath $WebLive -PriorPath $Plan.PriorWebRename -Label 'web' -MirrorPath $Plan.WebMirror

            # ensure live configs from snapshot if somehow missing
            $snapProd = Join-Path $Plan.ConfigSnapshot 'appsettings.Production.json'
            if ((Test-Path $snapProd) -and (Test-Path $ApiLive)) {
                Copy-Item $snapProd (Join-Path $ApiLive 'appsettings.Production.json') -Force
            }
            if ($DoIis) {
                Start-WebAppPool -Name $AppPoolName
                Wait-WebAppPoolState -AppPoolName $AppPoolName -DesiredState Started -TimeoutSeconds 60 | Out-Null
            }
            $exec.RolledBack = $true

            # Rollback must restore prior binary hash; Production IIS path also requires live Health.
            $afterDllSha = Sha (Join-Path $ApiLive 'ParkPalace.Api.dll')
            $exec.RollbackApiDllSha = $afterDllSha
            $hashOk = ($null -ne $exec.PriorApiDllSha -and $afterDllSha -eq $exec.PriorApiDllSha)
            $exec.RollbackBinaryHashMatch = $hashOk

            if ($DoIis) {
                try {
                    $exec.RollbackHealth = Invoke-ProductionHealthProbe -HealthUrl 'https://crm.sahar.loc/api/health'
                } catch {
                    $exec.RollbackHealth = [ordered]@{ Url = 'https://crm.sahar.loc/api/health'; Error = $_.Exception.Message }
                }
                $healthOk = (
                    $exec.RollbackHealth -and $exec.RollbackHealth.StatusCode -eq 200 -and
                    $exec.RollbackHealth.DatabaseConnected -and $exec.RollbackHealth.SepidarActiveReadOnly -and
                    -not $exec.RollbackHealth.TlsBypassUsed
                )
                $exec.RollbackHealthOk = [bool]$healthOk
                $exec.RollbackVerified = ($hashOk -and $healthOk)
            } else {
                # Isolated file drill: binary hash restore is the verification gate (no live IIS).
                $exec.RollbackVerified = [bool]$hashOk
            }

            if (-not $exec.RollbackVerified) {
                throw "Rollback completed but verification failed (hashOk=$hashOk healthOk=$($exec.RollbackHealthOk))."
            }
            Write-Host "Rollback verified hashOk=$hashOk healthOk=$($exec.RollbackHealthOk)" -ForegroundColor Green
        }
        catch {
            $exec.RollbackError = $_.Exception.Message
            Write-Host "Rollback secondary error: $($_.Exception.Message)" -ForegroundColor Red
        }
        $exec.FinishedAtUtc = [DateTime]::UtcNow.ToString('o')
        Write-Host 'Execution function returning after rollback path' -ForegroundColor Yellow
        return $exec
    }
}

function Initialize-IsolatedDrillTrees {
    param([string]$Root, [string]$PackageDir)
    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $token = [guid]::NewGuid().ToString('N').Substring(0, 8)
    $base = Join-Path $Root ("drill_{0}_{1}" -f $stamp, $token)
    $api = Join-Path $base 'api'
    $web = Join-Path $base 'web'
    $pkg = Join-Path $base 'package'
    $bakRoot = Join-Path $base 'backups'
    New-Item -ItemType Directory -Force -Path $api, $web, $pkg, $bakRoot | Out-Null

    # prior "production-like" content with live settings that must survive
    Set-Content -Path (Join-Path $api 'marker_prior.txt') -Value 'PRIOR_LIVE_API' -Encoding UTF8
    Set-Content -Path (Join-Path $web 'marker_prior.txt') -Value 'PRIOR_LIVE_WEB' -Encoding UTF8
    # fake prior dll so SHA differs from package
    [System.IO.File]::WriteAllBytes((Join-Path $api 'ParkPalace.Api.dll'), [Text.Encoding]::ASCII.GetBytes('PRIOR_DLL_BYTES'))
    $liveCfg = @{
        ConnectionStrings = @{
            SepidarDatabase = 'Server=SEPIDAR_LIVE;Database=Sepidar;Trusted_Connection=True;'
            CrmDatabase     = 'Server=CRM_LIVE;Database=ParkPalaceCRM;Trusted_Connection=True;'
        }
        Telephony = @{
            Provider = 'IssabelARI'
            Sftp = @{ Host = 'issabel.live' }
            RecordingsPath = 'D:\ParkPlaceCRM\api\recordings'
        }
    } | ConvertTo-Json -Depth 6
    Set-Content -Path (Join-Path $api 'appsettings.Production.json') -Value $liveCfg -Encoding UTF8
    Set-Content -Path (Join-Path $api 'appsettings.json') -Value $liveCfg -Encoding UTF8

    # Synthetic small ZIPs — same Expand-Archive/swap/preserve/rollback path as Production,
    # without extracting the multi-MB accepted package (that belongs to release window).
    $stageApi = Join-Path $base 'synth_api'
    $stageWeb = Join-Path $base 'synth_web'
    New-Item -ItemType Directory -Force -Path $stageApi, $stageWeb | Out-Null
    [System.IO.File]::WriteAllBytes((Join-Path $stageApi 'ParkPalace.Api.dll'), [Text.Encoding]::ASCII.GetBytes('PACKAGE_DLL_BYTES_FOR_DRILL'))
    Set-Content -Path (Join-Path $stageApi 'package_only_marker.txt') -Value 'FROM_PACKAGE' -Encoding UTF8
    # Package defaults that MUST be overwritten by live snapshot restore:
    $pkgCfg = @{
        ConnectionStrings = @{ SepidarDatabase = ''; CrmDatabase = 'Server=PKG;Database=X;' }
        Telephony = @{ Provider = 'Simulated' }
    } | ConvertTo-Json -Depth 5
    Set-Content -Path (Join-Path $stageApi 'appsettings.Production.json') -Value $pkgCfg -Encoding UTF8
    Set-Content -Path (Join-Path $stageApi 'appsettings.json') -Value $pkgCfg -Encoding UTF8
    Set-Content -Path (Join-Path $stageWeb 'index.html') -Value '<html>package-web</html>' -Encoding UTF8
    $apiZipOut = Join-Path $pkg 'ParkPalace_Api_dashboard-exec-v1.zip'
    $webZipOut = Join-Path $pkg 'ParkPalace_Web_dashboard-exec-v1.zip'
    if (Test-Path $apiZipOut) { Remove-Item $apiZipOut -Force }
    if (Test-Path $webZipOut) { Remove-Item $webZipOut -Force }
    Compress-Archive -Path (Join-Path $stageApi '*') -DestinationPath $apiZipOut
    Compress-Archive -Path (Join-Path $stageWeb '*') -DestinationPath $webZipOut
    $synthDllSha = Sha (Join-Path $stageApi 'ParkPalace.Api.dll')
    $man = [ordered]@{
        packageId = 'dashboard-exec-v1-isolated-drill'
        deploy_allowed = $false
        notes = 'Synthetic package for IsolatedFailureDrill only'
        files = @(
            @{ relativePath = 'ParkPalace_Api_dashboard-exec-v1.zip'; sha256 = (Sha $apiZipOut) }
            @{ relativePath = 'ParkPalace_Web_dashboard-exec-v1.zip'; sha256 = (Sha $webZipOut) }
        )
    }
    ($man | ConvertTo-Json -Depth 6) | Set-Content -Path (Join-Path $pkg 'manifest.json') -Encoding UTF8

    return [ordered]@{
        Base = $base
        ApiPath = $api
        WebPath = $web
        PackageDir = $pkg
        BackupRoot = $bakRoot
        PriorApiDllSha = Sha (Join-Path $api 'ParkPalace.Api.dll')
        SyntheticPackageDllSha = $synthDllSha
    }
}


