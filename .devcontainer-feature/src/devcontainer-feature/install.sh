#!/bin/bash
# Azure Artifacts Credential Provider - Devcontainer Feature Install Script
#
# This script installs the silent/headless NuGet credential provider for Azure Artifacts.
# It downloads from GitHub releases and installs to ~/.nuget/plugins/netcore/

set -e

VERSION="${VERSION:-latest}"
REPOSITORY="${REPOSITORY:-asidlo/credentialprovider-azureartifacts}"

echo "Installing Azure Artifacts Credential Provider..."
echo "  Repository: $REPOSITORY"
echo "  Version: $VERSION"

# Determine the user's home directory
if [ -n "$_REMOTE_USER_HOME" ]; then
    USER_HOME="$_REMOTE_USER_HOME"
elif [ -n "$_CONTAINER_USER_HOME" ]; then
    USER_HOME="$_CONTAINER_USER_HOME"
else
    USER_HOME="${HOME:-/root}"
fi

PLUGIN_DEST="$USER_HOME/.nuget/plugins/netcore/CredentialProvider.AzureArtifacts"

# Create temp directory for download
TEMP_DIR=$(mktemp -d)
trap "rm -rf $TEMP_DIR" EXIT

cd "$TEMP_DIR"

# Download the release
echo "Downloading credential provider..."

# Normalize version - add 'v' prefix if it looks like a version number without it
if [[ "$VERSION" != "latest" && "$VERSION" =~ ^[0-9] ]]; then
    VERSION="v$VERSION"
fi

# Try using GitHub CLI first (handles auth for private repos)
if command -v gh &> /dev/null; then
    echo "Using GitHub CLI to download..."
    if [ "$VERSION" = "latest" ]; then
        gh release download -R "$REPOSITORY" -p "*.tar.gz" || {
            echo "ERROR: Failed to download release. Make sure you're authenticated with 'gh auth login'"
            exit 1
        }
    else
        gh release download "$VERSION" -R "$REPOSITORY" -p "*.tar.gz" || {
            echo "ERROR: Failed to download release $VERSION"
            exit 1
        }
    fi
else
    # Fall back to curl (only works for public repos)
    echo "GitHub CLI not found, falling back to curl (only works for public repos)..."
    if [ "$VERSION" = "latest" ]; then
        DOWNLOAD_URL="https://github.com/$REPOSITORY/releases/latest/download/credentialprovider-azureartifacts.tar.gz"
    else
        DOWNLOAD_URL="https://github.com/$REPOSITORY/releases/download/$VERSION/credentialprovider-azureartifacts.tar.gz"
    fi
    
    curl -fsSL "$DOWNLOAD_URL" -o credentialprovider-azureartifacts.tar.gz || {
        echo "ERROR: Failed to download from $DOWNLOAD_URL"
        echo "If this is a private repository, install GitHub CLI and authenticate with 'gh auth login'"
        exit 1
    }
fi

# Extract the tarball
echo "Extracting..."
tar xzf credentialprovider-azureartifacts.tar.gz

# Run the install script
if [ -f install.sh ]; then
    # Override HOME so install.sh installs to the correct user's directory
    HOME="$USER_HOME" ./install.sh
else
    # Manual install if install.sh is not in the tarball
    echo "Installing to $PLUGIN_DEST..."
    mkdir -p "$PLUGIN_DEST"
    
    if [ -d netcore ]; then
        cp -r netcore/* "$PLUGIN_DEST/"
    elif [ -f CredentialProvider.AzureArtifacts.dll ]; then
        cp -r * "$PLUGIN_DEST/"
    else
        echo "ERROR: Could not find credential provider files in the archive"
        exit 1
    fi
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
