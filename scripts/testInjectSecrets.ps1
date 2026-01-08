$ErrorActionPreference = "Stop"

Write-Host "Paste the Base64 strings for the secrets. Press Enter to skip a secret (it won't be updated)."

$privateKey = Read-Host "ARGON_PRIVATE_KEY"
if (-not [string]::IsNullOrWhiteSpace($privateKey)) {
    $env:ARGON_PRIVATE_KEY = $privateKey.Trim()
}

$publicKey = Read-Host "ARGON_PUBLIC_KEY"
if (-not [string]::IsNullOrWhiteSpace($publicKey)) {
    $env:ARGON_PUBLIC_KEY = $publicKey.Trim()
}

$noise = Read-Host "ARGON_NOISE"
if (-not [string]::IsNullOrWhiteSpace($noise)) {
    $env:ARGON_NOISE = $noise.Trim()
}

$auth = Read-Host "ARGON_TPM_AUTH"
if (-not [string]::IsNullOrWhiteSpace($auth)) {
    $env:ARGON_TPM_AUTH = $auth.Trim()
}

Write-Host "Injecting provided secrets..."
$scriptPath = Join-Path $PSScriptRoot "injectSecrets.ps1"
& $scriptPath
