#Requires -Version 5.1
<#
.SYNOPSIS
  Isolated BACKUP DATABASE drill for the fixed COPY_ONLY statement in common.ps1.
  Creates a throwaway DB, runs Invoke-SqlCopyOnlyBackup, then drops DB and deletes .bak.
  Does NOT touch Production CRM DB or IIS.
#>
[CmdletBinding()]
param(
    [string]$RemoteHost = 'crm-mgmt.sahar.loc',
    [int]$RemotePort = 5987,
    [string]$LocalRepo = 'D:\Antigravity\ParkPlace',
    [string]$SqlInstance = 'localhost',
    [string]$ThrowawayDb = 'ParkPalaceCRM_CutoverBackupProbe_20261107'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
. (Join-Path $LocalRepo 'scripts\_CrmSecrets.ps1')

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$localEvidenceDir = Join-Path $LocalRepo 'docs\runs'
New-Item -ItemType Directory -Force -Path $localEvidenceDir | Out-Null
$localEvidence = Join-Path $localEvidenceDir "prod_dashboard_exec_v1_isolated_sql_backup_$stamp.json"

$commonLocal = Join-Path $LocalRepo 'scripts\crm_prod_cutover_dashboard_exec_v1.common.ps1'
if (-not (Test-Path $commonLocal)) { throw "Missing $commonLocal" }

$secPass = Get-CrmAdminSecurePassword -Purpose 'WinRM isolated SQL BACKUP verify (throwaway DB only)'
$cred = New-Object System.Management.Automation.PSCredential ('CRM\Administrator', $secPass)
$so = New-PSSessionOption -SkipCACheck:$false -SkipCNCheck:$false -SkipRevocationCheck:$false -OpenTimeout 30000 -OperationTimeout 300000
$uri = "https://${RemoteHost}:${RemotePort}/wsman"
Write-Host "Connecting WinRM $uri (isolated SQL BACKUP) ..." -ForegroundColor Cyan
$session = New-PSSession -ConnectionUri $uri -Credential $cred -SessionOption $so -Authentication Negotiate

$bag = [ordered]@{
    StartedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    Mode = 'IsolatedSqlBackupVerify'
    ProductionMutated = $false
    ProductionSiteTouched = $false
    ThrowawayDb = $ThrowawayDb
    SqlInstance = $SqlInstance
}

try {
    $remoteScripts = 'D:\ParkPlaceCRM\scripts'
    Invoke-Command -Session $session -ScriptBlock {
        param($dir)
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        New-Item -ItemType Directory -Force -Path 'D:\ParkPlaceCRM\backups\isolated-sql-probe' | Out-Null
        New-Item -ItemType Directory -Force -Path 'D:\ParkPlaceCRM\docs\runs' | Out-Null
    } -ArgumentList $remoteScripts

    Copy-Item -Path $commonLocal -Destination (Join-Path $remoteScripts 'crm_prod_cutover_dashboard_exec_v1.common.ps1') -ToSession $session -Force

    $remote = Invoke-Command -Session $session -ArgumentList @($SqlInstance, $ThrowawayDb) -ScriptBlock {
        param($SqlInstance, $ThrowawayDb)
        $ErrorActionPreference = 'Stop'
        $common = 'D:\ParkPlaceCRM\scripts\crm_prod_cutover_dashboard_exec_v1.common.ps1'
        . $common

        $src = Get-Content $common -Raw -Encoding UTF8
        if ($src -match "NAME = N'dashboard-exec-v1-cutover'; SKIP") {
            throw 'common.ps1 still has semicolon before SKIP'
        }
        if ($src -notmatch "NAME = N'dashboard-exec-v1-cutover', SKIP, STATS = 10") {
            throw 'common.ps1 missing comma-separated SKIP, STATS'
        }

        # Ensure throwaway DB does not collide with prod
        if ($ThrowawayDb -eq 'ParkPalaceCRM' -or $ThrowawayDb -notmatch 'CutoverBackupProbe') {
            throw "Refusing non-probe DB name: $ThrowawayDb"
        }

        sqlcmd -S $SqlInstance -E -b -Q @"
IF DB_ID(N'$ThrowawayDb') IS NOT NULL
BEGIN
  ALTER DATABASE [$ThrowawayDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE [$ThrowawayDb];
END
CREATE DATABASE [$ThrowawayDb];
"@
        if ($LASTEXITCODE -ne 0) { throw "CREATE DATABASE failed exit=$LASTEXITCODE" }

        $bak = "D:\ParkPlaceCRM\backups\isolated-sql-probe\${ThrowawayDb}_$([guid]::NewGuid().ToString('N').Substring(0,8)).bak"
        $sha = $null
        $backupOk = $false
        $backupError = $null
        try {
            $sha = [string](Invoke-SqlCopyOnlyBackup -SqlInstance $SqlInstance -Db $ThrowawayDb -BakPath $bak)
            # If sqlcmd STATS leaked into the return stream, take the last 64-char hex token.
            if ($sha -notmatch '^[A-F0-9]{64}$') {
                $hex = [regex]::Matches($sha, '[A-F0-9]{64}') | Select-Object -Last 1
                if ($hex) { $sha = $hex.Value }
            }
            $backupOk = (Test-Path $bak) -and ($sha -match '^[A-F0-9]{64}$')
        } catch {
            $backupError = $_.Exception.Message
        }

        # Cleanup always
        sqlcmd -S $SqlInstance -E -b -Q @"
IF DB_ID(N'$ThrowawayDb') IS NOT NULL
BEGIN
  ALTER DATABASE [$ThrowawayDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE [$ThrowawayDb];
END
"@ | Out-Null
        $dbGone = $null -eq (sqlcmd -S $SqlInstance -E -h -1 -W -Q "SET NOCOUNT ON; SELECT DB_ID(N'$ThrowawayDb');")
        # sqlcmd returns whitespace; normalize
        $dbIdRaw = (sqlcmd -S $SqlInstance -E -h -1 -W -Q "SET NOCOUNT ON; SELECT ISNULL(DB_ID(N'$ThrowawayDb'), -1);").Trim()
        $dbDropped = ($dbIdRaw -eq '-1')

        if (Test-Path $bak) { Remove-Item -LiteralPath $bak -Force }
        $bakGone = -not (Test-Path $bak)

        return [ordered]@{
            BackupOk = [bool]$backupOk
            BackupSha256 = $sha
            BackupError = $backupError
            BakPathWas = $bak
            BakDeleted = $bakGone
            ThrowawayDbDropped = $dbDropped
            SqlTextOk = $true
            ProductionDbTouched = $false
        }
    }

    $bag.Remote = $remote
    $bag.Gate = if ($remote.BackupOk -and $remote.ThrowawayDbDropped -and $remote.BakDeleted) {
        'ProdDashboardExecV1IsolatedSqlBackupPassed'
    } else {
        'ProdDashboardExecV1IsolatedSqlBackupFailed'
    }
    $bag.FinishedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    ($bag | ConvertTo-Json -Depth 8) | Set-Content -Path $localEvidence -Encoding UTF8
    Write-Host ($bag | ConvertTo-Json -Depth 6)
    Write-Host "Evidence: $localEvidence"
    Write-Host "Gate=$($bag.Gate)"
    if ($bag.Gate -ne 'ProdDashboardExecV1IsolatedSqlBackupPassed') { exit 1 }
}
finally {
    if ($session) { Remove-PSSession $session -ErrorAction SilentlyContinue }
}
