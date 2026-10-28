<#
.SYNOPSIS
    ParkPalace CRM - SECURE-WINRM-01 Server Execution Script
    Configures WinRM HTTPS Listener (Port 5986) on Windows Server CRM (192.168.1.248)
    Maintains existing HTTP Listener (Port 5985) as safe fallback.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " ParkPalace CRM - SECURE-WINRM-01 Server Configuration" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# 1. Audit Existing Listeners
Write-Host "`n[Step 1] Auditing existing WinRM Listeners..." -ForegroundColor Yellow
$existingListeners = Get-ChildItem WSMan:\LocalHost\Listener
$existingListeners | Select-Object Name, Transport, Port, Address, Enabled, CertificateThumbprint | Format-Table -AutoSize

$httpListener = $existingListeners | Where-Object { $_.Transport -eq "HTTP" -and $_.Port -eq 5985 }
if ($httpListener) {
    Write-Host "  [OK] Existing HTTP Listener on port 5985 is ACTIVE. It will be preserved." -ForegroundColor Green
} else {
    Write-Host "  [WARN] Existing HTTP Listener on port 5985 was not found." -ForegroundColor Yellow
}

# 2. Issue Dedicated WinRM Certificate (SAN = crm-mgmt.sahar.loc, Server Authentication EKU, CA=FALSE)
Write-Host "`n[Step 2] Checking / Issuing dedicated WinRM HTTPS certificate..." -ForegroundColor Yellow
$subjectName = "CN=crm-mgmt.sahar.loc"
$existingCert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { 
    $_.Subject -eq $subjectName -and $_.NotAfter -gt (Get-Date).AddDays(30)
} | Select-Object -First 1

if (-not $existingCert) {
    Write-Host "  Generating new Self-Signed WinRM certificate with RSA 3072-bit..." -ForegroundColor Cyan
    $certParams = @{
        Subject           = $subjectName
        DnsName           = @("crm-mgmt.sahar.loc", "crm.sahar.loc", "94.182.47.116")
        KeyAlgorithm      = "RSA"
        KeyLength         = 3072
        HashAlgorithm     = "SHA256"
        KeyUsage          = "DigitalSignature", "KeyEncipherment"
        TextExtension     = @(
            "2.5.29.37={text}1.3.6.1.5.5.7.3.1", # Server Authentication
            "2.5.29.19={text}ca=0"                # Basic Constraints: CA=FALSE
        )
        CertStoreLocation = "Cert:\LocalMachine\My"
        NotAfter          = (Get-Date).AddDays(365)
        FriendlyName      = "ParkPalace CRM WinRM HTTPS Management Certificate"
    }
    $cert = New-SelfSignedCertificate @certParams
    Write-Host "  [OK] Certificate generated successfully." -ForegroundColor Green
} else {
    $cert = $existingCert
    Write-Host "  [OK] Using existing valid WinRM certificate." -ForegroundColor Green
}

Write-Host "  Certificate Thumbprint : $($cert.Thumbprint)" -ForegroundColor White
Write-Host "  Subject                : $($cert.Subject)" -ForegroundColor White
Write-Host "  Valid Until (NotAfter) : $($cert.NotAfter)" -ForegroundColor White

# 3. Export Public Certificate (Zero Private Keys)
$exportDir = "D:\ParkPlaceCRM"
if (-not (Test-Path $exportDir)) {
    $exportDir = "C:\ParkPlaceCRM"
    New-Item -ItemType Directory -Path $exportDir -Force | Out-Null
}
$exportPath = Join-Path $exportDir "crm-mgmt-winrm.cer"
Export-Certificate -Cert $cert -FilePath $exportPath -Type CERT -Force | Out-Null
Write-Host "  [OK] Public certificate exported to: $exportPath" -ForegroundColor Green

# 4. Configure WinRM HTTPS Listener on Port 5986
Write-Host "`n[Step 3] Configuring WinRM HTTPS Listener (Port 5986)..." -ForegroundColor Yellow
$httpsListener = Get-ChildItem WSMan:\LocalHost\Listener | Where-Object { $_.Transport -eq "HTTPS" -and $_.Port -eq 5986 }
if ($httpsListener) {
    Write-Host "  HTTPS listener on 5986 exists. Updating CertificateThumbprint..." -ForegroundColor Cyan
    Set-Item -Path "WSMan:\LocalHost\Listener\$($httpsListener.Name)\CertificateThumbprint" -Value $cert.Thumbprint
} else {
    Write-Host "  Creating new HTTPS listener on port 5986..." -ForegroundColor Cyan
    New-Item -Path WSMan:\LocalHost\Listener -Transport HTTPS -Address * -CertificateThumbprint $cert.Thumbprint -Port 5986 -Force | Out-Null
}
Write-Host "  [OK] WinRM HTTPS Listener is ACTIVE on port 5986." -ForegroundColor Green

# 5. Configure Windows Defender Firewall for Port 5986
Write-Host "`n[Step 4] Configuring Windows Defender Firewall for Port 5986..." -ForegroundColor Yellow
$ruleName = "WinRM-HTTPS-In-5986"
$existingRule = Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue
if (-not $existingRule) {
    New-NetFirewallRule -Name $ruleName `
        -DisplayName "Windows Remote Management (HTTPS-In 5986)" `
        -Description "Inbound rule for secure WinRM HTTPS management (SECURE-WINRM-01)" `
        -Direction Inbound `
        -Protocol TCP `
        -LocalPort 5986 `
        -Action Allow `
        -Profile Any `
        -Enabled True | Out-Null
    Write-Host "  [OK] Firewall rule created for TCP port 5986." -ForegroundColor Green
} else {
    Set-NetFirewallRule -Name $ruleName -Enabled True | Out-Null
    Write-Host "  [OK] Firewall rule already exists and is enabled." -ForegroundColor Green
}

# 6. Final Audit Summary
Write-Host "`n[Step 5] Final Active Listeners Status:" -ForegroundColor Yellow
Get-ChildItem WSMan:\LocalHost\Listener | Select-Object Name, Transport, Port, Address, Enabled, CertificateThumbprint | Format-Table -AutoSize

Write-Host "=================================================================" -ForegroundColor Green
Write-Host " SECURE-WINRM-01 Server Configuration Finished Successfully." -ForegroundColor Green
Write-Host " Copy '$exportPath' to client ORPC for trust establishment." -ForegroundColor White
Write-Host "=================================================================" -ForegroundColor Green
