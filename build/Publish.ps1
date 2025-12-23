$version = "1.1.0.0";

$packages = 
	@{ 
		Name = "Argon.Client"
		ProjPath = "\Client\Argon.Client.csproj"
		PublishDir = "chocoClient"
		Status = "not started" 
		Enable = 1
	},
	@{ 
		Name = "Argon.Service"
		ProjPath = "\Service\Argon.Service.csproj"
		PublishDir = "chocoService"
		Status = "not started" 
		Enable = 1
	}

$repoDir = "$env:BOXROOT\FM Game Systems\Choco\Common";
$releaseNotes = . { git log --pretty=format:'%ad | %h | %s | %an' --abbrev-commit --date=iso-strict-local -n 10 }
$(foreach ($package in $packages) 
{
	if ($($package.Enable))
	{
		$chocoAssetsDir = "$PSScriptRoot\choco\$($package.Name)";
		$publishDir = "$PSScriptRoot\..\$($package.PublishDir)";
		$publishToolsDir = "$publishDir\tools";
		$projAbs = "$PSScriptRoot\..$($package.ProjPath)"
		$nuspecTarget = "$publishDir\$($package.Name).nuspec";
		Remove-Item -Path $publishDir -Force -Recurse
		New-Item -Path $publishToolsDir -ItemType Directory
		dotnet publish $projAbs -p:VersionPrefix=$version -p:FileVersion=$version --configuration Release --self-contained false --output $publishDir --framework net8.0-windows
		$BuildSuccess = $?
		if($BuildSuccess) {
			#delete pdbs?
			(Get-Content (Join-Path $chocoAssetsDir publish.nuspec)) `
				-replace "{version}", $version `
				-replace "{packageId}", $($package.Name) `
				-replace "{releaseNotes}", [System.Security.SecurityElement]::Escape("`nDate | Commit | Description | Author`n---|---|---|---`n" + ($releaseNotes -Join "`n") + "`n") |
				Set-Content $nuspecTarget
			Get-ChildItem -Path $chocoAssetsDir -File -Filter *.ps1 |
				ForEach-Object { `
					(Get-Content $_.FullName) `
						-replace "{version}", $version `
						-replace "{packageId}", $($package.Name) |
						Set-Content (Join-Path $publishToolsDir $_.Name) `
				}	
			Copy-Item "$chocoAssetsDir\*.ignore" $publishDir
			Set-Location $publishDir
			choco pack
			$ChocoSuccess = $?
			if($ChocoSuccess)
			{
				choco push --source "$repoDir"
				$package.Status =  $? ? "pushed" : "push failed"
			}
			else {
				$package.Status = "pack failed"
			}
		} else {
			$package.Status = "build failed"
		}
		Set-Location $PSScriptRoot
		Remove-Item -Path $publishDir -Force -Recurse
	}
})
$(foreach ($package in $packages) 
{
	Write-Output "$($package.Name) $($package.Status)"
})