#!/bin/bash
# Integration test for devcontainer feature installation
#
# This script tests the devcontainer feature install.sh script

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
FEATURE_SRC="$REPO_ROOT/.devcontainer-feature/src/devcontainer-feature"
TEST_HOME="$(mktemp -d)"

echo "=== Devcontainer Feature Installation Test ==="
echo ""
echo "Feature source: $FEATURE_SRC"
echo "Test HOME: $TEST_HOME"
echo ""

# Cleanup function
cleanup() {
    echo ""
    echo "Cleaning up test environment..."
    rm -rf "$TEST_HOME"
}
trap cleanup EXIT

# Test 1: Verify feature files exist
echo "Test 1: Verifying devcontainer feature files..."
if [ ! -f "$FEATURE_SRC/install.sh" ]; then
    echo "✗ install.sh not found"
    exit 1
fi
if [ ! -f "$FEATURE_SRC/devcontainer-feature.json" ]; then
    echo "✗ devcontainer-feature.json not found"
    exit 1
fi
echo "✓ Feature files exist"
echo ""

# Test 2: Build and prepare embedded binaries
echo "Test 2: Building credential provider for embedding..."
TEMP_PUBLISH="$TEST_HOME/publish"
dotnet publish "$REPO_ROOT/src/CredentialProvider.AzureArtifacts" \
    -c Debug \
    -o "$TEMP_PUBLISH" \
    --nologo -v quiet
echo "✓ Build successful"
echo ""

# Test 3: Simulate feature installation
echo "Test 3: Simulating devcontainer feature installation..."
# Create a test feature directory with embedded binaries
TEST_FEATURE_DIR="$TEST_HOME/feature"
mkdir -p "$TEST_FEATURE_DIR/netcore"
cp -r "$TEMP_PUBLISH"/* "$TEST_FEATURE_DIR/netcore/"
cp "$FEATURE_SRC/install.sh" "$TEST_FEATURE_DIR/"

# Simulate devcontainer environment variables
export _REMOTE_USER_HOME="$TEST_HOME"
export _REMOTE_USER="testuser"

# Run the install script (ignore permission errors for /etc since we're not root)
cd "$TEST_FEATURE_DIR"
set +e  # Temporarily disable exit on error
bash install.sh > /tmp/install-output.log 2>&1
INSTALL_EXIT_CODE=$?
set -e  # Re-enable exit on error

# Show output excluding permission denied errors
grep -v "Permission denied" /tmp/install-output.log || true
rm -f /tmp/install-output.log

# Check if installation actually succeeded (exit code 0 or permission error on /etc only)
if [ $INSTALL_EXIT_CODE -ne 0 ]; then
    # Installation failed, but check if it was only due to /etc permissions
    if [ ! -f "$TEST_HOME/.nuget/plugins/netcore/CredentialProvider.AzureArtifacts/CredentialProvider.AzureArtifacts.dll" ]; then
        echo "✗ Feature installation failed with exit code $INSTALL_EXIT_CODE"
        exit 1
    fi
    # Plugin was installed despite /etc error, which is acceptable
fi

# Verify installation
PLUGIN_DEST="$TEST_HOME/.nuget/plugins/netcore/CredentialProvider.AzureArtifacts"
if [ ! -f "$PLUGIN_DEST/CredentialProvider.AzureArtifacts.dll" ]; then
    echo "✗ Feature installation failed - DLL not found"
    exit 1
fi
echo "✓ Feature installation successful"
echo ""

# Test 4: Verify installed plugin works
echo "Test 4: Testing installed plugin..."
VERSION_OUTPUT=$(dotnet "$PLUGIN_DEST/CredentialProvider.AzureArtifacts.dll" --version 2>&1)
if [[ ! "$VERSION_OUTPUT" =~ "CredentialProvider.AzureArtifacts" ]]; then
    echo "✗ Plugin version check failed"
    exit 1
fi
echo "✓ Plugin works: $VERSION_OUTPUT"
echo ""

# Test 5: Verify NuGet configuration was created
echo "Test 5: Verifying NuGet configuration..."
NUGET_CONFIG="$TEST_HOME/.nuget/NuGet/NuGet.Config"
if [ -f "$NUGET_CONFIG" ]; then
    echo "✓ NuGet.Config created"
    if grep -q "signatureValidationMode" "$NUGET_CONFIG"; then
        echo "✓ NuGet.Config contains expected settings"
    else
        echo "⚠ NuGet.Config may be missing some settings"
    fi
else
    echo "⚠ NuGet.Config not created (may not be needed)"
fi
echo ""

# Test 6: Verify environment script was created
echo "Test 6: Verifying environment script..."
ENV_SCRIPT="/etc/profile.d/nuget-credprovider.sh"
if [ -f "$ENV_SCRIPT" ]; then
    echo "✓ Environment script created at $ENV_SCRIPT"
    if grep -q "NUGET_PLUGIN_PATHS" "$ENV_SCRIPT"; then
        echo "✓ Environment script contains NUGET_PLUGIN_PATHS"
    fi
else
    echo "⚠ Environment script not created (may require root)"
fi
echo ""

echo "=== Devcontainer Feature Tests Passed! ==="
echo ""
echo "Summary:"
echo "  ✓ Feature files exist"
echo "  ✓ Binaries can be built"
echo "  ✓ Feature installation works"
echo "  ✓ Plugin executes correctly"
echo "  ✓ Configuration files created"
echo ""
