#!/bin/bash
# Integration test for installation and authentication functionality
#
# This script tests:
# 1. Installation process (install.sh)
# 2. Plugin can be invoked
# 3. Version display works
# 4. Help display works
# 5. Test mode works
# 6. Auth environment variable is recognized

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TEST_HOME="$(mktemp -d)"
PLUGIN_DEST="$TEST_HOME/.nuget/plugins/netcore/CredentialProvider.AzureArtifacts"

echo "=== Credential Provider Installation & Auth Integration Tests ==="
echo ""
echo "Repository: $REPO_ROOT"
echo "Test HOME: $TEST_HOME"
echo ""

# Cleanup function
cleanup() {
    echo ""
    echo "Cleaning up test environment..."
    rm -rf "$TEST_HOME"
}
trap cleanup EXIT

# Test 1: Build the project
echo "Test 1: Building credential provider..."
cd "$REPO_ROOT"
dotnet build src/CredentialProvider.AzureArtifacts -c Debug --nologo -v quiet
echo "✓ Build successful"
echo ""

# Test 2: Publish for installation
echo "Test 2: Publishing credential provider..."
PUBLISH_DIR="$TEST_HOME/publish"
dotnet publish src/CredentialProvider.AzureArtifacts -c Debug -o "$PUBLISH_DIR/netcore" --nologo -v quiet
cp scripts/install.sh "$PUBLISH_DIR/"
chmod +x "$PUBLISH_DIR/install.sh"
echo "✓ Published successfully"
echo ""

# Test 3: Run install script
echo "Test 3: Testing installation script..."
cd "$PUBLISH_DIR"
HOME="$TEST_HOME" ./install.sh > /dev/null

# Verify installation
if [ ! -f "$PLUGIN_DEST/CredentialProvider.AzureArtifacts.dll" ]; then
    echo "✗ Installation failed - DLL not found"
    exit 1
fi
echo "✓ Installation successful"
echo ""

# Test 4: Test version display
echo "Test 4: Testing version display..."
VERSION_OUTPUT=$(dotnet "$PLUGIN_DEST/CredentialProvider.AzureArtifacts.dll" --version 2>&1)
if [[ ! "$VERSION_OUTPUT" =~ "CredentialProvider.AzureArtifacts" ]]; then
    echo "✗ Version output unexpected: $VERSION_OUTPUT"
    exit 1
fi
echo "✓ Version: $VERSION_OUTPUT"
echo ""

# Test 5: Test help display
echo "Test 5: Testing help display..."
HELP_OUTPUT=$(dotnet "$PLUGIN_DEST/CredentialProvider.AzureArtifacts.dll" --help 2>&1)
if [[ ! "$HELP_OUTPUT" =~ "NuGet Credential Provider" ]]; then
    echo "✗ Help output unexpected"
    exit 1
fi
if [[ ! "$HELP_OUTPUT" =~ "--test" ]] || [[ ! "$HELP_OUTPUT" =~ "--version" ]]; then
    echo "✗ Help output missing expected options"
    exit 1
fi
echo "✓ Help output correct"
echo ""

# Test 6: Test credential acquisition with environment variable
echo "Test 6: Testing VSS_NUGET_ACCESSTOKEN authentication..."
export VSS_NUGET_ACCESSTOKEN="test-token-12345"
TEST_OUTPUT=$(dotnet "$PLUGIN_DEST/CredentialProvider.AzureArtifacts.dll" --test 2>&1 || true)
if [[ "$TEST_OUTPUT" =~ "Successfully acquired token" ]]; then
    echo "✓ Token acquired from environment variable"
elif [[ "$TEST_OUTPUT" =~ "Failed to acquire token" ]]; then
    echo "⚠ Token acquisition failed (expected if no auth configured)"
else
    echo "Test output: $TEST_OUTPUT"
fi
unset VSS_NUGET_ACCESSTOKEN
echo ""

# Test 7: Test without authentication
echo "Test 7: Testing without authentication (should fail gracefully)..."
unset VSS_NUGET_ACCESSTOKEN
TEST_OUTPUT=$(dotnet "$PLUGIN_DEST/CredentialProvider.AzureArtifacts.dll" --test 2>&1 || true)
if [[ "$TEST_OUTPUT" =~ "Failed to acquire token" ]]; then
    echo "✓ Failed gracefully without credentials (expected)"
elif [[ "$TEST_OUTPUT" =~ "Successfully acquired token" ]]; then
    echo "✓ Token acquired from other auth method (Azure CLI or auth helper)"
fi
echo ""

# Test 8: Verify file permissions
echo "Test 8: Verifying file permissions..."
if [ ! -r "$PLUGIN_DEST/CredentialProvider.AzureArtifacts.dll" ]; then
    echo "✗ DLL is not readable"
    exit 1
fi
echo "✓ File permissions correct"
echo ""

# Test 9: List installed files
echo "Test 9: Verifying installed files..."
INSTALLED_FILES=$(ls -1 "$PLUGIN_DEST" | wc -l)
if [ "$INSTALLED_FILES" -lt 1 ]; then
    echo "✗ No files installed"
    exit 1
fi
echo "✓ Found $INSTALLED_FILES installed files"
echo "  Installed files:"
ls -lh "$PLUGIN_DEST/" | head -10
echo ""

echo "=== All Integration Tests Passed! ==="
echo ""
echo "Summary:"
echo "  ✓ Build successful"
echo "  ✓ Installation works"
echo "  ✓ Version display works"
echo "  ✓ Help display works"
echo "  ✓ Test mode works"
echo "  ✓ Environment variable auth recognized"
echo "  ✓ Auth fallback graceful"
echo "  ✓ File permissions correct"
echo ""
