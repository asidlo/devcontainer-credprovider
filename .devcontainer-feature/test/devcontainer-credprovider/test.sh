#!/bin/bash
# Test script for the devcontainer-credprovider devcontainer feature
#
# This script verifies that the credential provider was installed correctly.

set -e

echo "Testing Devcontainer Credential Provider installation..."

# Check if the plugin directory exists
PLUGIN_DIR="/usr/local/share/nuget/plugins/custom"

if [ ! -d "$PLUGIN_DIR" ]; then
    echo "FAIL: Plugin directory not found at $PLUGIN_DIR"
    exit 1
fi

echo "✓ Plugin directory exists"

# Check if the main DLL exists
if [ ! -f "$PLUGIN_DIR/CredentialProvider.Devcontainer.dll" ]; then
    echo "FAIL: Main DLL not found"
    exit 1
fi

echo "✓ Main DLL exists"

# Check if dotnet can execute the provider
if command -v dotnet &> /dev/null; then
    VERSION=$(dotnet "$PLUGIN_DIR/CredentialProvider.Devcontainer.dll" --version 2>/dev/null)
    if [ $? -eq 0 ]; then
        echo "✓ Provider executes successfully: $VERSION"
    else
        echo "FAIL: Provider failed to execute"
        exit 1
    fi
    
    # Check help output
    HELP=$(dotnet "$PLUGIN_DIR/CredentialProvider.Devcontainer.dll" --help 2>/dev/null)
    if [[ "$HELP" == *"NuGet Credential Provider"* ]]; then
        echo "✓ Help output is correct"
    else
        echo "FAIL: Help output unexpected"
        exit 1
    fi
else
    echo "⚠ dotnet not available, skipping execution test"
fi

# Check if NUGET_PLUGIN_PATHS is configured
if [ -f "/etc/profile.d/nuget-credprovider.sh" ]; then
    echo "✓ Environment configuration file exists"
    
    # Source the file and check the variable
    source /etc/profile.d/nuget-credprovider.sh
    if [[ "$NUGET_PLUGIN_PATHS" == *"/usr/local/share/nuget/plugins/custom"* ]]; then
        echo "✓ NUGET_PLUGIN_PATHS includes custom plugin directory"
    else
        echo "⚠ NUGET_PLUGIN_PATHS may not include custom plugin directory"
    fi
else
    echo "⚠ Environment configuration file not found"
fi

# Check if artifacts-credprovider fallback is installed
AZURE_PLUGIN_DIR="/usr/local/share/nuget/plugins/azure"
if [ -d "$AZURE_PLUGIN_DIR" ]; then
    echo "✓ Azure artifacts-credprovider directory exists"
else
    echo "⚠ Azure artifacts-credprovider not installed (optional fallback)"
fi

echo ""
echo "All tests passed!"
