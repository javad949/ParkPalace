#Requires -Version 5.1
<#
.SYNOPSIS
  Builds an immutable package for the executive decision dashboard, independent of v1.0.24-rc3.
  Does NOT deploy Production. Does NOT flip deploy_allowed on rc3.
#>
param(
    [string]$OutRoot = (Join-Path $PSScriptRoot "..\release\dashboard-exec-v1"),
    [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$out = Join-Path $OutRoot $stamp
New-Item -ItemType Directory -Force -Path $out | Out-Null
$publishApi = Join-Path $out 'publish-api'
$publishWeb = Join-Path $out 'publish-web'

Write-Host "=== Build dashboard-exec-v1 package ===" -ForegroundColor Cyan
Write-Host "Repo: $repo"
Write-Host "Out:  $out"

Push-Location $repo
try {
    dotnet publish 'src\ParkPalace.Api\ParkPalace.Api.csproj' -c $Configuration -o $publishApi --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw "API publish failed: $LASTEXITCODE" }

    Push-Location 'src\parkpalace-web'
    try {
        if (-not (Test-Path 'node_modules')) { npm ci }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw "Web build failed: $LASTEXITCODE" }
        New-Item -ItemType Directory -Force -Path $publishWeb | Out-Null
        Copy-Item -Recurse -Force 'dist\*' $publishWeb
    } finally {
        Pop-Location
    }

    $apiZip = Join-Path $out 'ParkPalace_Api_dashboard-exec-v1.zip'
    $webZip = Join-Path $out 'ParkPalace_Web_dashboard-exec-v1.zip'
    if (Test-Path $apiZip) { Remove-Item $apiZip -Force }
    if (Test-Path $webZip) { Remove-Item $webZip -Force }
    Compress-Archive -Path (Join-Path $publishApi '*') -DestinationPath $apiZip
    Compress-Archive -Path (Join-Path $publishWeb '*') -DestinationPath $webZip

    function Get-Sha256([string]$path) {
        return (Get-FileHash -Path $path -Algorithm SHA256).Hash
    }

    $manifest = [ordered]@{
        packageId            = 'dashboard-exec-v1'
        builtAtUtc           = (Get-Date).ToUniversalTime().ToString('o')
        independentOf        = 'v1.0.24-rc3'
        deploy_allowed       = $false
        notes                = 'Executive decision dashboard only. Do not treat as rc3 cutover. Production deploy forbidden until separate staging gates pass.'
        files                = @(
            @{ relativePath = 'ParkPalace_Api_dashboard-exec-v1.zip'; sha256 = (Get-Sha256 $apiZip); bytes = (Get-Item $apiZip).Length }
            @{ relativePath = 'ParkPalace_Web_dashboard-exec-v1.zip'; sha256 = (Get-Sha256 $webZip); bytes = (Get-Item $webZip).Length }
        )
        evidenceDocs         = @(
            'docs/ParkPalace_Executive_Decision_Dashboard_Implementation_Report_20261107.md'
            'docs/ParkPalace_Executive_Decision_Dashboard_Audit_20261107.md'
            'docs/ParkPalace_Executive_Decision_Dashboard_KPI_Contracts_20261107.md'
            'docs/ParkPalace_Executive_Decision_Dashboard_Design_20261107.md'
        )
        stagingScript        = 'scripts/crm_stage_dashboard_exec_v1_runner.ps1'
        stagingHttpGatesOnly = 'scripts/crm_stage_dashboard_exec_v1_gates.ps1'
        productionTouched    = $false
    }

    $manifestPath = Join-Path $out 'manifest.json'
    ($manifest | ConvertTo-Json -Depth 6) | Set-Content -Path $manifestPath -Encoding UTF8

    # Stable pointer for latest build of this package line
    $latest = Join-Path $OutRoot 'latest'
    if (Test-Path $latest) { Remove-Item $latest -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $latest | Out-Null
    Copy-Item $apiZip (Join-Path $latest (Split-Path $apiZip -Leaf))
    Copy-Item $webZip (Join-Path $latest (Split-Path $webZip -Leaf))
    Copy-Item $manifestPath (Join-Path $latest 'manifest.json')

    Write-Host "API SHA256: $($manifest.files[0].sha256)" -ForegroundColor Green
    Write-Host "Web SHA256: $($manifest.files[1].sha256)" -ForegroundColor Green
    Write-Host "Manifest: $manifestPath"
    Write-Host "deploy_allowed=false (package + rc3 unchanged)" -ForegroundColor Yellow
}
finally {
    Pop-Location
}
