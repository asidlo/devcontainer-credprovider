#!/bin/bash
# Install the Devcontainer credential provider to the NuGet plugins folder
#
# Usage:
#   ./install.sh                              # Build locally and install
#   SOURCE=release ./install.sh               # Download from latest GitHub release
#   SOURCE=pr PR_NUMBER=123 ./install.sh      # Download from PR build artifact
#   RUN_TESTS=true ./install.sh               # Build, install, and run tests
#
# Environment variables:
#   SOURCE                      - Where to get binaries: "local" (default), "release", or "pr"
#   PR_NUMBER                   - PR number when SOURCE=pr
#   RELEASE_VERSION             - Release tag when SOURCE=release (default: latest)
#   RUN_TESTS                   - Run verification tests after install (default: false)
#   PLUGIN_INSTALL_DIR          - Override installation directory (default: /usr/local/share/nuget/plugins/custom)
#   SKIP_ARTIFACTS_CREDPROVIDER - Skip installing Microsoft's artifacts-credprovider (default: false)
#   SKIP_ENV_CONFIG             - Skip configuring NUGET_PLUGIN_PATHS (default: false)
#   GITHUB_REPO                 - GitHub repo for downloads (default: asidlo/devcontainer-credprovider)

set -e

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." 2>/dev/null && pwd)" || REPO_ROOT="$SCRIPT_DIR"
SOURCE="${SOURCE:-local}"
RUN_TESTS="${RUN_TESTS:-false}"
GITHUB_REPO="${GITHUB_REPO:-asidlo/devcontainer-credprovider}"
PLUGIN_BASE_DIR="${PLUGIN_INSTALL_DIR:-/usr/local/share/nuget/plugins/custom}"
PLUGIN_INSTALL_DIR="$PLUGIN_BASE_DIR/CredentialProvider.Devcontainer"
AZURE_PLUGIN_DIR="/usr/local/share/nuget/plugins/azure"

# Temporary directory for downloads/builds
WORK_DIR=$(mktemp -d)
trap "rm -rf $WORK_DIR" EXIT

echo "=== Devcontainer Credential Provider - Install ==="
echo ""
echo "Source: $SOURCE"
echo "Install directory: $PLUGIN_INSTALL_DIR"
echo ""

# Function to find plugin source from various locations
find_plugin_source() {
  local search_dir="$1"
  
  if [ -f "$search_dir/netcore/CredentialProvider.Devcontainer.dll" ]; then
    echo "$search_dir/netcore"
  elif [ -f "$search_dir/CredentialProvider.Devcontainer.dll" ]; then
    echo "$search_dir"
  elif [ -d "$search_dir/netcore/CredentialProvider.Devcontainer" ]; then
    echo "$search_dir/netcore/CredentialProvider.Devcontainer"
  else
    echo ""
  fi
}

