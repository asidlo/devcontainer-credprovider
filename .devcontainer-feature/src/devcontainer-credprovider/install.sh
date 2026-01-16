#!/bin/bash
# Devcontainer Credential Provider - Devcontainer Feature Install Script
#
# This script installs the silent/headless NuGet credential provider for Devcontainer.
# The credential provider binaries are embedded in this feature package.
#
# It also installs Microsoft's artifacts-credprovider as a fallback for device code flow.

set -e

echo "Installing Devcontainer Credential Provider..."

# Fixed installation directories
# NuGet discovers plugins by scanning for CredentialProvider.*/ subdirectories
PLUGIN_BASE_DIR="/usr/local/share/nuget/plugins/custom"
PLUGIN_INSTALL_DIR="$PLUGIN_BASE_DIR/CredentialProvider.Devcontainer"
AZURE_PLUGIN_DIR="/usr/local/share/nuget/plugins/azure"

# Get the directory where this script is located (contains embedded binaries)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
EMBEDDED_DIR="$SCRIPT_DIR/netcore"

# Check if binaries are embedded in the feature
if [ -d "$EMBEDDED_DIR" ] && [ -f "$EMBEDDED_DIR/CredentialProvider.Devcontainer.dll" ]; then
    echo "Using embedded credential provider binaries..."

    # Create plugin installation directory
    mkdir -p "$PLUGIN_INSTALL_DIR"

    # Copy embedded binaries
    cp -r "$EMBEDDED_DIR"/* "$PLUGIN_INSTALL_DIR/"

    # Set permissions
    chmod -R 755 "$PLUGIN_INSTALL_DIR"

    echo "Installed embedded binaries to $PLUGIN_INSTALL_DIR"
else
    echo "ERROR: Embedded binaries not found at $EMBEDDED_DIR"
    echo "This feature package may be corrupted. Please report this issue."
    exit 1
fi

# Verify installation
if [ -f "$PLUGIN_INSTALL_DIR/CredentialProvider.Devcontainer.dll" ]; then
    echo ""
    echo "✓ Devcontainer Credential Provider installed successfully!"
    echo "  Location: $PLUGIN_INSTALL_DIR"

    # Try to show version if dotnet is available
    if command -v dotnet &>/dev/null; then
        INSTALLED_VERSION=$(dotnet "$PLUGIN_INSTALL_DIR/CredentialProvider.Devcontainer.dll" --version 2>/dev/null | awk '{print $2}' || echo "unknown")
        echo "  Version: $INSTALLED_VERSION"
    fi
else
    echo "ERROR: Installation verification failed"
    exit 1
fi

# Install Microsoft's artifacts-credprovider as fallback
echo ""
echo "Installing Microsoft artifacts-credprovider as fallback..."

# The upstream install script hardcodes $HOME/.nuget/, so we download directly
AZURE_CREDPROVIDER_URL="https://github.com/Microsoft/artifacts-credprovider/releases/latest/download/Microsoft.Net6.NuGet.CredentialProvider.tar.gz"

# Create Azure plugin directory
mkdir -p "$AZURE_PLUGIN_DIR"

if command -v curl &>/dev/null; then
    # Download and extract to temp, then move to target location
    AZURE_TEMP_DIR=$(mktemp -d)
    trap "rm -rf $AZURE_TEMP_DIR" EXIT

    echo "Downloading from $AZURE_CREDPROVIDER_URL"
    if curl -fsSL "$AZURE_CREDPROVIDER_URL" | tar xz -C "$AZURE_TEMP_DIR" 2>/dev/null; then
        # The tarball extracts to plugins/netcore/CredentialProvider.Microsoft/
        if [ -d "$AZURE_TEMP_DIR/plugins/netcore/CredentialProvider.Microsoft" ]; then
            rm -rf "$AZURE_PLUGIN_DIR/CredentialProvider.Microsoft"
            cp -r "$AZURE_TEMP_DIR/plugins/netcore/CredentialProvider.Microsoft" "$AZURE_PLUGIN_DIR/"
            chmod -R 755 "$AZURE_PLUGIN_DIR/CredentialProvider.Microsoft"
            echo "✓ Microsoft artifacts-credprovider installed to $AZURE_PLUGIN_DIR/CredentialProvider.Microsoft"
        else
            echo "⚠ Warning: Unexpected archive structure"
        fi
    else
        echo "⚠ Warning: Failed to download Microsoft artifacts-credprovider"
        echo "  Device code flow fallback will not be available"
    fi
else
    echo "⚠ Warning: curl not available, skipping artifacts-credprovider installation"
fi

# Configure environment for terminal shells
# Note: C# DevKit gets NUGET_PLUGIN_PATHS from containerEnv in devcontainer-feature.json
echo ""
echo "Configuring environment for terminal shells..."

PROFILE_SCRIPT="/etc/profile.d/nuget-credprovider.sh"

cat >"$PROFILE_SCRIPT" <<'ENVSCRIPT'
# Devcontainer Credential Provider - Non-interactive NuGet authentication

# Force non-interactive mode for NuGet
export NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED="true"
export NUGET_PLUGIN_HANDSHAKE_TIMEOUT_IN_SECONDS="30"
export NUGET_PLUGIN_REQUEST_TIMEOUT_IN_SECONDS="30"

# Set plugin paths so NuGet can find credential providers
# Custom devcontainer provider is first, falls back to Microsoft's artifacts-credprovider
export NUGET_PLUGIN_PATHS="/usr/local/share/nuget/plugins/custom;/usr/local/share/nuget/plugins/azure"
ENVSCRIPT

chmod 644 "$PROFILE_SCRIPT"

echo "✓ Environment configured in $PROFILE_SCRIPT"

echo ""
echo "You can now use 'dotnet restore' with Azure Artifacts feeds."
echo "C# DevKit will also use this credential provider."
echo ""
echo "Plugin locations:"
echo "  Custom (auth helper)           : $PLUGIN_INSTALL_DIR"
echo "  Azure (artifacts-credprovider) : $AZURE_PLUGIN_DIR"
