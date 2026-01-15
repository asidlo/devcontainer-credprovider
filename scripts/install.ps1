# Install the Devcontainer credential provider to the NuGet plugins folder
#
# Usage:
#   From NuGet package: ~/.nuget/packages/azureartifacts.credentialprovider/1.0.0/tools/install.ps1
#   From source repo:   dotnet publish -c Release -o bin/publish; .\install.ps1

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$pluginDest = Join-Path $env:USERPROFILE ".nuget\plugins\netcore\CredentialProvider.Devcontainer"

# Check for source in order of preference:
# 1. NuGet package structure: tools/netcore/CredentialProvider.Devcontainer/
# 2. Local publish output: bin/publish/
$nugetPackageSource = Join-Path $scriptDir "netcore\CredentialProvider.Devcontainer"
$localPublishSource = Join-Path $scriptDir "bin\publish"

if (Test-Path $nugetPackageSource) {
    $pluginSource = $nugetPackageSource
} elseif (Test-Path $localPublishSource) {
    $pluginSource = $localPublishSource
} else {
    Write-Error @"
ERROR: Cannot find credential provider binaries.
Looking for:
  - $nugetPackageSource (NuGet package)
  - $localPublishSource (local build)

If building from source, run: dotnet publish -c Release -o bin/publish
"@
    exit 1
}

Write-Host "Installing Devcontainer credential provider..."
Write-Host "Source: $pluginSource"

# Create destination directory
New-Item -ItemType Directory -Force -Path $pluginDest | Out-Null

# Remove old files to ensure clean install
Remove-Item -Path "$pluginDest\*" -Recurse -Force -ErrorAction SilentlyContinue

# Copy all files
Copy-Item -Path "$pluginSource\*" -Destination $pluginDest -Recurse -Force

Write-Host "Credential provider installed to: $pluginDest"
Write-Host ""
Write-Host "You can now use 'dotnet restore' with Devcontainer feeds."
Write-Host "No environment variables needed!"
Write-Host ""
Write-Host "To verify: Get-ChildItem $pluginDest"
