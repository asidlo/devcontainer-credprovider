#!/bin/bash
# Uninstall the Devcontainer credential provider

PLUGIN_INSTALL_DIR="${PLUGIN_INSTALL_DIR:-/usr/local/share/nuget/plugins/custom}"
AZURE_PLUGIN_DIR="/usr/local/share/nuget/plugins/azure"
PROFILE_SCRIPT="/etc/profile.d/nuget-credprovider.sh"

echo "Uninstalling Devcontainer credential provider..."

# Remove devcontainer provider
if [ -d "$PLUGIN_INSTALL_DIR" ]; then
    rm -rf "$PLUGIN_INSTALL_DIR"
    echo "✓ Removed devcontainer provider from: $PLUGIN_INSTALL_DIR"
else
    echo "⚠ Devcontainer provider not found at: $PLUGIN_INSTALL_DIR"
fi

# Remove artifacts-credprovider
if [ -d "$AZURE_PLUGIN_DIR" ]; then
    rm -rf "$AZURE_PLUGIN_DIR"
    echo "✓ Removed artifacts-credprovider from: $AZURE_PLUGIN_DIR"
else
    echo "⚠ Artifacts-credprovider not found at: $AZURE_PLUGIN_DIR"
fi

# Remove environment configuration
if [ -f "$PROFILE_SCRIPT" ]; then
    rm -f "$PROFILE_SCRIPT"
    echo "✓ Removed environment configuration: $PROFILE_SCRIPT"
else
    echo "⚠ Environment configuration not found at: $PROFILE_SCRIPT"
fi

# Clean up parent directory if empty
PLUGIN_PARENT="/usr/local/share/nuget/plugins"
if [ -d "$PLUGIN_PARENT" ] && [ -z "$(ls -A "$PLUGIN_PARENT")" ]; then
    rmdir "$PLUGIN_PARENT" 2>/dev/null || true
fi

echo ""
echo "Uninstall complete."
