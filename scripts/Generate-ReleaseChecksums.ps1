param(
    [string]$TargetDir = "dist/pilot-preview-release"
)

$basePath = (Resolve-Path $TargetDir).Path
$outPath = Join-Path $basePath "SHA256SUMS.txt"

if (Test-Path $outPath) {
    Remove-Item -Force $outPath
}

$files = Get-ChildItem -File -Recurse -Path $basePath | Where-Object { $_.FullName -ne $outPath } | Sort-Object FullName
$lines = New-Object System.Collections.Generic.List[string]

foreach ($f in $files) {
    $rel = $f.FullName.Substring($basePath.Length).TrimStart('\', '/') -replace '\\', '/'
    $hash = (Get-FileHash -Path $f.FullName -Algorithm SHA256).Hash
    $lines.Add("$hash  $rel")
}

[System.IO.File]::WriteAllLines($outPath, $lines, [System.Text.UTF8Encoding]::new($false))
Write-Host "Generated SHA256SUMS.txt for $($lines.Count) files in $TargetDir"