# Get binaries based on SOURCE
case "$SOURCE" in
  local)
    echo "1. Building from local source..."
    
    # Check if we're in a repo with source code
    if [ -d "$REPO_ROOT/src/CredentialProvider.Devcontainer" ]; then
      PUBLISH_DIR="$WORK_DIR/publish"
      dotnet publish "$REPO_ROOT/src/CredentialProvider.Devcontainer" \
        -c Release \
        -o "$PUBLISH_DIR" \
        --nologo
      PLUGIN_SOURCE="$PUBLISH_DIR"
      echo "   ✓ Built from source"
    else
      # Try to find pre-built binaries in common locations
      PLUGIN_SOURCE=$(find_plugin_source "$SCRIPT_DIR")
      if [ -z "$PLUGIN_SOURCE" ]; then
        PLUGIN_SOURCE=$(find_plugin_source "$REPO_ROOT/bin/publish")
      fi
      
      if [ -z "$PLUGIN_SOURCE" ]; then
        echo "ERROR: Cannot find credential provider binaries."
        echo "Either run from the repository root or set SOURCE=release to download."
        exit 1
      fi
      echo "   ✓ Using pre-built binaries from $PLUGIN_SOURCE"
    fi
    ;;
    
  release)
    echo "1. Downloading from GitHub release..."
    
    RELEASE_VERSION="${RELEASE_VERSION:-latest}"
    
    if ! command -v gh &>/dev/null && ! command -v curl &>/dev/null; then
      echo "ERROR: Either 'gh' (GitHub CLI) or 'curl' is required for release downloads"
      exit 1
    fi
    
    cd "$WORK_DIR"
    
    if command -v gh &>/dev/null; then
      if [ "$RELEASE_VERSION" = "latest" ]; then
        gh release download -R "$GITHUB_REPO" -p "*.tar.gz"
      else
        gh release download "$RELEASE_VERSION" -R "$GITHUB_REPO" -p "*.tar.gz"
      fi
    else
      # Fallback to curl
      if [ "$RELEASE_VERSION" = "latest" ]; then
        DOWNLOAD_URL="https://github.com/$GITHUB_REPO/releases/latest/download/devcontainer-credprovider.tar.gz"
      else
        DOWNLOAD_URL="https://github.com/$GITHUB_REPO/releases/download/$RELEASE_VERSION/devcontainer-credprovider.tar.gz"
      fi
      curl -fsSL -o devcontainer-credprovider.tar.gz "$DOWNLOAD_URL"
    fi
    
    tar xzf *.tar.gz
    PLUGIN_SOURCE=$(find_plugin_source "$WORK_DIR")
    
    if [ -z "$PLUGIN_SOURCE" ]; then
      echo "ERROR: Failed to find binaries in downloaded release"
      exit 1
    fi
    echo "   ✓ Downloaded release ${RELEASE_VERSION}"
    ;;
    
  pr)
    echo "1. Downloading from PR build..."
    
    if [ -z "$PR_NUMBER" ]; then
      echo "ERROR: PR_NUMBER is required when SOURCE=pr"
      exit 1
    fi
    
    if ! command -v gh &>/dev/null; then
      echo "ERROR: 'gh' (GitHub CLI) is required for PR artifact downloads"
      exit 1
    fi
    
    cd "$WORK_DIR"
    
    # Get the latest workflow run for this PR
    RUN_ID=$(gh run list -R "$GITHUB_REPO" --branch "pull/$PR_NUMBER/head" --workflow "Build and Test" --status success --limit 1 --json databaseId -q '.[0].databaseId')
    
    if [ -z "$RUN_ID" ]; then
      echo "ERROR: No successful build found for PR #$PR_NUMBER"
      exit 1
    fi
    
    gh run download "$RUN_ID" -R "$GITHUB_REPO" -n "credential-provider"
    PLUGIN_SOURCE=$(find_plugin_source "$WORK_DIR")
    
    if [ -z "$PLUGIN_SOURCE" ]; then
      echo "ERROR: Failed to find binaries in PR artifact"
      exit 1
    fi
    echo "   ✓ Downloaded from PR #$PR_NUMBER (run $RUN_ID)"
    ;;
    
  *)
    echo "ERROR: Unknown SOURCE '$SOURCE'. Valid values: local, release, pr"
    exit 1
    ;;
esac

# Verify we have the plugin DLL
if [ ! -f "$PLUGIN_SOURCE/CredentialProvider.Devcontainer.dll" ]; then
  echo "ERROR: CredentialProvider.Devcontainer.dll not found in $PLUGIN_SOURCE"
  exit 1
fi

# Install the plugin
echo ""
echo "2. Installing credential provider..."

