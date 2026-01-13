#!/bin/bash
# Azure Artifacts Credential Provider - Devcontainer Feature Install Script
#
# This script installs the silent/headless NuGet credential provider for Azure Artifacts.
# The credential provider binaries are embedded in this feature package.

set -e

echo "Installing Azure Artifacts Credential Provider..."

# Determine the user's home directory
if [ -n "$_REMOTE_USER_HOME" ]; then
    USER_HOME="$_REMOTE_USER_HOME"
elif [ -n "$_CONTAINER_USER_HOME" ]; then
    USER_HOME="$_CONTAINER_USER_HOME"
else
    USER_HOME="${HOME:-/root}"
fi

PLUGIN_DEST="$USER_HOME/.nuget/plugins/netcore/CredentialProvider.AzureArtifacts"

# Get the directory where this script is located (contains embedded binaries)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
EMBEDDED_DIR="$SCRIPT_DIR/netcore"

# Check if binaries are embedded in the feature
if [ -d "$EMBEDDED_DIR" ] && [ -f "$EMBEDDED_DIR/CredentialProvider.AzureArtifacts.dll" ]; then
    echo "Using embedded credential provider binaries..."
    
    # Create destination directory
    mkdir -p "$PLUGIN_DEST"
    
    # Copy embedded binaries
    cp -r "$EMBEDDED_DIR"/* "$PLUGIN_DEST/"
    
    echo "Installed embedded binaries to $PLUGIN_DEST"
else
    echo "ERROR: Embedded binaries not found at $EMBEDDED_DIR"
    echo "This feature package may be corrupted. Please report this issue."
    exit 1
fi

# Verify installation
if [ -f "$PLUGIN_DEST/CredentialProvider.AzureArtifacts.dll" ]; then
    echo ""
    echo "✓ Azure Artifacts Credential Provider installed successfully!"
    echo "  Location: $PLUGIN_DEST"
    
    # Try to show version if dotnet is available
    if command -v dotnet &> /dev/null; then
        INSTALLED_VERSION=$(dotnet "$PLUGIN_DEST/CredentialProvider.AzureArtifacts.dll" --version 2>/dev/null | awk '{print $2}' || echo "unknown")
        echo "  Version: $INSTALLED_VERSION"
    fi
    
    echo ""
    echo "You can now use 'dotnet restore' with Azure Artifacts feeds."
else
    echo "ERROR: Installation verification failed"
    exit 1
fi
