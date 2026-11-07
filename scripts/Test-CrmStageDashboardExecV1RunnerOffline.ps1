#Requires -Version 5.1
<#
.SYNOPSIS
  Offline syntax + PreviewOnly gate-title checks for crm_stage_dashboard_exec_v1_runner.ps1
  Does not require CRM IIS/SQL. Asserts PackageVerifiedOnly (not PreviewPassed) when ExecuteEligible=False.
#>
$ErrorActionPreference = 'Stop'
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir)) { $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path }
$repo = (Resolve-Path (Join-Path $scriptDir '..')).Path
$runner = Join-Path $scriptDir 'crm_stage_dashboard_exec_v1_runner.ps1'
$package = Join-Path $repo 'release\dashboard-exec-v1\latest'
$evidence = Join-Path $repo 'docs\runs'

Write-Host "AST parse: $runner"
$tokens = $null; $errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($runner, [ref]$tokens, [ref]$errors)
if ($errors -and $errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    throw "AST parse failed: $($errors.Count) error(s)"
}
Write-Host "AST_OK"

# Ensure misleading Gate title 'PreviewPassed' is gone from runner
$raw = Get-Content $runner -Raw
if ($raw -match "Gate\s*=\s*'PreviewPassed'" -or $raw -match 'Gate=PreviewPassed') {
    throw "Runner still emits misleading PreviewPassed title"
}
if ($raw -notmatch 'PackageVerifiedOnly') { throw 'Runner missing PackageVerifiedOnly gate' }
if ($raw -notmatch 'StagingPreviewPassed') { throw 'Runner missing StagingPreviewPassed gate' }
Write-Host "GATE_TITLES_OK"

Write-Host "Running PreviewOnly offline..."
& powershell -NoProfile -ExecutionPolicy Bypass -File $runner -Mode PreviewOnly -PackageDir $package -EvidenceDir $evidence
$code = $LASTEXITCODE
$latest = Get-ChildItem $evidence -Filter 'stage_dashboard_exec_v1_runner_PreviewOnly_*.json' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $latest) { throw 'No PreviewOnly evidence JSON written' }
$j = Get-Content $latest.FullName -Raw | ConvertFrom-Json

Write-Host "Evidence=$($latest.FullName)"
Write-Host "Gate=$($j.Gate) ExecuteEligible=$($j.ExecuteEligible) ExitCode=$code"

if ($j.ExecuteEligible -eq $true) {
    throw 'Unexpected ExecuteEligible=True on offline/dev host'
}
if ($j.Gate -eq 'PreviewPassed') {
    throw 'Misleading Gate=PreviewPassed on offline host'
}
if ($j.Gate -ne 'PackageVerifiedOnly' -and $j.Gate -ne 'Pending') {
    throw "Unexpected Gate=$($j.Gate)"
}
# With known-good package we expect PackageVerifiedOnly
if ($j.Gate -ne 'PackageVerifiedOnly') {
    throw "Expected PackageVerifiedOnly for valid package offline, got $($j.Gate)"
}
if ($code -ne 0) { throw "Expected exit 0 for PackageVerifiedOnly, got $code" }

Write-Host "OFFLINE_TEST_PASSED" -ForegroundColor Green
