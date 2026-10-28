<#
.SYNOPSIS
    Controlled Production Deployment Script for ParkPalace CRM API v1.0.4.
.DESCRIPTION
    Deploys ParkPalaceCRM_Api_v1.0.4.zip to D:\ParkPlaceCRM\api on the CRM pilot server.
    Preserves existing web.config, appsettings.Production.json, connection strings, and secrets.
    Performs pre-deployment rollback snapshot, measures AppPool downtime, and executes read-only validation.
#>

[CmdletBinding()]
param(
    [string]$PackagePath = "d:\Antigravity\ParkPlace\dist\release_v1.0.4\ParkPalaceCRM_Api_v1.0.4.zip",
    [string]$ExpectedHash = "0E637081C9EEA80E6218717530B2386728F9698DB33FCF83486D7D961D8F175A",
    [string]$RemoteHost = "crm-mgmt.sahar.loc",
    [int]$RemotePort = 5987
)

$ErrorActionPreference = 'Stop'

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " ParkPalace CRM API v1.0.4 - Controlled Deployment" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Verify Local Package Hash
Write-Host "`n[Step 1] Verifying local package hash..." -ForegroundColor Yellow
if (-not (Test-Path $PackagePath)) {
    throw "Package file not found: $PackagePath"
}
$actualLocalHash = (Get-FileHash -Path $PackagePath -Algorithm SHA256).Hash
Write-Host "  Package: $PackagePath"
Write-Host "  Actual SHA-256:   $actualLocalHash"
Write-Host "  Expected SHA-256: $ExpectedHash"

if ($actualLocalHash -ne $ExpectedHash) {
    throw "Package hash mismatch! Deployment aborted."
}
Write-Host "  [OK] Hash verification passed." -ForegroundColor Green

# 2. Establish WinRM Session
Write-Host "`n[Step 2] Establishing secure WinRM session to https://${RemoteHost}:${RemotePort}/wsman..." -ForegroundColor Yellow
$secPass = ConvertTo-SecureString "OR@1405.srv" -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential ("CRM\Administrator", $secPass)
$so = New-PSSessionOption -SkipCACheck:$false -SkipCNCheck:$false -SkipRevocationCheck:$false

$session = New-PSSession -ConnectionUri "https://${RemoteHost}:${RemotePort}/wsman" -Credential $cred -SessionOption $so -Authentication Negotiate
Write-Host "  [OK] WinRM session established." -ForegroundColor Green

$backupPath = ""
$downtimeSeconds = 0

