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

# Install xdg-open for browser-based authentication flows
# Microsoft's artifacts-credprovider uses xdg-open to launch the browser for device code flow
echo ""
echo "Installing xdg-open..."

XDG_OPEN_SHIM_CONTENT='#!/bin/bash
# Shim to redirect xdg-open calls to VS Code'"'"'s browser helper
if [ -n "$BROWSER" ]; then
    exec "$BROWSER" "$@"
else
    echo "No BROWSER set, cannot open: $1" >&2
    exit 1
fi'

install_xdg_open_shim() {
    echo "$XDG_OPEN_SHIM_CONTENT" > /usr/local/bin/xdg-open
    chmod 755 /usr/local/bin/xdg-open
    echo "✓ Installed xdg-open shim to /usr/local/bin/xdg-open"
}

# Check if already available
if command -v xdg-open &>/dev/null; then
    echo "✓ xdg-open already available at $(command -v xdg-open)"
else
    # Detect OS from /etc/os-release
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        OS_ID="${ID:-unknown}"
        OS_ID_LIKE="${ID_LIKE:-}"
    else
        OS_ID="unknown"
        OS_ID_LIKE=""
    fi

    case "$OS_ID" in
        debian|ubuntu|linuxmint|pop|elementary|zorin|kali|raspbian)
            if apt-get update && apt-get install -y xdg-utils 2>/dev/null; then
                echo "✓ Installed xdg-open via apt-get"
            else
                echo "⚠ apt-get install failed, installing shim..."
                install_xdg_open_shim
            fi
            ;;
        mariner|azurelinux|cbl-mariner)
            # Azure Linux/Mariner - xdg-utils not available in repos
            echo "⚠ xdg-utils not available on Azure Linux/Mariner, installing shim..."
            install_xdg_open_shim
            ;;
        fedora|rhel|centos|rocky|alma|ol)
            if dnf install -y xdg-utils 2>/dev/null; then
                echo "✓ Installed xdg-open via dnf"
            else
                echo "⚠ dnf install failed, installing shim..."
                install_xdg_open_shim
            fi
            ;;
        alpine)
            if apk add --no-cache xdg-utils 2>/dev/null; then
                echo "✓ Installed xdg-open via apk"
            else
                echo "⚠ apk install failed, installing shim..."
                install_xdg_open_shim
            fi
            ;;
        arch|manjaro|endeavouros)
            if pacman -S --noconfirm xdg-utils 2>/dev/null; then
                echo "✓ Installed xdg-open via pacman"
            else
                echo "⚠ pacman install failed, installing shim..."
                install_xdg_open_shim
            fi
            ;;
        opensuse*|sles)
            if zypper install -y xdg-utils 2>/dev/null; then
                echo "✓ Installed xdg-open via zypper"
            else
                echo "⚠ zypper install failed, installing shim..."
                install_xdg_open_shim
            fi
            ;;
        *)
            # Check ID_LIKE for derivative distros
            case "$OS_ID_LIKE" in
                *debian*|*ubuntu*)
                    if apt-get update && apt-get install -y xdg-utils 2>/dev/null; then
                        echo "✓ Installed xdg-open via apt-get"
                    else
                        install_xdg_open_shim
                    fi
                    ;;
                *fedora*|*rhel*)
                    if dnf install -y xdg-utils 2>/dev/null || yum install -y xdg-utils 2>/dev/null; then
                        echo "✓ Installed xdg-open via dnf/yum"
                    else
                        install_xdg_open_shim
                    fi
                    ;;
                *suse*)
                    if zypper install -y xdg-utils 2>/dev/null; then
                        echo "✓ Installed xdg-open via zypper"
                    else
                        install_xdg_open_shim
                    fi
                    ;;
                *arch*)
                    if pacman -S --noconfirm xdg-utils 2>/dev/null; then
                        echo "✓ Installed xdg-open via pacman"
                    else
                        install_xdg_open_shim
                    fi
                    ;;
                *)
                    echo "⚠ Unknown OS ($OS_ID), installing xdg-open shim..."
                    install_xdg_open_shim
                    ;;
            esac
            ;;
    esac
fi

# Configure environment for terminal shells
# Note: C# DevKit gets NUGET_PLUGIN_PATHS from containerEnv in devcontainer-feature.json
echo ""
echo "Configuring environment for terminal shells..."

PROFILE_SCRIPT="/etc/profile.d/nuget-credprovider.sh"

# Build full DLL paths for plugin discovery
DEVCONTAINER_PLUGIN_DLL="$PLUGIN_INSTALL_DIR/CredentialProvider.Devcontainer.dll"
AZURE_PLUGIN_DLL="$AZURE_PLUGIN_DIR/CredentialProvider.Microsoft/CredentialProvider.Microsoft.dll"

cat >"$PROFILE_SCRIPT" <<ENVSCRIPT
# Devcontainer Credential Provider - Non-interactive NuGet authentication

# Force non-interactive mode for NuGet
export NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED="true"
export NUGET_PLUGIN_HANDSHAKE_TIMEOUT_IN_SECONDS="30"
export NUGET_PLUGIN_REQUEST_TIMEOUT_IN_SECONDS="30"

# Set plugin paths so NuGet can find credential providers
# Must point to actual DLL files, semicolon-separated (even on Linux)
# Custom devcontainer provider is first, falls back to Microsoft's artifacts-credprovider
export NUGET_PLUGIN_PATHS="$DEVCONTAINER_PLUGIN_DLL;$AZURE_PLUGIN_DLL"
ENVSCRIPT

chmod 644 "$PROFILE_SCRIPT"

# Also add to /etc/environment for non-interactive shells (used by C# DevKit, VS Code extensions)
if [ -w "/etc/environment" ] || [ "$(id -u)" = "0" ]; then
  # Remove any existing NUGET_PLUGIN_PATHS line
  grep -v '^NUGET_PLUGIN_PATHS=' /etc/environment > /tmp/environment.tmp 2>/dev/null || true
  echo "NUGET_PLUGIN_PATHS=\"$DEVCONTAINER_PLUGIN_DLL;$AZURE_PLUGIN_DLL\"" >> /tmp/environment.tmp
  mv /tmp/environment.tmp /etc/environment
  echo "✓ Configured /etc/environment (for non-interactive shells)"
fi

echo "✓ Environment configured in $PROFILE_SCRIPT"

echo ""
echo "You can now use 'dotnet restore' with Azure Artifacts feeds."
echo "C# DevKit will also use this credential provider."
echo ""
echo "Plugin locations:"
echo "  Custom (auth helper)           : $PLUGIN_INSTALL_DIR"
echo "  Azure (artifacts-credprovider) : $AZURE_PLUGIN_DIR"
