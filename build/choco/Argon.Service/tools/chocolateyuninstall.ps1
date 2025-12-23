try 
{
    Uninstall-ChocolateyWindowsService -Name '$packageId$'
}
catch 
{
    Write-Output "No service uninstalled"
}
