#Requires -Version 5.1
<#
.SYNOPSIS
  WinRM: copy fixed runner + provision script to CRM, provision isolated DashboardExec site, PreviewOnly.
  Never runs Execute. Never mutates Production / Sepidar / Issabel / Stage-v1024 target.
#>
[CmdletBinding()]
param(
    [string]$RemoteHost = 'crm-mgmt.sahar.loc',
    [int]$RemotePort = 5987,
    [string]$LocalRepo = 'D:\Antigravity\ParkPlace'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

. (Join-Path $LocalRepo 'scripts\_CrmSecrets.ps1')

$localRunner = Join-Path $LocalRepo 'scripts\crm_stage_dashboard_exec_v1_runner.ps1'
$localProvision = Join-Path $LocalRepo 'scripts\crm_provision_dashboard_exec_isolated_stage.ps1'
foreach ($p in @($localRunner, $localProvision)) {
    if (-not (Test-Path $p)) { throw "Missing local script: $p" }
}

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$localEvidenceDir = Join-Path $LocalRepo 'docs\runs'
New-Item -ItemType Directory -Force -Path $localEvidenceDir | Out-Null
$localEvidence = Join-Path $localEvidenceDir "crm_remote_isolated_dashboard_stage_$stamp.json"

$secPass = Get-CrmAdminSecurePassword -Purpose 'WinRM dashboard-exec isolated stage'
$cred = New-Object System.Management.Automation.PSCredential ('CRM\Administrator', $secPass)
$so = New-PSSessionOption -SkipCACheck:$false -SkipCNCheck:$false -SkipRevocationCheck:$false -OpenTimeout 30000 -OperationTimeout 600000
$uri = "https://${RemoteHost}:${RemotePort}/wsman"
Write-Host "Connecting WinRM $uri ..." -ForegroundColor Cyan
$session = New-PSSession -ConnectionUri $uri -Credential $cred -SessionOption $so -Authentication Negotiate

$bag = [ordered]@{
    TransferMethod     = "WinRM HTTPS ${RemoteHost}:${RemotePort}"
    ProductionTouched  = $false
    ModeExecuted       = 'PreviewOnly'
    ExecuteNotRun      = $true
    StartedAtUtc       = (Get-Date).ToUniversalTime().ToString('o')
}

try {
    Invoke-Command -Session $session -ScriptBlock {
        New-Item -ItemType Directory -Force -Path 'D:\ParkPlaceCRM\scripts' | Out-Null
        New-Item -ItemType Directory -Force -Path 'D:\ParkPlaceCRM\docs\runs' | Out-Null
        New-Item -ItemType Directory -Force -Path 'D:\ParkPlaceCRM\packages\dashboard-exec-v1\latest' | Out-Null
        New-Item -ItemType Directory -Force -Path 'D:\ParkPlaceCRM\staging\dashboard_exec_v1' | Out-Null
    }

    Write-Host 'Copying runner + provision scripts...' -ForegroundColor Cyan
    Copy-Item -Path $localRunner -Destination 'D:\ParkPlaceCRM\scripts\crm_stage_dashboard_exec_v1_runner.ps1' -ToSession $session -Force
    Copy-Item -Path $localProvision -Destination 'D:\ParkPlaceCRM\scripts\crm_provision_dashboard_exec_isolated_stage.ps1' -ToSession $session -Force

    $bag.RemoteScriptHashes = Invoke-Command -Session $session -ScriptBlock {
        $paths = @(
            'D:\ParkPlaceCRM\scripts\crm_stage_dashboard_exec_v1_runner.ps1',
            'D:\ParkPlaceCRM\scripts\crm_provision_dashboard_exec_isolated_stage.ps1'
        )
        foreach ($p in $paths) {
            [pscustomobject]@{
                Path = $p
                Exists = (Test-Path $p)
                Sha256 = if (Test-Path $p) { (Get-FileHash $p -Algorithm SHA256).Hash } else { $null }
                Bytes = if (Test-Path $p) { (Get-Item $p).Length } else { -1 }
            }
        }
    }

    Write-Host 'Provisioning isolated site (RC3 clone, port 51025)...' -ForegroundColor Cyan
    $provision = Invoke-Command -Session $session -ScriptBlock {
        $out = & powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\ParkPlaceCRM\scripts\crm_provision_dashboard_exec_isolated_stage.ps1' 2>&1
        $code = $LASTEXITCODE
        $statePath = 'D:\ParkPlaceCRM\staging\dashboard_exec_v1\site_state.json'
        $evidence = Get-ChildItem 'D:\ParkPlaceCRM\docs\runs\provision_dashboard_exec_isolated_stage_*.json' |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        [pscustomobject]@{
            ExitCode = $code
            Output = ($out | Out-String)
            StatePath = $statePath
            State = if (Test-Path $statePath) { Get-Content $statePath -Raw | ConvertFrom-Json } else { $null }
            EvidencePath = if ($evidence) { $evidence.FullName } else { $null }
            Evidence = if ($evidence) { Get-Content $evidence.FullName -Raw | ConvertFrom-Json } else { $null }
        }
    }
    $bag.Provision = $provision
    if ($provision.ExitCode -ne 0 -or -not $provision.State) {
        throw "Provision failed exit=$($provision.ExitCode). See Provision.Evidence."
    }

    $st = $provision.State
    Write-Host "PreviewOnly against $($st.SiteName) $($st.StagingApiBase) baseline=$($st.ExpectedApiBaselinePath)" -ForegroundColor Cyan
    $preview = Invoke-Command -Session $session -ArgumentList @(
        [string]$st.SiteName,
        [string]$st.StagingApiBase,
        [string]$st.StagingRoot,
        [string]$st.ExpectedApiBaselinePath,
        [int]$st.HttpPort
    ) -ScriptBlock {
        param($SiteName, $ApiBase, $Root, $Baseline, $Port)
        $args = @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass',
            '-File', 'D:\ParkPlaceCRM\scripts\crm_stage_dashboard_exec_v1_runner.ps1',
            '-Mode', 'PreviewOnly',
            '-PackageDir', 'D:\ParkPlaceCRM\packages\dashboard-exec-v1\latest',
            '-EvidenceDir', 'D:\ParkPlaceCRM\docs\runs',
            '-StagingSiteName', $SiteName,
            '-StagingApiBase', $ApiBase,
            '-StagingRoot', $Root,
            '-ExpectedApiBaselinePath', $Baseline,
            '-ExpectedBindingPort', $Port,
            '-FallbackRc3ApiPath', 'D:\ParkPlaceCRM\staging\v1024_c98981ae\rc3_api_20261106_143114_25779b88'
        )
        $out = & powershell @args 2>&1
        $code = $LASTEXITCODE
        $ev = Get-ChildItem 'D:\ParkPlaceCRM\docs\runs\stage_dashboard_exec_v1_runner_PreviewOnly_*.json' |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        [pscustomobject]@{
            ExitCode = $code
            Output = ($out | Out-String)
            EvidencePath = if ($ev) { $ev.FullName } else { $null }
            Evidence = if ($ev) { Get-Content $ev.FullName -Raw | ConvertFrom-Json } else { $null }
        }
    }
    $bag.Preview = $preview
    $bag.PreviewExitCode = $preview.ExitCode
    $bag.RemoteEvidencePath = $preview.EvidencePath
    $bag.PreviewEvidence = $preview.Evidence

    # Read-only confirm Stage-v1024 still on sepidarsearch / not retargeted by us
    $bag.StageV1024After = Invoke-Command -Session $session -ScriptBlock {
        Import-Module WebAdministration
        $site = Get-Website -Name 'ParkPalaceCRM-Stage-v1024'
        $api = Get-WebApplication -Site 'ParkPalaceCRM-Stage-v1024' | Where-Object { $_.path -eq '/api' } | Select-Object -First 1
        [pscustomobject]@{
            Web = $site.physicalPath
            Api = $api.physicalPath
            Bindings = @($site.bindings.Collection | ForEach-Object { $_.bindingInformation })
        }
    }

    $bag.FinishedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    ($bag | ConvertTo-Json -Depth 14) | Set-Content -Path $localEvidence -Encoding UTF8
    Write-Host "Local evidence: $localEvidence"
    if ($preview.Evidence) {
        Write-Host "Gate=$($preview.Evidence.Gate) ExecuteEligible=$($preview.Evidence.ExecuteEligible)"
    }
    if ($preview.ExitCode -ne 0) { exit $preview.ExitCode }
    exit 0
}
finally {
    if ($session) { Remove-PSSession $session }
}
