$ErrorActionPreference = 'Stop'

$secPass = ConvertTo-SecureString "OR@1405.srv" -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential ("CRM\Administrator", $secPass)
$so = New-PSSessionOption -SkipCACheck:$false -SkipCNCheck:$false -SkipRevocationCheck:$false

$session = New-PSSession -ConnectionUri "https://crm-mgmt.sahar.loc:5987/wsman" -Credential $cred -SessionOption $so -Authentication Negotiate

try {
    # 1. Binary Hash Verification on Remote Server
    Write-Host "=== 1. REMOTE BINARY HASH VERIFICATION ==="
    $remoteHashes = Invoke-Command -Session $session -ScriptBlock {
        Get-ChildItem "D:\ParkPlaceCRM\api\ParkPalace*.dll" | ForEach-Object {
            [PSCustomObject]@{
                FileName = $_.Name
                SHA256 = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash
                Length = $_.Length
                LastWriteTime = $_.LastWriteTime
            }
        }
    }
    $remoteHashes | Format-Table -AutoSize | Out-String | Write-Host

    # 2. Compare with local zip package binaries
    Write-Host "=== 2. LOCAL ZIP BINARY HASHES ==="
    $tempInspect = "d:\Antigravity\ParkPlace\scratch\inspect_zip"
    if (Test-Path $tempInspect) { Remove-Item -Recurse -Force $tempInspect }
    Expand-Archive -Path "d:\Antigravity\ParkPlace\dist\release_v1.0.4\ParkPalaceCRM_Api_v1.0.4.zip" -DestinationPath $tempInspect -Force
    $zipHashes = Get-ChildItem "$tempInspect\ParkPalace*.dll" | ForEach-Object {
        [PSCustomObject]@{
            FileName = $_.Name
            SHA256 = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash
            Length = $_.Length
        }
    }
    $zipHashes | Format-Table -AutoSize | Out-String | Write-Host
    Remove-Item -Recurse -Force $tempInspect

    # 3. Check AppPool, Web, and Config Preservation
    Write-Host "=== 3. IIS, APPPOOL & CONFIG INTEGRITY ==="
    $iisStatus = Invoke-Command -Session $session -ScriptBlock {
        Import-Module WebAdministration
        $pool = (Get-WebAppPoolState -Name "ParkPalaceCRM").Value
        
        # Test web response
        $webRes = Invoke-WebRequest -Uri "https://crm.sahar.loc/" -UseBasicParsing -TimeoutSec 10
        
        # Test API health
        $health = Invoke-RestMethod -Uri "https://crm.sahar.loc/api/health" -UseDefaultCredentials -TimeoutSec 10
        
        # Check preserved config timestamps & hashes vs backup
        $backupDir = "D:\ParkPlaceCRM\backups\api_pre_v104_20261023_222933"
        $prodJsonRemote = (Get-FileHash -Path "D:\ParkPlaceCRM\api\appsettings.Production.json" -Algorithm SHA256).Hash
        $prodJsonBackup = (Get-FileHash -Path "$backupDir\appsettings.Production.json" -Algorithm SHA256).Hash
        $webConfigRemote = (Get-FileHash -Path "D:\ParkPlaceCRM\api\web.config" -Algorithm SHA256).Hash
        $webConfigBackup = (Get-FileHash -Path "$backupDir\web.config" -Algorithm SHA256).Hash

        [PSCustomObject]@{
            AppPoolState = $pool
            WebHttpStatus = $webRes.StatusCode
            WebContentLength = $webRes.RawContentLength
            ApiHealthStatus = $health.status
            ApiDatabaseConnected = $health.databaseConnected
            ApiEnvironment = $health.environment
            ProdJsonPreserved = ($prodJsonRemote -eq $prodJsonBackup)
            WebConfigPreserved = ($webConfigRemote -eq $webConfigBackup)
            ProdJsonHash = $prodJsonRemote
            WebConfigHash = $webConfigRemote
        }
    }
    $iisStatus | Format-List | Out-String | Write-Host

    # 4. Strict RBAC Real HTTP Status Verification (Read-Only)
    Write-Host "=== 4. RBAC LIVE HTTP STATUS VERIFICATION ==="
    $rbacResults = Invoke-Command -Session $session -ScriptBlock {
        $endpoints = @(
            @{ Name = "Health Check"; Role = "Anonymous"; Method = "GET"; Uri = "https://crm.sahar.loc/api/health"; Expected = 200; Headers = @{} },
            @{ Name = "Identity Check"; Role = "Admin"; Method = "GET"; Uri = "https://crm.sahar.loc/api/me"; Expected = 200; Headers = @{} },
            @{ Name = "Catalog Materials"; Role = "Admin"; Method = "GET"; Uri = "https://crm.sahar.loc/api/v1/materials"; Expected = 200; Headers = @{} },
            @{ Name = "Anonymous /api/me"; Role = "Anonymous"; Method = "GET"; Uri = "https://crm.sahar.loc/api/me"; Expected = 401; Headers = @{}; Anonymous = $true },
            @{ Name = "Procurement Orders"; Role = "Procurement"; Method = "GET"; Uri = "https://crm.sahar.loc/api/v1/procurement/orders"; Expected = 200; Headers = @{ "X-Active-Role" = "Procurement" } },
            @{ Name = "Procurement Dashboard"; Role = "Procurement"; Method = "GET"; Uri = "https://crm.sahar.loc/api/v1/reports/role-dashboard/Procurement"; Expected = 200; Headers = @{ "X-Active-Role" = "Procurement" } },
            @{ Name = "Procurement -> Finance Dashboard"; Role = "Procurement"; Method = "GET"; Uri = "https://crm.sahar.loc/api/v1/reports/role-dashboard/Finance"; Expected = 403; Headers = @{ "X-Active-Role" = "Procurement" } },
            @{ Name = "Warehouse Inventory"; Role = "Warehouse"; Method = "GET"; Uri = "https://crm.sahar.loc/api/v1/inventory"; Expected = 200; Headers = @{ "X-Active-Role" = "Warehouse" } },
            @{ Name = "Warehouse Receipts"; Role = "Warehouse"; Method = "GET"; Uri = "https://crm.sahar.loc/api/v1/warehouse/receipts"; Expected = 200; Headers = @{ "X-Active-Role" = "Warehouse" } },
            @{ Name = "Warehouse -> Finance Invoices"; Role = "Warehouse"; Method = "GET"; Uri = "https://crm.sahar.loc/api/v1/finance/invoices"; Expected = 403; Headers = @{ "X-Active-Role" = "Warehouse" } },
            @{ Name = "Warehouse -> Finance Dashboard"; Role = "Warehouse"; Method = "GET"; Uri = "https://crm.sahar.loc/api/v1/reports/role-dashboard/Finance"; Expected = 403; Headers = @{ "X-Active-Role" = "Warehouse" } }
        )

        $list = @()
        foreach ($ep in $endpoints) {
            $actualStatus = 0
            $errorMsg = ""
            try {
                $params = @{
                    Uri = $ep.Uri
                    Method = $ep.Method
                    TimeoutSec = 10
                }
                if ($ep.Headers.Count -gt 0) { $params["Headers"] = $ep.Headers }
                if (-not $ep.Anonymous) { $params["UseDefaultCredentials"] = $true }

                $resp = Invoke-RestMethod @params
                $actualStatus = 200
            }
            catch {
                if ($_.Exception.Response) {
                    $actualStatus = [int]$_.Exception.Response.StatusCode
                } else {
                    $actualStatus = 0
                    $errorMsg = $_.Exception.Message
                }
            }

            $verdict = if ($actualStatus -eq $ep.Expected) { "PASS" } else { "FAIL" }
            $list += [PSCustomObject]@{
                Endpoint = $ep.Name
                ActiveRole = $ep.Role
                Uri = $ep.Uri
                ExpectedHTTP = $ep.Expected
                ActualHTTP = $actualStatus
                Verdict = $verdict
                Error = $errorMsg
            }
        }
        $list
    }
    $rbacResults | Format-Table -AutoSize | Out-String | Write-Host
}
finally {
    Remove-PSSession -Session $session
}
