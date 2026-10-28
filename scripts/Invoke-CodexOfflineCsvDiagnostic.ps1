param([string]$ManifestPath = "$PSScriptRoot/../docs/runs/codex_csv_diagnostic_20261028/input_manifest.json")
$ErrorActionPreference = "Stop"
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$inputs = @{}
foreach ($file in $manifest.Files) {
    if ((Get-FileHash -LiteralPath $file.Path -Algorithm SHA256).Hash -ne $file.Sha256) {
        throw "Input hash mismatch: $($file.Role)"
    }
    $inputs[$file.Role] = $file.Path
}
foreach ($role in @("FiscalYears", "Stocks", "Units", "StockSummary")) {
    if (!$inputs.ContainsKey($role)) { throw "Missing input role: $role" }
}
$repo = Split-Path $PSScriptRoot
$runId = "codex_offline_" + [DateTime]::UtcNow.ToString("yyyyMMdd_HHmmss", [Globalization.CultureInfo]::InvariantCulture)
$outputDir = Join-Path $repo "docs/runs/$runId"
# Exact paths only. No nested or companion run discovery; CRM baseline is in memory.
& dotnet (Join-Path $repo "dist/pilot-preview-release/ParkPalace.Worker.dll") --pilot-preview --csv-dir (Split-Path $ManifestPath) --run-id $runId --fiscal-years-file $inputs.FiscalYears --stocks-file $inputs.Stocks --units-file $inputs.Units --summary-file $inputs.StockSummary --output-json "$outputDir/pilot_preview_output.json" --output-report "$outputDir/pilot_preview_report.md"
$result = $LASTEXITCODE
if ($result -ne 2) { throw "Expected diagnostic-only exit 2; received $result" }
$report = Get-Content "$outputDir/pilot_preview_output.json" -Raw | ConvertFrom-Json
if ($report.IsExecutable -ne $false -or $report.MissingMovementData -ne $true) {
    throw "Diagnostic safety assertions failed"
}
Copy-Item -LiteralPath $ManifestPath -Destination "$outputDir/input_manifest.json"
Write-Host "Diagnostic output: $outputDir"