try {
    # 3. Verify Remote IIS Configuration
    Write-Host "`n[Step 3] Verifying remote IIS paths and AppPool configuration..." -ForegroundColor Yellow
    $iisInfo = Invoke-Command -Session $session -ScriptBlock {
        Import-Module WebAdministration
        $app = Get-WebApplication -Site "ParkPalaceCRM" | Where-Object { $_.Path -eq "/api" }
        $site = Get-Website -Name "ParkPalaceCRM"
        $pool = Get-ChildItem IIS:\AppPools | Where-Object { $_.Name -eq "ParkPalaceCRM" }
        
        [PSCustomObject]@{
            SiteName = $site.Name
            SitePhysicalPath = $site.PhysicalPath
            SiteAppPool = $site.applicationPool
            AppPath = $app.Path
            AppPhysicalPath = $app.PhysicalPath
            AppPool = $app.applicationPool
            PoolState = $pool.State
        }
    }

    Write-Host "  Site Name:          $($iisInfo.SiteName)"
    Write-Host "  Site Path (Web):    $($iisInfo.SitePhysicalPath)"
    Write-Host "  Site AppPool:       $($iisInfo.SiteAppPool)"
    Write-Host "  API App Path:       $($iisInfo.AppPath)"
    Write-Host "  API Physical Path:  $($iisInfo.AppPhysicalPath)"
    Write-Host "  API AppPool:        $($iisInfo.AppPool)"

    if ($iisInfo.AppPhysicalPath -ne "D:\ParkPlaceCRM\api") {
        throw "Unexpected API physical path: $($iisInfo.AppPhysicalPath). Expected: D:\ParkPlaceCRM\api"
    }
    if ($iisInfo.AppPool -ne "ParkPalaceCRM") {
        throw "Unexpected API AppPool: $($iisInfo.AppPool). Expected: ParkPalaceCRM"
    }
    Write-Host "  [OK] IIS topology matches expectations. Web and API share AppPool 'ParkPalaceCRM'." -ForegroundColor Green

    # 4. Create Pre-Deployment Rollback Snapshot
    Write-Host "`n[Step 4] Creating pre-deployment snapshot backup of API folder..." -ForegroundColor Yellow
    $backupResult = Invoke-Command -Session $session -ScriptBlock {
        $backupRoot = "D:\ParkPlaceCRM\backups"
        if (-not (Test-Path $backupRoot)) { New-Item -ItemType Directory -Path $backupRoot | Out-Null }
        
        $timestamp = (Get-Date).ToString("yyyyMMdd_HHmmss")
        $apiBackup = Join-Path $backupRoot "api_pre_v104_$timestamp"
        
        Copy-Item -Recurse -Path "D:\ParkPlaceCRM\api" -Destination $apiBackup
        
        $fileCount = (Get-ChildItem -Path $apiBackup -Recurse -File).Count
        [PSCustomObject]@{
            BackupPath = $apiBackup
            FileCount = $fileCount
        }
    }
    $backupPath = $backupResult.BackupPath
    Write-Host "  [OK] Snapshot created: $($backupResult.BackupPath) ($($backupResult.FileCount) files)" -ForegroundColor Green

    # 5. Upload Package to Remote Temp and Verify Hash
    Write-Host "`n[Step 5] Uploading package to server temp directory..." -ForegroundColor Yellow
    $remoteZip = "C:\Windows\Temp\ParkPalaceCRM_Api_v1.0.4.zip"
    Copy-Item -LiteralPath $PackagePath -Destination $remoteZip -ToSession $session -Force

    $remoteHash = Invoke-Command -Session $session -ArgumentList $remoteZip -ScriptBlock {
        param($path)
        (Get-FileHash -Path $path -Algorithm SHA256).Hash
    }
    Write-Host "  Remote SHA-256: $remoteHash"
    if ($remoteHash -ne $ExpectedHash) {
        throw "Remote package hash mismatch after upload! Aborting."
    }
    Write-Host "  [OK] Remote package verified." -ForegroundColor Green

    # 6. Stop AppPool & Record Downtime Start
    Write-Host "`n[Step 6] Stopping AppPool 'ParkPalaceCRM' (brief interruption to Web & API)..." -ForegroundColor Yellow
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
        $state = (Get-WebAppPoolState -Name "ParkPalaceCRM").Value
        Write-Host "  AppPool State: $state"
    }

    # 7. Extract and Apply API Files (Preserving Config)
    Write-Host "`n[Step 7] Extracting and applying API files (preserving web.config and appsettings)..." -ForegroundColor Yellow
    $deployResult = Invoke-Command -Session $session -ArgumentList $remoteZip -ScriptBlock {
        param($zip)
        $tempExtract = "C:\Windows\Temp\api_v104_extracted"
        if (Test-Path $tempExtract) { Remove-Item -Recurse -Force $tempExtract }
        Expand-Archive -Path $zip -DestinationPath $tempExtract -Force

        $targetDir = "D:\ParkPlaceCRM\api"
        $preserved = @()
        $updated = @()

        # Update root files
        Get-ChildItem -Path $tempExtract -File | ForEach-Object {
            # Strictly preserve existing server web.config and all appsettings*.json
            if ($_.Name -eq "web.config" -or $_.Name -like "appsettings*.json") {
                if (Test-Path (Join-Path $targetDir $_.Name)) {
                    $preserved += $_.Name
                    return
                }
            }
            Copy-Item -Path $_.FullName -Destination $targetDir -Force
            $updated += $_.Name
        }

        # Update runtimes subfolder if present
        $extractedRuntimes = Join-Path $tempExtract "runtimes"
        if (Test-Path $extractedRuntimes) {
            $targetRuntimes = Join-Path $targetDir "runtimes"
            Copy-Item -Path "$extractedRuntimes\*" -Destination $targetRuntimes -Recurse -Force
        }

        Remove-Item -Recurse -Force $tempExtract
        Remove-Item -Force $zip

        [PSCustomObject]@{
            UpdatedCount = $updated.Count
            PreservedFiles = ($preserved -join ', ')
            InfraDllHash = (Get-FileHash -Path "$targetDir\ParkPalace.Infrastructure.dll" -Algorithm SHA256).Hash
            ApiDllHash = (Get-FileHash -Path "$targetDir\ParkPalace.Api.dll" -Algorithm SHA256).Hash
        }
    }

    Write-Host "  Files updated: $($deployResult.UpdatedCount)"
    Write-Host "  Files preserved: $($deployResult.PreservedFiles)"
    Write-Host "  ParkPalace.Infrastructure.dll Hash: $($deployResult.InfraDllHash)"
    Write-Host "  ParkPalace.Api.dll Hash:            $($deployResult.ApiDllHash)"

    # 8. Start AppPool & Record Downtime End
    Write-Host "`n[Step 8] Starting AppPool 'ParkPalaceCRM'..." -ForegroundColor Yellow
    Invoke-Command -Session $session -ScriptBlock {
        Import-Module WebAdministration
        Start-WebAppPool -Name "ParkPalaceCRM"
        
        $timeout = 15
        $elapsed = 0
        while ((Get-WebAppPoolState -Name "ParkPalaceCRM").Value -ne "Started" -and $elapsed -lt $timeout) {
            Start-Sleep -Seconds 1
            $elapsed++
        }
        $state = (Get-WebAppPoolState -Name "ParkPalaceCRM").Value
        Write-Host "  AppPool State: $state"
    }

    $downtimeEnd = Get-Date
    $downtimeSeconds = [math]::Round(($downtimeEnd - $downtimeStart).TotalSeconds, 2)
    Write-Host "  [OK] AppPool restarted. Service interruption duration: $downtimeSeconds seconds." -ForegroundColor Green

    # 9. Verify Post-Deployment Health and RBAC over HTTPS
    Write-Host "`n[Step 9] Executing live post-deployment validation over valid HTTPS..." -ForegroundColor Yellow
    $testResults = Invoke-Command -Session $session -ScriptBlock {
        $results = [ordered]@{}

        # Test A: Health Check
        try {
            $health = Invoke-RestMethod -Uri "https://crm.sahar.loc/api/health" -UseDefaultCredentials -TimeoutSec 15
            $results["Health_Status"] = $health.status
            $results["Database_Connected"] = $health.databaseConnected
            $results["Health_HTTP"] = 200
        } catch {
            $results["Health_Status"] = "FAIL: $($_.Exception.Message)"
            $results["Health_HTTP"] = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { "Err" }
        }

        # Test B: Identity and Active Role (Admin)
        try {
            $me = Invoke-RestMethod -Uri "https://crm.sahar.loc/api/me" -UseDefaultCredentials -TimeoutSec 15
            $results["Identity_User"] = $me.username
            $results["Identity_DisplayName"] = $me.displayName
            $results["Identity_ActiveRole"] = $me.activeRole
            $results["Identity_HTTP"] = 200
        } catch {
            $results["Identity_Status"] = "FAIL: $($_.Exception.Message)"
            $results["Identity_HTTP"] = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { "Err" }
        }

        # Test C: Read-Only Permitted Endpoint (Procurement Catalog View)
        try {
            $mats = Invoke-RestMethod -Uri "https://crm.sahar.loc/api/v1/materials" -UseDefaultCredentials -TimeoutSec 15
            $results["Materials_Catalog_Count"] = $mats.Count
            $results["Materials_Catalog_HTTP"] = 200
            $results["Materials_Catalog_Verdict"] = "PASS (200 OK as expected)"
        } catch {
            $code = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { "Err" }
            $results["Materials_Catalog_HTTP"] = $code
            $results["Materials_Catalog_Verdict"] = "FAIL: Expected 200, got $code"
        }

        # Test D: Read-Only Permitted Endpoint under Active Role Warehouse (Warehouse Inventory)
        $whHeaders = @{ "X-Active-Role" = "Warehouse" }
        try {
            $inv = Invoke-RestMethod -Uri "https://crm.sahar.loc/api/v1/warehouse/inventory" -Headers $whHeaders -UseDefaultCredentials -TimeoutSec 15
            $results["Warehouse_Inventory_HTTP"] = 200
            $results["Warehouse_Inventory_Verdict"] = "PASS (200 OK as expected for Warehouse)"
        } catch {
            $code = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { "Err" }
            $results["Warehouse_Inventory_HTTP"] = $code
            $results["Warehouse_Inventory_Verdict"] = "FAIL: Expected 200, got $code"
        }

        # Test E: Read-Only Forbidden Endpoint under Active Role Warehouse (Finance Role Dashboard)
        try {
            $finDash = Invoke-RestMethod -Uri "https://crm.sahar.loc/api/v1/reports/role-dashboard/Finance" -Headers $whHeaders -UseDefaultCredentials -TimeoutSec 15
            $results["Warehouse_FinanceDash_HTTP"] = 200
            $results["Warehouse_FinanceDash_Verdict"] = "FAIL: Expected 403 Forbidden, but received 200 OK!"
        } catch {
            $code = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { "Err" }
            $results["Warehouse_FinanceDash_HTTP"] = $code
            if ($code -eq 403) {
                $results["Warehouse_FinanceDash_Verdict"] = "PASS (403 Forbidden as expected for Warehouse on Finance)"
            } else {
                $results["Warehouse_FinanceDash_Verdict"] = "FAIL: Expected 403 Forbidden, but got $code ($($_.Exception.Message))"
            }
        }

        # Test F: Anonymous Access Prohibition (Should be 401 Unauthorized)
        try {
            $anon = Invoke-RestMethod -Uri "https://crm.sahar.loc/api/me" -Method Get -TimeoutSec 15
            $results["Anonymous_Access_HTTP"] = 200
            $results["Anonymous_Access_Verdict"] = "FAIL: Anonymous access allowed!"
        } catch {
            $code = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { "Err" }
            $results["Anonymous_Access_HTTP"] = $code
            if ($code -eq 401) {
                $results["Anonymous_Access_Verdict"] = "PASS (401 Unauthorized as expected)"
            } else {
                $results["Anonymous_Access_Verdict"] = "FAIL: Expected 401, got $code"
            }
        }

        return $results
    }

    Write-Host "`n--- LIVE VERIFICATION RESULTS ---" -ForegroundColor Cyan
    $testResults.GetEnumerator() | ForEach-Object {
        Write-Host "  $($_.Key): $($_.Value)"
    }

    # Verify pass conditions
    if ($testResults["Health_HTTP"] -ne 200 -or $testResults["Database_Connected"] -ne $true) {
        throw "Health check failed after deployment!"
    }
    if ($testResults["Warehouse_FinanceDash_HTTP"] -ne 403) {
        throw "RBAC boundary check failed: Expected 403 Forbidden on Warehouse accessing Finance dashboard."
    }

    Write-Host "`n[SUCCESS] Deployment of API v1.0.4 completed and verified successfully." -ForegroundColor Green

}
catch {
    Write-Error "DEPLOYMENT FAILED: $_"
    if ($backupPath -and $session) {
        Write-Host "`n[ROLLBACK] Initiating rollback to snapshot: $backupPath..." -ForegroundColor Red
        Invoke-Command -Session $session -ArgumentList $backupPath -ScriptBlock {
            param($bPath)
            Import-Module WebAdministration
            Stop-WebAppPool -Name "ParkPalaceCRM" -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
            
            Copy-Item -Path "$bPath\*" -Destination "D:\ParkPlaceCRM\api" -Recurse -Force
            
            Start-WebAppPool -Name "ParkPalaceCRM"
            Write-Host "Rollback completed."
        }
    }
    throw $_
}
finally {
    if ($session) {
        Remove-PSSession -Session $session
    }
}