mkdir -p "$PLUGIN_INSTALL_DIR"
rm -rf "$PLUGIN_INSTALL_DIR"/*
cp -r "$PLUGIN_SOURCE/"* "$PLUGIN_INSTALL_DIR/"
chmod -R 755 "$PLUGIN_INSTALL_DIR"

echo "   ✓ Installed to $PLUGIN_INSTALL_DIR"

# Install Microsoft's artifacts-credprovider as fallback
if [ "${SKIP_ARTIFACTS_CREDPROVIDER:-false}" != "true" ]; then
  echo ""
  echo "3. Installing Microsoft artifacts-credprovider as fallback..."
  
  # The upstream install script hardcodes $HOME/.nuget/, so we download directly
  AZURE_CREDPROVIDER_VERSION="${AZURE_CREDPROVIDER_VERSION:-latest}"
  AZURE_CREDPROVIDER_FILE="Microsoft.Net6.NuGet.CredentialProvider.tar.gz"
  
  if [ "$AZURE_CREDPROVIDER_VERSION" = "latest" ]; then
    AZURE_CREDPROVIDER_URL="https://github.com/Microsoft/artifacts-credprovider/releases/latest/download/$AZURE_CREDPROVIDER_FILE"
  else
    AZURE_CREDPROVIDER_URL="https://github.com/Microsoft/artifacts-credprovider/releases/download/$AZURE_CREDPROVIDER_VERSION/$AZURE_CREDPROVIDER_FILE"
  fi
  
  mkdir -p "$AZURE_PLUGIN_DIR"
  
  if command -v curl &>/dev/null; then
    echo "   Downloading from $AZURE_CREDPROVIDER_URL"
    
    # Download and extract to temp, then move to target location
    AZURE_TEMP_DIR="$WORK_DIR/azure-credprovider"
    mkdir -p "$AZURE_TEMP_DIR"
    
    if curl -fsSL "$AZURE_CREDPROVIDER_URL" | tar xz -C "$AZURE_TEMP_DIR" 2>/dev/null; then
      # The tarball extracts to plugins/netcore/CredentialProvider.Microsoft/
      if [ -d "$AZURE_TEMP_DIR/plugins/netcore/CredentialProvider.Microsoft" ]; then
        rm -rf "$AZURE_PLUGIN_DIR/CredentialProvider.Microsoft"
        cp -r "$AZURE_TEMP_DIR/plugins/netcore/CredentialProvider.Microsoft" "$AZURE_PLUGIN_DIR/"
        chmod -R 755 "$AZURE_PLUGIN_DIR/CredentialProvider.Microsoft"
        echo "   ✓ Installed to $AZURE_PLUGIN_DIR/CredentialProvider.Microsoft"
      else
        echo "   ⚠ Warning: Unexpected archive structure"
        ls -la "$AZURE_TEMP_DIR"
      fi
    else
      echo "   ⚠ Warning: Failed to download Microsoft artifacts-credprovider"
    fi
  else
    echo "   ⚠ Warning: curl not available, skipping"
  fi
else
  echo ""
  echo "3. Skipping Microsoft artifacts-credprovider (SKIP_ARTIFACTS_CREDPROVIDER=true)"
fi

# Configure NUGET_PLUGIN_PATHS (note: plural with 'S' is required by NuGet)
if [ "${SKIP_ENV_CONFIG:-false}" != "true" ]; then
  echo ""
  echo "4. Configuring environment..."
  
  # Build plugin paths - must point to the actual plugin DLL, semicolon-separated (even on Linux)
  DEVCONTAINER_PLUGIN_DLL="$PLUGIN_INSTALL_DIR/CredentialProvider.Devcontainer.dll"
  AZURE_PLUGIN_DLL="$AZURE_PLUGIN_DIR/CredentialProvider.Microsoft/CredentialProvider.Microsoft.dll"
  
  # Configure /etc/profile.d for interactive login shells (requires root)
  PROFILE_SCRIPT="/etc/profile.d/nuget-credprovider.sh"
  if [ -w "/etc/profile.d" ] || [ "$(id -u)" = "0" ]; then
    cat >"$PROFILE_SCRIPT" <<ENVSCRIPT
# Devcontainer Credential Provider - Non-interactive NuGet authentication
export NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED="true"
export NUGET_CREDENTIALPROVIDER_FORCE_CANSHOWDIALOG_TO="true"
export NUGET_PLUGIN_HANDSHAKE_TIMEOUT_IN_SECONDS="30"
export NUGET_PLUGIN_REQUEST_TIMEOUT_IN_SECONDS="30"

# Plugin paths - custom provider first, then Azure artifacts-credprovider fallback
# Note: NUGET_PLUGIN_PATHS must be semicolon-separated (even on Linux) and point to actual plugin DLLs
export NUGET_PLUGIN_PATHS="$DEVCONTAINER_PLUGIN_DLL;$AZURE_PLUGIN_DLL"
ENVSCRIPT
    chmod 644 "$PROFILE_SCRIPT"
    echo "   ✓ Configured $PROFILE_SCRIPT"
  else
    echo "   ⚠ Skipping /etc/profile.d (not writable, not root)"
  fi
  
  # Configure /etc/environment for non-interactive shells (requires root)
  # This is read by PAM, systemd, and some VS Code extensions
  if [ -w "/etc/environment" ] || [ "$(id -u)" = "0" ]; then
    # Remove any existing NUGET_PLUGIN_PATHS line
    grep -v '^NUGET_PLUGIN_PATHS=' /etc/environment > /tmp/environment.tmp 2>/dev/null || true
    echo "NUGET_PLUGIN_PATHS=\"$DEVCONTAINER_PLUGIN_DLL;$AZURE_PLUGIN_DLL\"" >> /tmp/environment.tmp
    mv /tmp/environment.tmp /etc/environment
    echo "   ✓ Configured /etc/environment (for non-interactive shells)"
  else
    echo "   ⚠ Skipping /etc/environment (not writable, not root)"
  fi
  
  # Configure user's shell rc files (works without root)
  for rcfile in "$HOME/.bashrc" "$HOME/.zshrc"; do
    if [ -f "$rcfile" ]; then
      # Remove any existing NUGET_PLUGIN_PATHS line and add fresh
      grep -v 'NUGET_PLUGIN_PATHS=' "$rcfile" > "$rcfile.tmp" 2>/dev/null || true
      echo "" >> "$rcfile.tmp"
      echo "# Devcontainer Credential Provider" >> "$rcfile.tmp"
      echo "export NUGET_PLUGIN_PATHS=\"$DEVCONTAINER_PLUGIN_DLL;$AZURE_PLUGIN_DLL\"" >> "$rcfile.tmp"
      mv "$rcfile.tmp" "$rcfile"
      echo "   ✓ Configured $rcfile"
    fi
  done
  
  # If nothing was configured, show manual instructions
  if [ ! -w "/etc/profile.d" ] && [ "$(id -u)" != "0" ] && [ ! -f "$HOME/.bashrc" ] && [ ! -f "$HOME/.zshrc" ]; then
    echo "   ⚠ Could not configure environment automatically. Add to your shell profile:"
    echo "   export NUGET_PLUGIN_PATHS=\"$DEVCONTAINER_PLUGIN_DLL;$AZURE_PLUGIN_DLL\""
  fi
else
  echo ""
  echo "4. Skipping environment config (SKIP_ENV_CONFIG=true)"
fi

# Run tests if requested
if [ "$RUN_TESTS" = "true" ]; then
  echo ""
  echo "5. Running verification tests..."
  
  # Test that plugin exists
  if [ -f "$PLUGIN_INSTALL_DIR/CredentialProvider.Devcontainer.dll" ]; then
    echo "   ✓ Plugin DLL exists"
  else
    echo "   ✗ Plugin DLL not found"
    exit 1
  fi
  
  # Test that plugin can be invoked
  if dotnet "$PLUGIN_INSTALL_DIR/CredentialProvider.Devcontainer.dll" --help >/dev/null 2>&1; then
    echo "   ✓ Plugin executes successfully"
  else
    echo "   ✗ Plugin failed to execute"
    exit 1
  fi
  
  # Show version
  VERSION=$(dotnet "$PLUGIN_INSTALL_DIR/CredentialProvider.Devcontainer.dll" --version 2>/dev/null || echo "unknown")
  echo "   ✓ Version: $VERSION"
  
  # Test help output
  echo ""
  echo "   Help output:"
  dotnet "$PLUGIN_INSTALL_DIR/CredentialProvider.Devcontainer.dll" --help | sed 's/^/   /'
  
  echo ""
  echo "   ✓ All tests passed!"
fi

echo ""
echo "=== Installation Complete ==="
echo ""
echo "Plugin locations:"
echo "  Custom (auth helper):     $PLUGIN_INSTALL_DIR"
echo "  Azure (device code flow): $AZURE_PLUGIN_DIR"
echo ""
echo "To verify: dotnet $PLUGIN_INSTALL_DIR/CredentialProvider.Devcontainer.dll --version"
