#!/bin/bash
# Uninstall the AzureArtifacts credential provider

PLUGIN_DEST="$HOME/.nuget/plugins/netcore/CredentialProvider.AzureArtifacts"

if [ -d "$PLUGIN_DEST" ]; then
    echo "Removing AzureArtifacts credential provider..."
    rm -rf "$PLUGIN_DEST"
    echo "Credential provider removed from: $PLUGIN_DEST"

    # Clean up parent if empty
    PLUGIN_PARENT="$HOME/.nuget/plugins/netcore"
    if [ -d "$PLUGIN_PARENT" ] && [ -z "$(ls -A "$PLUGIN_PARENT")" ]; then
        rmdir "$PLUGIN_PARENT"
    fi
else
    echo "Credential provider not found at: $PLUGIN_DEST"
    echo "(Already uninstalled or never installed)"
fi
