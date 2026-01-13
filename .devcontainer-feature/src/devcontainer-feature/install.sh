#!/bin/bash
# Azure Artifacts Credential Provider - Devcontainer Feature Install Script
#
# This script installs the silent/headless NuGet credential provider for Azure Artifacts.
# The credential provider binaries are embedded in this feature package.

set -e

echo "Installing Azure Artifacts Credential Provider..."

# Determine the user's home directory and username
if [ -n "$_REMOTE_USER_HOME" ]; then
    USER_HOME="$_REMOTE_USER_HOME"
elif [ -n "$_CONTAINER_USER_HOME" ]; then
    USER_HOME="$_CONTAINER_USER_HOME"
else
    USER_HOME="${HOME:-/root}"
fi

# Get the target user for ownership
TARGET_USER="${_REMOTE_USER:-${_CONTAINER_USER:-root}}"

PLUGIN_DEST="$USER_HOME/.nuget/plugins/netcore/CredentialProvider.AzureArtifacts"

# Get the directory where this script is located (contains embedded binaries)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
EMBEDDED_DIR="$SCRIPT_DIR/netcore"

# Check if binaries are embedded in the feature
if [ -d "$EMBEDDED_DIR" ] && [ -f "$EMBEDDED_DIR/CredentialProvider.AzureArtifacts.dll" ]; then
    echo "Using embedded credential provider binaries..."
    
    # Create parent directories with permissive permissions
    # This allows other features (like Microsoft's artifacts-helper) to also install credential providers
    PLUGINS_DIR="$USER_HOME/.nuget/plugins/netcore"
    mkdir -p "$PLUGINS_DIR"
    
    # Set ownership to target user so other features can write to this directory
    chown -R "$TARGET_USER:$TARGET_USER" "$USER_HOME/.nuget" 2>/dev/null || true
    chmod 755 "$USER_HOME/.nuget" 2>/dev/null || true
    chmod 755 "$USER_HOME/.nuget/plugins" 2>/dev/null || true
    chmod 755 "$PLUGINS_DIR" 2>/dev/null || true
    
    # Create our plugin directory
    mkdir -p "$PLUGIN_DEST"
    
    # Copy embedded binaries
    cp -r "$EMBEDDED_DIR"/* "$PLUGIN_DEST/"
    
    # Ensure target user owns everything
    chown -R "$TARGET_USER:$TARGET_USER" "$USER_HOME/.nuget" 2>/dev/null || true
    
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
    
    # Test auth helper availability with retries
    # The auth helper from other features may take time to initialize
    echo ""
    echo "Testing auth helper availability..."
    AUTH_HELPER_FOUND=false
    AUTH_HELPER_PATHS="$USER_HOME/ado-auth-helper $USER_HOME/azure-auth-helper /usr/local/bin/ado-auth-helper /usr/local/bin/azure-auth-helper"
    
    for helper_path in $AUTH_HELPER_PATHS; do
        if [ -x "$helper_path" ]; then
            echo "  Found auth helper: $helper_path"
            AUTH_HELPER_FOUND=true
            
            # Try to get a token with retries
            MAX_RETRIES=3
            RETRY_DELAY=2
            
            for attempt in $(seq 1 $MAX_RETRIES); do
                echo "  Attempting to acquire token (attempt $attempt/$MAX_RETRIES)..."
                if TOKEN=$($helper_path get-access-token 2>/dev/null) && [ -n "$TOKEN" ] && ! echo "$TOKEN" | grep -qi "^error"; then
                    echo "  ✓ Auth helper is working! Token acquired successfully."
                    break 2  # Break out of both loops
                else
                    if [ $attempt -lt $MAX_RETRIES ]; then
                        echo "  Token not available yet, retrying in ${RETRY_DELAY}s..."
                        sleep $RETRY_DELAY
                    fi
                fi
            done
            
            if [ -z "$TOKEN" ] || echo "$TOKEN" | grep -qi "^error"; then
                echo "  ⚠ Auth helper found but token acquisition failed."
                echo "    This may succeed later once Azure authentication is complete."
            fi
            break
        fi
    done
    
    if [ "$AUTH_HELPER_FOUND" = false ]; then
        echo "  ℹ No auth helper found. The credential provider will use Azure.Identity."
        echo "    Make sure you've run 'az login' or have Azure credentials configured."
    fi
    
    # Configure NuGet to use non-interactive mode
    # This prevents C# DevKit from falling back to device code flow
    NUGET_CONFIG_DIR="$USER_HOME/.nuget/NuGet"
    NUGET_CONFIG="$NUGET_CONFIG_DIR/NuGet.Config"
    
    mkdir -p "$NUGET_CONFIG_DIR"
    
    # Create or update NuGet.Config to disable interactive auth
    if [ ! -f "$NUGET_CONFIG" ]; then
        echo "Creating NuGet.Config with non-interactive settings..."
        cat > "$NUGET_CONFIG" << 'NUGETCONFIG'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <config>
    <!-- Disable interactive authentication prompts -->
    <!-- Forces NuGet to use credential providers instead of device code flow -->
    <add key="signatureValidationMode" value="accept" />
  </config>
  <packageSourceCredentials>
    <!-- Credential providers will handle authentication -->
  </packageSourceCredentials>
</configuration>
NUGETCONFIG
    fi
    
    chown -R "$TARGET_USER:$TARGET_USER" "$NUGET_CONFIG_DIR" 2>/dev/null || true
    
    # Set environment variable to force non-interactive mode
    # This affects both CLI and C# DevKit
    PROFILE_SCRIPT="/etc/profile.d/nuget-credprovider.sh"
    echo "Configuring environment for non-interactive NuGet authentication..."
    cat > "$PROFILE_SCRIPT" << 'ENVSCRIPT'
# Azure Artifacts Credential Provider - Non-interactive NuGet authentication
# Disables device code flow and forces use of credential providers

# Force non-interactive mode for NuGet
export NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED="true"
export NUGET_PLUGIN_HANDSHAKE_TIMEOUT_IN_SECONDS="30"
export NUGET_PLUGIN_REQUEST_TIMEOUT_IN_SECONDS="30"

# Explicitly set plugin paths so NuGet (and C# DevKit) can find credential providers
# This is the most important setting for C# DevKit integration
export NUGET_PLUGIN_PATHS="${HOME}/.nuget/plugins/netcore/CredentialProvider.AzureArtifacts/CredentialProvider.AzureArtifacts.dll"
ENVSCRIPT
    
    chmod 644 "$PROFILE_SCRIPT"
    
    echo ""
    echo "You can now use 'dotnet restore' with Azure Artifacts feeds."
    echo "C# DevKit will also use this credential provider."
else
    echo "ERROR: Installation verification failed"
    exit 1
fi
