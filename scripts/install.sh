#!/bin/bash
# Install the Devcontainer credential provider to the NuGet plugins folder
#
# Usage:
#   From GitHub release:  curl -fsSL URL/credential-provider.tar.gz | tar xz && ./install.sh
#   From NuGet package:   ~/.nuget/packages/azureartifacts.credentialprovider/1.0.0/tools/install.sh
#   From source repo:     dotnet publish -c Release -o bin/publish && ./install.sh
#
# Environment variables:
#   PLUGIN_INSTALL_DIR - Override the installation directory (default: /usr/local/share/nuget/plugins/custom)
#   SKIP_ARTIFACTS_CREDPROVIDER - Set to "true" to skip installing Microsoft's artifacts-credprovider
#   SKIP_ENV_CONFIG - Set to "true" to skip configuring NUGET_PLUGIN_PATH

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLUGIN_INSTALL_DIR="${PLUGIN_INSTALL_DIR:-/usr/local/share/nuget/plugins/custom}"
AZURE_PLUGIN_DIR="/usr/local/share/nuget/plugins/azure"

# Check for source in order of preference:
# 1. Tarball extraction: netcore/ subfolder with .dll (new portable format)
# 2. Tarball extraction: .dll in same directory as install.sh
# 3. NuGet package structure: tools/netcore/CredentialProvider.Devcontainer/
# 4. Local publish output: bin/publish/
#
# NOTE: NuGet plugin discovery REQUIRES a .dll file for netcore plugins.
# Self-contained executables do NOT work with ~/.nuget/plugins/netcore/ discovery.
# See: https://learn.microsoft.com/en-us/nuget/reference/extensibility/nuget-cross-platform-plugins

if [ -f "$SCRIPT_DIR/netcore/CredentialProvider.Devcontainer.dll" ]; then
  PLUGIN_SOURCE="$SCRIPT_DIR/netcore"
elif [ -f "$SCRIPT_DIR/CredentialProvider.Devcontainer.dll" ]; then
  PLUGIN_SOURCE="$SCRIPT_DIR"
elif [ -d "$SCRIPT_DIR/netcore/CredentialProvider.Devcontainer" ]; then
  PLUGIN_SOURCE="$SCRIPT_DIR/netcore/CredentialProvider.Devcontainer"
elif [ -d "$SCRIPT_DIR/bin/publish" ]; then
  PLUGIN_SOURCE="$SCRIPT_DIR/bin/publish"
else
  echo "ERROR: Cannot find credential provider binaries."
  echo "Looking for:"
  echo "  - $SCRIPT_DIR/netcore/CredentialProvider.Devcontainer.dll (tarball extraction)"
  echo "  - $SCRIPT_DIR/CredentialProvider.Devcontainer.dll (flat extraction)"
  echo "  - $SCRIPT_DIR/netcore/CredentialProvider.Devcontainer/ (NuGet package)"
  echo "  - $SCRIPT_DIR/bin/publish/ (local build)"
  echo ""
  echo "If building from source, run: dotnet publish -c Release -o bin/publish"
  exit 1
fi

echo "Installing Devcontainer credential provider..."
echo "Source: $PLUGIN_SOURCE"

# Create destination directory
mkdir -p "$PLUGIN_INSTALL_DIR"

# Remove old files to ensure clean install
rm -rf "$PLUGIN_INSTALL_DIR"/*

# Copy all files
cp -r "$PLUGIN_SOURCE/"* "$PLUGIN_INSTALL_DIR/"
chmod -R 755 "$PLUGIN_INSTALL_DIR"

echo "✓ Credential provider installed to: $PLUGIN_INSTALL_DIR"

# Install Microsoft's artifacts-credprovider as fallback
if [ "${SKIP_ARTIFACTS_CREDPROVIDER:-false}" != "true" ]; then
  echo ""
  echo "Installing Microsoft artifacts-credprovider as fallback..."
  
  AZURE_CREDPROVIDER_SCRIPT_URL="https://raw.githubusercontent.com/microsoft/artifacts-credprovider/master/helpers/installcredprovider.sh"
  
  mkdir -p "$AZURE_PLUGIN_DIR"
  
  if command -v curl &>/dev/null; then
    export NUGET_CREDENTIALPROVIDER_INSTALL_DIR="$AZURE_PLUGIN_DIR"
    if curl -fsSL "$AZURE_CREDPROVIDER_SCRIPT_URL" | bash -s -- -n 2>/dev/null; then
      echo "✓ Microsoft artifacts-credprovider installed to $AZURE_PLUGIN_DIR"
    else
      echo "⚠ Warning: Failed to install Microsoft artifacts-credprovider"
    fi
  else
    echo "⚠ Warning: curl not available, skipping artifacts-credprovider installation"
  fi
fi

# Configure NUGET_PLUGIN_PATH
if [ "${SKIP_ENV_CONFIG:-false}" != "true" ]; then
  echo ""
  echo "Configuring NUGET_PLUGIN_PATH..."
  
  PROFILE_SCRIPT="/etc/profile.d/nuget-credprovider.sh"
  
  cat >"$PROFILE_SCRIPT" <<ENVSCRIPT
# Devcontainer Credential Provider - Non-interactive NuGet authentication
export NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED="true"
export NUGET_CREDENTIALPROVIDER_FORCE_CANSHOWDIALOG_TO="true"
export NUGET_PLUGIN_HANDSHAKE_TIMEOUT_IN_SECONDS="30"
export NUGET_PLUGIN_REQUEST_TIMEOUT_IN_SECONDS="30"

# Plugin paths - custom provider first, then Azure artifacts-credprovider fallback
export NUGET_PLUGIN_PATH="$PLUGIN_INSTALL_DIR:$AZURE_PLUGIN_DIR\${NUGET_PLUGIN_PATH:+:\$NUGET_PLUGIN_PATH}"
ENVSCRIPT

  chmod 644 "$PROFILE_SCRIPT"
  echo "✓ Environment configured in $PROFILE_SCRIPT"
fi

echo ""
echo "Installation complete!"
echo ""
echo "Plugin locations:"
echo "  Custom (auth helper):     $PLUGIN_INSTALL_DIR"
echo "  Azure (device code flow): $AZURE_PLUGIN_DIR"
echo ""
echo "To verify: dotnet $PLUGIN_INSTALL_DIR/CredentialProvider.Devcontainer.dll --version"
