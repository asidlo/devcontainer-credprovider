#!/bin/bash
# Install the AzureArtifacts credential provider to the NuGet plugins folder
#
# Usage:
#   From GitHub release:  curl -fsSL URL/credential-provider.tar.gz | tar xz && ./install.sh
#   From NuGet package:   ~/.nuget/packages/azureartifacts.credentialprovider/1.0.0/tools/install.sh
#   From source repo:     dotnet publish -c Release -o bin/publish && ./install.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLUGIN_DEST="$HOME/.nuget/plugins/netcore/CredentialProvider.AzureArtifacts"

# Check for source in order of preference:
# 1. Tarball extraction: binaries in same directory as install.sh (CredentialProvider.AzureArtifacts.dll)
# 2. NuGet package structure: tools/netcore/CredentialProvider.AzureArtifacts/
# 3. Local publish output: bin/publish/
if [ -f "$SCRIPT_DIR/CredentialProvider.AzureArtifacts.dll" ]; then
  PLUGIN_SOURCE="$SCRIPT_DIR"
elif [ -d "$SCRIPT_DIR/netcore/CredentialProvider.AzureArtifacts" ]; then
  PLUGIN_SOURCE="$SCRIPT_DIR/netcore/CredentialProvider.AzureArtifacts"
elif [ -d "$SCRIPT_DIR/bin/publish" ]; then
  PLUGIN_SOURCE="$SCRIPT_DIR/bin/publish"
else
  echo "ERROR: Cannot find credential provider binaries."
  echo "Looking for:"
  echo "  - $SCRIPT_DIR/CredentialProvider.AzureArtifacts.dll (tarball extraction)"
  echo "  - $SCRIPT_DIR/netcore/CredentialProvider.AzureArtifacts/ (NuGet package)"
  echo "  - $SCRIPT_DIR/bin/publish/ (local build)"
  echo ""
  echo "If building from source, run: dotnet publish -c Release -o bin/publish"
  exit 1
fi

echo "Installing AzureArtifacts credential provider..."
echo "Source: $PLUGIN_SOURCE"

# Create destination directory
mkdir -p "$PLUGIN_DEST"

# Remove old files to ensure clean install
rm -rf "$PLUGIN_DEST"/*

# Copy all files
cp -r "$PLUGIN_SOURCE/"* "$PLUGIN_DEST/"

# Make the executable runnable (for Linux/macOS)
chmod +x "$PLUGIN_DEST/CredentialProvider.AzureArtifacts" 2>/dev/null || true

echo "Credential provider installed to: $PLUGIN_DEST"
echo ""
echo "You can now use 'dotnet restore' with Azure Artifacts feeds."
echo "No environment variables needed!"
echo ""
echo "To verify: ls -la $PLUGIN_DEST"
