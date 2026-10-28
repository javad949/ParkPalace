<#
.SYNOPSIS
    Controlled Production Deployment Script for ParkPalace CRM v1.0.7 (Catalog Standardization).
.DESCRIPTION
    Deploys ParkPalace CRM API and Web v1.0.7 to D:\ParkPlaceCRM on server 192.168.1.248.
    Preserves existing web.config, appsettings.Production.json, and operational authentication.
    Applies migration 20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits.
    Verifies that NO test data (TEST-DAM/TEST-SEP) is seeded into Production.
    Measures AppPool downtime, validates health, materials endpoint, and catalog schema.
#>

[CmdletBinding()]
param(
    [string]$ApiPackagePath = "d:\Antigravity\ParkPlace\release\phase7_release\ParkPalace_Api_v1.0.7.zip",
    [string]$ExpectedApiHash = "F7C14200C8E8307429F523F87DF60E7D849B521608C3A15B12B67EA9B9BE25E9",
    [string]$WebPackagePath = "d:\Antigravity\ParkPlace\release\phase7_release\ParkPalace_Web_v1.0.7.zip",
    [string]$ExpectedWebHash = "E424FECA636FB7C1D3448DA0E2BC67A5C635D9A1C18B9B73CB2C6FAEE76ED73A",
    [string]$SqlScriptPath = "d:\Antigravity\ParkPlace\release\phase7_release\20261024_Phase7_CatalogStandardization.sql",
    [string]$ExpectedSqlHash = "543922575D176196F961DAC10A8EE7233AB0A418C0513E1CD4BBF48F7E20C823",
    [string]$RemoteHost = "crm-mgmt.sahar.loc",
    [int]$RemotePort = 5987
)

$ErrorActionPreference = 'Stop'

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " ParkPalace CRM v1.0.7 - Controlled Production Deployment" -ForegroundColor Cyan
Write-Host " Target Host: $RemoteHost ($RemotePort) -> 192.168.1.248" -ForegroundColor Cyan
Write-Host " Target Database: ParkPalaceCRM" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Verify Local Package Hashes
Write-Host "`n[Step 1] Verifying local package hashes..." -ForegroundColor Yellow
$localApiHash = (Get-FileHash -Path $ApiPackagePath -Algorithm SHA256).Hash
$localWebHash = (Get-FileHash -Path $WebPackagePath -Algorithm SHA256).Hash
$localSqlHash = (Get-FileHash -Path $SqlScriptPath -Algorithm SHA256).Hash

Write-Host "  API Zip:  $localApiHash (Expected: $ExpectedApiHash)"
Write-Host "  Web Zip:  $localWebHash (Expected: $ExpectedWebHash)"
Write-Host "  SQL File: $localSqlHash (Expected: $ExpectedSqlHash)"

if ($localApiHash -ne $ExpectedApiHash) { throw "Local API zip hash mismatch! Aborting." }
if ($localWebHash -ne $ExpectedWebHash) { throw "Local Web zip hash mismatch! Aborting." }
if ($localSqlHash -ne $ExpectedSqlHash) { throw "Local SQL file hash mismatch! Aborting." }
Write-Host "  [OK] All local package hashes verified successfully." -ForegroundColor Green

# 2. Establish WinRM Session
Write-Host "`n[Step 2] Establishing secure WinRM session to https://${RemoteHost}:${RemotePort}/wsman..." -ForegroundColor Yellow
$secPass = ConvertTo-SecureString "OR@1405.srv" -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential ("CRM\Administrator", $secPass)
$so = New-PSSessionOption -SkipCACheck:$false -SkipCNCheck:$false -SkipRevocationCheck:$false

$session = New-PSSession -ConnectionUri "https://${RemoteHost}:${RemotePort}/wsman" -Credential $cred -SessionOption $so -Authentication Negotiate
Write-Host "  [OK] Secure WinRM session established." -ForegroundColor Green

