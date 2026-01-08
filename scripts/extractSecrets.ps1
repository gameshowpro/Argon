$ErrorActionPreference = "Stop"

function Get-SecretFromFile {
    param (
        [string]$FilePath,
        [string]$VariableName
    )

    if (-not (Test-Path $FilePath)) {
        Write-Error "File not found: $FilePath"
    }

    $content = Get-Content $FilePath -Raw
    
    # Regex to match: static readonly byte[] VARNAME = { ... }; or [ ... ];
    # (?s) enables single-line mode so . matches newlines
    $pattern = "(?s)static readonly byte\[\]\s+$VariableName\s*=\s*(?:\{|\[)(.*?)(?:\}|\]);"
    
    if ($content -match $pattern) {
        $arrayContent = $matches[1]
        
        # Extract hex values
        $hexValues = [regex]::Matches($arrayContent, "0x[0-9A-Fa-f]{2}") | ForEach-Object { $_.Value }
        
        if ($hexValues.Count -eq 0) {
            Write-Warning "No bytes found for $VariableName in $FilePath"
            return $null
        }

        # Convert hex strings to bytes
        $bytes = $hexValues | ForEach-Object { [Convert]::ToByte($_, 16) }
        
        # Convert to Base64
        return [Convert]::ToBase64String($bytes)
    } else {
        Write-Warning "Variable $VariableName not found in $FilePath"
        return $null
    }
}

$repoRoot = Resolve-Path "$PSScriptRoot\.."
$privateFile = Join-Path $repoRoot "src\GameshowPro.Argon.Create\Private.cs"
$publicFile = Join-Path $repoRoot "src\GameshowPro.Argon.Common\Public.cs"
$authFile = Join-Path $repoRoot "src\GameshowPro.Argon.Common\Tools.cs"

$privateKey = Get-SecretFromFile -FilePath $privateFile -VariableName "s_private"
if ($privateKey) {
    Write-Host "ARGON_PRIVATE_KEY = $privateKey"
}

$publicKey = Get-SecretFromFile -FilePath $publicFile -VariableName "s_public"
if ($publicKey) {
    Write-Host "ARGON_PUBLIC_KEY = $publicKey"
}

$noise = Get-SecretFromFile -FilePath $publicFile -VariableName "s_noise"
if ($noise) {
    Write-Host "ARGON_NOISE = $noise"
}

$auth = Get-SecretFromFile -FilePath $authFile -VariableName "s_auth"
if ($auth) {
    Write-Host "ARGON_TPM_AUTH = $auth"
}