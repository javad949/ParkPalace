#Requires -Version 5.1
<# Offline checks for common.ps1 blockers (backup SQL text, no TLS bypass, stop helper present). #>
param([string]$CommonPath = 'D:\Antigravity\ParkPlace\scripts\crm_prod_cutover_dashboard_exec_v1.common.ps1')
$ErrorActionPreference = 'Stop'
$failed = 0
function Ok($n){ Write-Host "PASS $n" -ForegroundColor Green }
function Bad($n,$m){ Write-Host "FAIL $n :: $m" -ForegroundColor Red; $script:failed++ }

$src = Get-Content $CommonPath -Raw -Encoding UTF8

if ($src -match "NAME = N'dashboard-exec-v1-cutover'; SKIP") {
    Bad 'BackupSqlSemicolon' 'semicolon still present before SKIP'
} else { Ok 'BackupSqlSemicolon' }

if ($src -notmatch "NAME = N'dashboard-exec-v1-cutover', SKIP, STATS = 10") {
    Bad 'BackupSqlComma' 'expected comma-separated SKIP, STATS'
} else { Ok 'BackupSqlComma' }

if ($src -match 'TrustAll|CertificatePolicy') {
    Bad 'NoTlsBypass' 'TLS TrustAll/CertificatePolicy must not remain for Production smoke'
} else { Ok 'NoTlsBypass' }

if ($src -match '127\.0\.0\.1/api/health') {
    Bad 'NoLoopbackHealth' '127.0.0.1 health fallback must be removed'
} else { Ok 'NoLoopbackHealth' }

if ($src -notmatch 'function Wait-WebAppPoolState') {
    Bad 'WaitAppPoolHelper' 'Wait-WebAppPoolState missing'
} else { Ok 'WaitAppPoolHelper' }

if ($src -notmatch 'AppPool-Stopped-confirmed') {
    Bad 'StopConfirmedStep' 'explicit stop confirmation step missing'
} else { Ok 'StopConfirmedStep' }

if ($src -notmatch 'RollbackBinaryHashMatch' -or $src -notmatch 'RollbackVerified') {
    Bad 'RollbackVerify' 'rollback binary hash / verified gates missing'
} else { Ok 'RollbackVerify' }

if ($src -notmatch 'Invoke-ProductionHealthProbe') {
    Bad 'ProdHealthProbe' 'Invoke-ProductionHealthProbe missing'
} else { Ok 'ProdHealthProbe' }

if ($src -notmatch 'already exists and INIT must not overwrite') {
    Bad 'BakNoOverwrite' 'Invoke-SqlCopyOnlyBackup must refuse existing .bak before INIT'
} else { Ok 'BakNoOverwrite' }

if ($src -notmatch 'function Test-IsProductionApiPath' -or $src -notmatch 'function Test-IsProductionWebPath') {
    Bad 'DistinctPathHelpers' 'role-specific API/Web path helpers missing'
} else { Ok 'DistinctPathHelpers' }

if ($src -notmatch 'function Assert-ProductionApiAndWebPaths') {
    Bad 'AssertProdPaths' 'Assert-ProductionApiAndWebPaths missing'
} else { Ok 'AssertProdPaths' }

. $CommonPath
if (-not (Test-IsProductionApiPath 'D:\ParkPlaceCRM\api')) { Bad 'ApiPathExact' 'api path should match' } else { Ok 'ApiPathExact' }
if (Test-IsProductionApiPath 'D:\ParkPlaceCRM\web') { Bad 'ApiPathNotWeb' 'web must not satisfy API helper' } else { Ok 'ApiPathNotWeb' }
if (-not (Test-IsProductionWebPath 'D:\ParkPlaceCRM\web')) { Bad 'WebPathExact' 'web path should match' } else { Ok 'WebPathExact' }
if (Test-IsProductionWebPath 'D:\ParkPlaceCRM\api') { Bad 'WebPathNotApi' 'api must not satisfy Web helper' } else { Ok 'WebPathNotApi' }
try {
    Assert-ProductionApiAndWebPaths -ApiPath 'D:\ParkPlaceCRM\web' -WebPath 'D:\ParkPlaceCRM\api'
    Bad 'SwappedPathsRejected' 'swapped api/web should throw'
} catch { Ok 'SwappedPathsRejected' }

$tmpBak = Join-Path $env:TEMP ("dashboardexec_bak_refuse_{0}.bak" -f [guid]::NewGuid().ToString('N'))
Set-Content -LiteralPath $tmpBak -Value 'existing' -Encoding ASCII
try {
    Invoke-SqlCopyOnlyBackup -SqlInstance 'localhost' -Db 'nope' -BakPath $tmpBak
    Bad 'ExistingBakRefused' 'existing bak should throw before sqlcmd'
} catch {
    if ($_.Exception.Message -match 'already exists and INIT must not overwrite') { Ok 'ExistingBakRefused' }
    else { Bad 'ExistingBakRefused' $_.Exception.Message }
} finally {
    Remove-Item -LiteralPath $tmpBak -Force -ErrorAction SilentlyContinue
}

$prodSrc = Get-Content (Join-Path (Split-Path $CommonPath -Parent) 'crm_prod_cutover_dashboard_exec_v1_production.ps1') -Raw -Encoding UTF8
if ($prodSrc -notmatch 'Assert-ProductionApiAndWebPaths') {
    Bad 'ProdUsesAssertPaths' 'production edition must call Assert-ProductionApiAndWebPaths'
} else { Ok 'ProdUsesAssertPaths' }

if ($failed -gt 0) { Write-Host "FAILED=$failed"; exit 1 }
Write-Host 'ALL_COMMON_FIX_CHECKS_PASS'
exit 0
