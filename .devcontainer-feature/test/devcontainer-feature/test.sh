#!/bin/bash
# Test script for the azure-artifacts-credprovider devcontainer feature
#
# This script verifies that the credential provider was installed correctly.

set -e

echo "Testing Azure Artifacts Credential Provider installation..."

# Check if the plugin directory exists
PLUGIN_DIR="$HOME/.nuget/plugins/netcore/CredentialProvider.AzureArtifacts"

if [ ! -d "$PLUGIN_DIR" ]; then
    echo "FAIL: Plugin directory not found at $PLUGIN_DIR"
    exit 1
fi

echo "✓ Plugin directory exists"

# Check if the main DLL exists
if [ ! -f "$PLUGIN_DIR/CredentialProvider.AzureArtifacts.dll" ]; then
    echo "FAIL: Main DLL not found"
    exit 1
fi

echo "✓ Main DLL exists"

# Check if dotnet can execute the provider
if command -v dotnet &> /dev/null; then
    VERSION=$(dotnet "$PLUGIN_DIR/CredentialProvider.AzureArtifacts.dll" --version 2>/dev/null)
    if [ $? -eq 0 ]; then
        echo "✓ Provider executes successfully: $VERSION"
    else
        echo "FAIL: Provider failed to execute"
        exit 1
    fi
    
    # Check help output
    HELP=$(dotnet "$PLUGIN_DIR/CredentialProvider.AzureArtifacts.dll" --help 2>/dev/null)
    if [[ "$HELP" == *"NuGet Credential Provider"* ]]; then
        echo "✓ Help output is correct"
    else
        echo "FAIL: Help output unexpected"
        exit 1
    fi
else
    echo "⚠ dotnet not available, skipping execution test"
fi

echo ""
echo "All tests passed!"