try {
    # 3. Pre-Flight Remote Migration Status
    Write-Host "`n[Step 3] Checking remote migration status on ParkPalaceCRM..." -ForegroundColor Yellow
    $remoteMigrations = Invoke-Command -Session $session -ScriptBlock {
        sqlcmd -E -S localhost -d ParkPalaceCRM -Q "SET NOCOUNT ON; SELECT MigrationId FROM [__EFMigrationsHistory] ORDER BY MigrationId;"
    }
    Write-Host ($remoteMigrations | Out-String)
    
    $hasPhase6 = $remoteMigrations -match "20261018042000_Phase6_TelephonyAndCallManagement"
    $hasPhase7 = $remoteMigrations -match "20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits"
    
    if (-not $hasPhase6) {
        throw "Remote database is not at Phase 6! Cannot safely apply Phase 7 migration."
    }
    if ($hasPhase7) {
        Write-Host "  [NOTE] Phase 7 migration is already recorded in database. Migration step will be idempotent." -ForegroundColor Yellow
    } else {
        Write-Host "  [OK] Database is at Phase 6. Ready to apply Phase 7 migration." -ForegroundColor Green
    }

    # 4. Snapshot Backups of API and Web Folders
    Write-Host "`n[Step 4] Creating snapshot backups of current API and Web folders..." -ForegroundColor Yellow
    $backupResult = Invoke-Command -Session $session -ScriptBlock {
        $backupRoot = "D:\ParkPlaceCRM\backups"
        if (-not (Test-Path $backupRoot)) { New-Item -ItemType Directory -Path $backupRoot | Out-Null }
        
        $timestamp = (Get-Date).ToString("yyyyMMdd_HHmmss")
        $apiBackup = Join-Path $backupRoot "api_pre_v107_$timestamp"
        $webBackup = Join-Path $backupRoot "web_pre_v107_$timestamp"
        
        Copy-Item -Recurse -Path "D:\ParkPlaceCRM\api" -Destination $apiBackup
        Copy-Item -Recurse -Path "D:\ParkPlaceCRM\web" -Destination $webBackup
        
        [PSCustomObject]@{
            ApiBackup = $apiBackup
            WebBackup = $webBackup
            ApiFiles = (Get-ChildItem -Path $apiBackup -Recurse -File).Count
            WebFiles = (Get-ChildItem -Path $webBackup -Recurse -File).Count
        }
    }
    Write-Host "  [OK] API Snapshot: $($backupResult.ApiBackup) ($($backupResult.ApiFiles) files)" -ForegroundColor Green
    Write-Host "  [OK] Web Snapshot: $($backupResult.WebBackup) ($($backupResult.WebFiles) files)" -ForegroundColor Green

    # 5. Upload Packages and SQL Script to Remote Server
    Write-Host "`n[Step 5] Uploading packages and SQL migration to server temp directory..." -ForegroundColor Yellow
    $remoteApiZip = "C:\Windows\Temp\ParkPalace_Api_v1.0.7.zip"
    $remoteWebZip = "C:\Windows\Temp\ParkPalace_Web_v1.0.7.zip"
    $remoteSql = "C:\Windows\Temp\20261024_Phase7_CatalogStandardization.sql"
    
    Copy-Item -LiteralPath $ApiPackagePath -Destination $remoteApiZip -ToSession $session -Force
    Copy-Item -LiteralPath $WebPackagePath -Destination $remoteWebZip -ToSession $session -Force
    Copy-Item -LiteralPath $SqlScriptPath -Destination $remoteSql -ToSession $session -Force
    
    $remoteHashes = Invoke-Command -Session $session -ArgumentList $remoteApiZip, $remoteWebZip, $remoteSql -ScriptBlock {
        param($a, $w, $s)
        [PSCustomObject]@{
            ApiHash = (Get-FileHash -Path $a -Algorithm SHA256).Hash
            WebHash = (Get-FileHash -Path $w -Algorithm SHA256).Hash
            SqlHash = (Get-FileHash -Path $s -Algorithm SHA256).Hash
        }
    }
    
    Write-Host "  Remote API Hash: $($remoteHashes.ApiHash)"
    Write-Host "  Remote Web Hash: $($remoteHashes.WebHash)"
    Write-Host "  Remote SQL Hash: $($remoteHashes.SqlHash)"
    
    if ($remoteHashes.ApiHash -ne $ExpectedApiHash) { throw "Remote API package hash mismatch! Aborting." }
    if ($remoteHashes.WebHash -ne $ExpectedWebHash) { throw "Remote Web package hash mismatch! Aborting." }
    if ($remoteHashes.SqlHash -ne $ExpectedSqlHash) { throw "Remote SQL script hash mismatch! Aborting." }
    Write-Host "  [OK] Remote packages and script verified with 100% hash parity." -ForegroundColor Green

    # 6. Apply Database Migration
    Write-Host "`n[Step 6] Executing Phase 7 catalog migration on ParkPalaceCRM..." -ForegroundColor Yellow
    $migrationExecution = Invoke-Command -Session $session -ArgumentList $remoteSql -ScriptBlock {
        param($sqlFile)
        $output = sqlcmd -E -S localhost -d ParkPalaceCRM -i $sqlFile -b
        $lastExit = $LASTEXITCODE
        $newMigrations = sqlcmd -E -S localhost -d ParkPalaceCRM -Q "SET NOCOUNT ON; SELECT TOP 1 MigrationId FROM [__EFMigrationsHistory] ORDER BY MigrationId DESC;"
        
        [PSCustomObject]@{
            ExitCode = $lastExit
            Output = ($output -join "`n")
            LatestMigration = ($newMigrations -join "").Trim()
        }
    }
    Write-Host "  Migration Exit Code: $($migrationExecution.ExitCode)"
    Write-Host "  Latest Migration in DB: $($migrationExecution.LatestMigration)"
    
    if ($migrationExecution.ExitCode -ne 0 -or $migrationExecution.LatestMigration -notmatch "Phase7_CatalogStandardizationAndDecoupledUnits") {
        throw "SQL Migration execution failed! Aborting before file extraction."
    }
    Write-Host "  [OK] Phase 7 migration successfully applied and recorded in DB." -ForegroundColor Green

    # 7. Stop AppPool & Record Downtime Start
    Write-Host "`n[Step 7] Stopping AppPool 'ParkPalaceCRM' and recording downtime start..." -ForegroundColor Yellow
    $downtimeStart = Get-Date

    Invoke-Command -Session $session -ScriptBlock {
        Import-Module WebAdministration
        Stop-WebAppPool -Name "ParkPalaceCRM" -ErrorAction SilentlyContinue
        
        $timeout = 15
        $elapsed = 0
        while ((Get-WebAppPoolState -Name "ParkPalaceCRM").Value -ne "Stopped" -and $elapsed -lt $timeout) {
            Start-Sleep -Seconds 1
            $elapsed++
        }
    }

    # 8. Deploy API Package (Preserving Configs)
    Write-Host "`n[Step 8] Deploying API files (preserving web.config and appsettings.Production.json)..." -ForegroundColor Yellow
    Invoke-Command -Session $session -ArgumentList $remoteApiZip -ScriptBlock {
        param($zipPath)
        $apiDir = "D:\ParkPlaceCRM\api"
        $tempExtract = "C:\Windows\Temp\api_extract_v107"
        if (Test-Path $tempExtract) { Remove-Item -Recurse -Force $tempExtract }
        
        Expand-Archive -Path $zipPath -DestinationPath $tempExtract -Force
        
        # Preserve existing configs
        $preservedConfigs = @("appsettings.Production.json", "web.config")
        $savedConfigs = @{}
        foreach ($cfg in $preservedConfigs) {
            $src = Join-Path $apiDir $cfg
            if (Test-Path $src) {
                $savedConfigs[$cfg] = Get-Content -Raw -Path $src
            }
        }
        
        # Copy new files over API directory
        Copy-Item -Path "$tempExtract\*" -Destination $apiDir -Recurse -Force
        
        # Restore preserved configs
        foreach ($cfg in $savedConfigs.Keys) {
            $dst = Join-Path $apiDir $cfg
            Set-Content -Path $dst -Value $savedConfigs[$cfg] -Force
        }
        
        Remove-Item -Recurse -Force $tempExtract
        Remove-Item -Force $zipPath
    }
    Write-Host "  [OK] API package deployed with preserved configurations." -ForegroundColor Green

    # 9. Deploy Web Package (Preserving web.config)
    Write-Host "`n[Step 9] Deploying Web files (preserving web.config)..." -ForegroundColor Yellow
    Invoke-Command -Session $session -ArgumentList $remoteWebZip -ScriptBlock {
        param($zipPath)
        $webDir = "D:\ParkPlaceCRM\web"
        $tempExtract = "C:\Windows\Temp\web_extract_v107"
        if (Test-Path $tempExtract) { Remove-Item -Recurse -Force $tempExtract }
        
        Expand-Archive -Path $zipPath -DestinationPath $tempExtract -Force
        
        # Preserve web.config in web folder if present
        $webConfigPath = Join-Path $webDir "web.config"
        $savedWebConfig = $null
        if (Test-Path $webConfigPath) {
            $savedWebConfig = Get-Content -Raw -Path $webConfigPath
        }
        
        # Clean old assets folder to avoid stale chunk accumulation
        $assetsDir = Join-Path $webDir "assets"
        if (Test-Path $assetsDir) { Remove-Item -Recurse -Force $assetsDir }
        
        # Copy new web files
        Copy-Item -Path "$tempExtract\*" -Destination $webDir -Recurse -Force
        
        if ($savedWebConfig) {
            Set-Content -Path $webConfigPath -Value $savedWebConfig -Force
        }
        
        Remove-Item -Recurse -Force $tempExtract
        Remove-Item -Force $zipPath
    }
    Write-Host "  [OK] Web package deployed with clean assets and preserved web.config." -ForegroundColor Green

    # 10. Start AppPool & Calculate Downtime
    Write-Host "`n[Step 10] Starting AppPool 'ParkPalaceCRM'..." -ForegroundColor Yellow
    Invoke-Command -Session $session -ScriptBlock {
        Import-Module WebAdministration
        Start-WebAppPool -Name "ParkPalaceCRM" -ErrorAction SilentlyContinue
        
        $timeout = 15
        $elapsed = 0
        while ((Get-WebAppPoolState -Name "ParkPalaceCRM").Value -ne "Started" -and $elapsed -lt $timeout) {
            Start-Sleep -Seconds 1
            $elapsed++
        }
    }
    $downtimeEnd = Get-Date
    $downtimeDuration = [math]::Round(($downtimeEnd - $downtimeStart).TotalSeconds, 1)
    Write-Host "  [OK] AppPool started. Measured downtime: $downtimeDuration seconds." -ForegroundColor Green

    # 11. Post-Deployment Validation
    Write-Host "`n[Step 11] Executing post-deployment validation..." -ForegroundColor Yellow
    $validation = Invoke-Command -Session $session -ScriptBlock {
        Import-Module WebAdministration
        $poolState = (Get-WebAppPoolState -Name "ParkPalaceCRM").Value
        
        # Health check
        $health = Invoke-RestMethod -Uri "https://crm.sahar.loc/api/health" -UseDefaultCredentials -TimeoutSec 15
        
        # Web status
        $web = Invoke-WebRequest -Uri "https://crm.sahar.loc/" -UseBasicParsing -TimeoutSec 15
        
        # Materials API check (Windows authenticated)
        $materials = Invoke-RestMethod -Uri "https://crm.sahar.loc/api/v1/materials?page=1&pageSize=5" -UseDefaultCredentials -TimeoutSec 15
        
        # Ensure NO test items (TEST-DAM / TEST-SEP) exist in DB
        $testDamCount = sqlcmd -E -S localhost -d ParkPalaceCRM -h -1 -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM Materials WHERE InternalCode LIKE 'TEST-%';"
        $testSepCount = sqlcmd -E -S localhost -d ParkPalaceCRM -h -1 -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM InventoryItemMappings WHERE SepidarItemId LIKE 'TEST-%';"
        
        # Check that new columns exist on real materials
        $colCheck = sqlcmd -E -S localhost -d ParkPalaceCRM -h -1 -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('Materials') AND name IN ('CategorizationStatus', 'ConsumptionUnitId', 'IsCustomizedByUser', 'PackageSize', 'PurchaseUnitId');"
        
        [PSCustomObject]@{
            AppPoolState = $poolState
            HealthStatus = $health.status
            HealthVersion = $health.version
            WebStatusCode = $web.StatusCode
            TotalMaterials = $materials.totalCount
            FirstMaterialName = $materials.items[0].name
            FirstMaterialCategory = $materials.items[0].categoryName
            FirstMaterialBaseUnit = $materials.items[0].baseUnitName
            TestDamItemsInDb = [int]($testDamCount -join "").Trim()
            TestSepItemsInDb = [int]($testSepCount -join "").Trim()
            NewColumnsCount = [int]($colCheck -join "").Trim()
        }
    }

    Write-Host "`n=================== POST-DEPLOYMENT AUDIT ===================" -ForegroundColor Cyan
    Write-Host "  AppPool State:              $($validation.AppPoolState)"
    Write-Host "  API Health Status:          $($validation.HealthStatus)"
    Write-Host "  API Health Version:         $($validation.HealthVersion)"
    Write-Host "  Web HTTP Status:            $($validation.WebStatusCode)"
    Write-Host "  Total Materials in DB:      $($validation.TotalMaterials)"
    Write-Host "  Sample Material:            $($validation.FirstMaterialName) ($($validation.FirstMaterialCategory))"
    Write-Host "  Material Base Unit:         $($validation.FirstMaterialBaseUnit)"
    Write-Host "  TEST-DAM Items in Prod DB:  $($validation.TestDamItemsInDb) (MUST BE 0)"
    Write-Host "  TEST-SEP Mappings in DB:    $($validation.TestSepItemsInDb) (MUST BE 0)"
    Write-Host "  Catalog New Columns Found:  $($validation.NewColumnsCount)/5"
    Write-Host "  Measured AppPool Downtime:  $downtimeDuration seconds"
    Write-Host "=============================================================" -ForegroundColor Cyan

    if ($validation.AppPoolState -ne "Started") { throw "Validation failed: AppPool is not Started" }
    if ($validation.HealthStatus -ne "Healthy") { throw "Validation failed: API is not Healthy" }
    if ($validation.WebStatusCode -ne 200) { throw "Validation failed: Web status code is not 200" }
    if ($validation.TestDamItemsInDb -ne 0) { throw "CRITICAL: TEST-DAM items were detected in production database!" }
    if ($validation.TestSepItemsInDb -ne 0) { throw "CRITICAL: TEST-SEP mappings were detected in production database!" }
    if ($validation.NewColumnsCount -ne 5) { throw "Validation failed: Not all Phase 7 catalog columns were created!" }

    Write-Host "`n[SUCCESS] Deployment of ParkPalace CRM v1.0.7 completed with 100% validation pass." -ForegroundColor Green
}
catch {
    Write-Host "`n[ERROR] Deployment failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Consult rollback runbook. Do NOT run Down migration automatically." -ForegroundColor Red
    throw
}
finally {
    if ($session) {
        Remove-PSSession -Session $session
    }
}
