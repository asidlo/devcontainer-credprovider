# Uninstall the Devcontainer credential provider

$pluginDest = Join-Path $env:USERPROFILE ".nuget\plugins\netcore\CredentialProvider.Devcontainer"

if (Test-Path $pluginDest) {
    Write-Host "Removing Devcontainer credential provider..."
    Remove-Item -Path $pluginDest -Recurse -Force
    Write-Host "Credential provider removed from: $pluginDest"

    # Clean up parent if empty
    $pluginParent = Join-Path $env:USERPROFILE ".nuget\plugins\netcore"
    if ((Test-Path $pluginParent) -and ((Get-ChildItem $pluginParent | Measure-Object).Count -eq 0)) {
        Remove-Item -Path $pluginParent -Force
    }
} else {
    Write-Host "Credential provider not found at: $pluginDest"
    Write-Host "(Already uninstalled or never installed)"
}
