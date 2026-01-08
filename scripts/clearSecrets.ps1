$ErrorActionPreference = "Stop"

function Get-DeadBeefBase64 {
    param (
        [int]$Length
    )
    
    $pattern = [byte[]](0xDE, 0xAD, 0xBE, 0xEF)
    $bytes = New-Object byte[] $Length
    
    for ($i = 0; $i -lt $Length; $i++) {
        $bytes[$i] = $pattern[$i % 4]
    }
    
    return [Convert]::ToBase64String($bytes)
}

# Lengths based on standard NIST P-256 key blob sizes and the noise array size
# Private Key Blob: Magic(4) + Len(4) + X(32) + Y(32) + D(32) = 104 bytes
$env:ARGON_PRIVATE_KEY = Get-DeadBeefBase64 -Length 104

# Public Key Blob: Magic(4) + Len(4) + X(32) + Y(32) = 72 bytes
$env:ARGON_PUBLIC_KEY = Get-DeadBeefBase64 -Length 72

# Noise: 32 bytes
$env:ARGON_NOISE = Get-DeadBeefBase64 -Length 32

# Auth: 32 bytes
$env:ARGON_TPM_AUTH = Get-DeadBeefBase64 -Length 32

Write-Host "Setting dummy secrets (DEADBEEF)..."
Write-Host "ARGON_PRIVATE_KEY length: 104 bytes"
Write-Host "ARGON_PUBLIC_KEY length: 72 bytes"
Write-Host "ARGON_NOISE length: 32 bytes"
Write-Host "ARGON_TPM_AUTH length: 32 bytes"

$scriptPath = Join-Path $PSScriptRoot "injectSecrets.ps1"
& $scriptPath
