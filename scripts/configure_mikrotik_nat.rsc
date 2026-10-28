# ==============================================================================
# ParkPalace CRM - MikroTik RouterOS NAT Configuration Script
# Task: SECURE-WINRM-01
# Target: Forward external TCP 5987 to internal CRM 192.168.1.248:5986 (WinRM HTTPS)
# NOTE: Preserves existing NAT 5986 -> 192.168.1.248:5985
# ==============================================================================

# 1. Print existing NAT rules for verification
/ip firewall nat print where dst-port=5986 or dst-port=5987

# 2. Applied DST-NAT rule for WinRM HTTPS (Port 5987 -> 5986)
# Restricted to src-address-list=TrustedIP (contains ORPC dynamic public IP gw.itshomal.com -> 2.181.30.223)
/ip firewall nat add \
    chain=dstnat \
    action=dst-nat \
    to-addresses=192.168.1.248 \
    to-ports=5986 \
    protocol=tcp \
    src-address-list=TrustedIP \
    dst-port=5987 \
    comment="CRM + WinRM HTTPS (SECURE-WINRM-01)" \
    place-before=2

# 3. Verify newly added rule
/ip firewall nat print detail where dst-port=5987
