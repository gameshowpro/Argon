$ErrorActionPreference = "Stop"

function Set-CSharpSecret {
    param (
        [string]$FilePath,
        [string]$VariableName,
        [string]$EnvVarName
    )

    $base64Value = [Environment]::GetEnvironmentVariable($EnvVarName)
    
    if ([string]::IsNullOrWhiteSpace($base64Value)) {
        Write-Warning "Environment variable '$EnvVarName' is not set or empty. Skipping injection for '$VariableName'."
        return
    }

    if (-not (Test-Path $FilePath)) {
        Write-Error "File not found: $FilePath"
    }

    try {
        $bytes = [Convert]::FromBase64String($base64Value)
    }
    catch {
        Write-Error "Failed to decode Base64 value from '$EnvVarName'. Error: $_"
    }

    # Format as hex strings: 0x01, 0x02, ...
    $hexStrings = $bytes | ForEach-Object { "0x{0:X2}" -f $_ }
    
    # Create the inner content string with indentation
    $innerContent = "`r`n        " + ($hexStrings -join ", ") + "`r`n    "

    $fileContent = Get-Content $FilePath -Raw

    # Regex to find the variable definition and its content
    # Matches: internal static readonly byte[] VarName = { ... }; or [ ... ];
    # Group 1: Prefix (declaration + opening brace/bracket)
    # Group 2: Suffix (closing brace/bracket + semicolon)
    # The content between them is matched by .*? but not captured in a group we use (it's replaced)
    
    $pattern = "(?s)(static readonly byte\[\]\s+$VariableName\s*=\s*(?:\{|\[)).*?((?:\}|\]);)"
    
    if ($fileContent -match $pattern) {
        # Construct replacement string using captured groups $1 and $2
        $replacement = '${1}' + $innerContent + '${2}'
        
        $updatedContent = $fileContent -replace $pattern, $replacement
        
        [System.IO.File]::WriteAllText($FilePath, $updatedContent, [System.Text.Encoding]::UTF8)
        Write-Host "Successfully injected '$EnvVarName' into '$VariableName' in '$FilePath'."
    } else {
        Write-Warning "Variable definition for '$VariableName' not found in '$FilePath'."
    }
}

$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$privateFile = Join-Path $repoRoot "src\GameshowPro.Argon.Create\Private.cs"
$publicFile = Join-Path $repoRoot "src\GameshowPro.Argon.Common\Public.cs"
$authFile = Join-Path $repoRoot "src\GameshowPro.Argon.Common\Tools.cs"

Set-CSharpSecret -FilePath $privateFile -VariableName "s_private" -EnvVarName "ARGON_PRIVATE_KEY"
Set-CSharpSecret -FilePath $publicFile -VariableName "s_public" -EnvVarName "ARGON_PUBLIC_KEY"
Set-CSharpSecret -FilePath $publicFile -VariableName "s_noise" -EnvVarName "ARGON_NOISE"
Set-CSharpSecret -FilePath $authFile -VariableName "s_auth" -EnvVarName "ARGON_TPM_AUTH"
