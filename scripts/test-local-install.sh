#!/bin/bash
# Test local build and installation of the credential provider
#
# Usage: ./scripts/test-local-install.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISH_DIR="$REPO_ROOT/bin/test-publish"
PLUGIN_DEST="$HOME/.nuget/plugins/netcore/CredentialProvider.Devcontainer"

echo "=== Devcontainer Credential Provider - Local Install Test ==="
echo ""

# Clean previous test output
echo "1. Cleaning previous build..."
rm -rf "$PUBLISH_DIR"

# Build the project (framework-dependent)
echo "2. Publishing credential provider..."
dotnet publish "$REPO_ROOT/src/CredentialProvider.Devcontainer" \
  -c Release \
  -o "$PUBLISH_DIR/netcore"

echo ""
echo "   Published files:"
ls -la "$PUBLISH_DIR/netcore/"

# Verify .dll exists (required for NuGet plugin discovery)
if [ ! -f "$PUBLISH_DIR/netcore/CredentialProvider.Devcontainer.dll" ]; then
  echo ""
  echo "ERROR: CredentialProvider.Devcontainer.dll not found!"
  echo "The build may have produced a self-contained executable instead."
  echo "Check the .csproj for SelfContained/PublishSingleFile settings."
  exit 1
fi

echo ""
echo "   ✓ Found CredentialProvider.Devcontainer.dll"

# Copy install script to publish directory
cp "$SCRIPT_DIR/install.sh" "$PUBLISH_DIR/"
chmod +x "$PUBLISH_DIR/install.sh"

# Run the install script
echo ""
echo "3. Running install.sh..."
"$PUBLISH_DIR/install.sh"

# Verify installation
echo ""
echo "4. Verifying installation..."
if [ -f "$PLUGIN_DEST/CredentialProvider.Devcontainer.dll" ]; then
  echo "   ✓ Plugin installed correctly"
  echo ""
  echo "   Installed files:"
  ls -la "$PLUGIN_DEST/"
else
  echo "   ✗ Installation failed - .dll not found in destination"
  exit 1
fi

# Test that the plugin can be invoked
echo ""
echo "5. Testing plugin invocation..."
if dotnet "$PLUGIN_DEST/CredentialProvider.Devcontainer.dll" --help >/dev/null 2>&1; then
  echo "   ✓ Plugin executes successfully"
  echo ""
  dotnet "$PLUGIN_DEST/CredentialProvider.Devcontainer.dll" --help
else
  echo "   ✗ Plugin failed to execute"
  exit 1
fi

echo ""
echo "=== All tests passed! ==="
echo ""
echo "The credential provider is installed and ready."
echo "Try running 'dotnet restore' against an Devcontainer feed."
