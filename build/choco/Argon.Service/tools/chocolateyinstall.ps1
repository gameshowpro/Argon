$binDir  = Join-Path $(Get-Item $MyInvocation.MyCommand.Definition).Directory.Parent.FullName "binaries"
Write-Output "binDir = $binDir"
$serviceExe = Join-Path $binDir '$packageId$.exe'

$packageArgs = @{
  Name                  = '$packageId$'
  DisplayName           = 'Argon $version$'
  Description           = 'Service required for continued use of Argon functions.'
  StartupType           = 'Automatic'
  ServiceExecutablePath = $serviceExe
  Username              = 'LocalSystem'
}
Install-ChocolateyWindowsService @packageArgs