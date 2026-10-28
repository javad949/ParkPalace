<#
.SYNOPSIS
    ParkPalace CRM - SECURE-WINRM-01 Client Validation Script (ORPC)
    Trusts the public certificate, maps hostname, and tests WinRM HTTPS connection.
    STRICT SECURITY: Enforces SkipCACheck:False, SkipCNCheck:False. Zero credentials logged.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$PublicCertPath = "d:\ParkPlace\scripts\crm-mgmt-winrm.cer",

    [Parameter(Mandatory = $false)]
    [string]$ServerHostName = "crm-mgmt.sahar.loc",

    [Parameter(Mandatory = $false)]
    [int]$HttpsPort = 5987,

    [Parameter(Mandatory = $false)]
    [string]$PublicIP = "94.182.47.116"
)

$ErrorActionPreference = "Stop"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " ParkPalace CRM - SECURE-WINRM-01 Client Validation (ORPC)" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# 1. Verify Public Certificate in Trusted Root
if (Test-Path $PublicCertPath) {
    Write-Host "`n[Step 1] Importing public certificate into Cert:\LocalMachine\Root..." -ForegroundColor Yellow
    $cert = Import-Certificate -FilePath $PublicCertPath -CertStoreLocation "Cert:\LocalMachine\Root"
    Write-Host "  [OK] Certificate imported. Thumbprint: $($cert.Thumbprint)" -ForegroundColor Green
} else {
    Write-Host "`n[Step 1] Checking existing certificates in Cert:\LocalMachine\Root..." -ForegroundColor Yellow
    $matchingCert = Get-ChildItem Cert:\LocalMachine\Root | Where-Object { $_.Subject -like "*$ServerHostName*" } | Select-Object -First 1
    if ($matchingCert) {
        Write-Host "  [OK] Found trusted certificate in Root store: $($matchingCert.Thumbprint)" -ForegroundColor Green
    } else {
        Write-Host "  [WARN] Public certificate '$PublicCertPath' not found and not in Root store." -ForegroundColor Yellow
        Write-Host "         Please provide the public .cer file exported from CRM." -ForegroundColor Yellow
    }
}

# 2. Verify or Update Hosts Mapping
Write-Host "`n[Step 2] Verifying hosts file mapping ($PublicIP -> $ServerHostName)..." -ForegroundColor Yellow
$hostsPath = "$env:windir\System32\drivers\etc\hosts"
$hostsContent = Get-Content $hostsPath -Raw -ErrorAction SilentlyContinue
$mappingLine = "$PublicIP    $ServerHostName"

if ($hostsContent -notmatch [regex]::Escape($ServerHostName)) {
    Write-Host "  Adding mapping to hosts file..." -ForegroundColor Cyan
    Add-Content -Path $hostsPath -Value "`n$mappingLine" -Force
    Write-Host "  [OK] Added: $mappingLine" -ForegroundColor Green
} else {
    Write-Host "  [OK] Hosts file already contains mapping for $ServerHostName." -ForegroundColor Green
}

# 3. Test L4 TCP Connectivity to HTTPS Port
Write-Host "`n[Step 3] Testing TCP connectivity to $PublicIP on port $HttpsPort..." -ForegroundColor Yellow
$tcpTest = Test-NetConnection -ComputerName $PublicIP -Port $HttpsPort
if ($tcpTest.TcpTestSucceeded) {
    Write-Host "  [OK] TCP port $HttpsPort is OPEN and reachable!" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] TCP port $HttpsPort is CLOSED or unreachable." -ForegroundColor Red
    Write-Host "         Ensure the MikroTik NAT rule (TCP 5987 -> 192.168.1.248:5986) has been applied." -ForegroundColor Yellow
    return
}

# 4. Strict WinRM HTTPS Session Validation
Write-Host "`n[Step 4] Testing WinRM HTTPS authentication and remote execution..." -ForegroundColor Yellow
Write-Host "  Prompting for CRM administrative credentials (in-memory only, zero disk logging)..." -ForegroundColor Cyan
$cred = Get-Credential

# STRICT: SkipCACheck:False, SkipCNCheck:False, SkipRevocationCheck:False
$sessionOption = New-PSSessionOption -SkipCACheck:$false -SkipCNCheck:$false -SkipRevocationCheck:$false
$connectionUri = "https://$($ServerHostName):$($HttpsPort)/wsman"

Write-Host "  Connecting to $connectionUri with strict TLS validation..." -ForegroundColor Cyan

try {
    $session = New-PSSession -ConnectionUri $connectionUri `
        -Credential $cred `
        -SessionOption $sessionOption `
        -Authentication Negotiate

    Write-Host "  [OK] WinRM HTTPS PSSession established successfully!" -ForegroundColor Green

    # Step 5: Read-Only Verification Command
    Write-Host "`n[Step 5] Running Read-Only verification command on CRM..." -ForegroundColor Yellow
    $remoteResult = Invoke-Command -Session $session -ScriptBlock {
        $os = Get-CimInstance Win32_OperatingSystem
        $listeners = & winrm enumerate winrm/config/listener
        [PSCustomObject]@{
            ComputerName   = $env:COMPUTERNAME
            OSCaption      = $os.Caption
            OSArchitecture = $os.OSArchitecture
            Version        = $os.Version
            ActiveListeners= ($listeners -join "`n")
            TimestampUtc   = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        }
    }

    $remoteResult | Format-List
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host " SECURE-WINRM-01 Verification PASSED (100% Strict TLS Validation)" -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Green

    Remove-PSSession -Session $session
}
catch {
    Write-Host "  [FAIL] WinRM HTTPS Connection failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "         Review certificate SAN, trust store, and network connectivity." -ForegroundColor Yellow
}
