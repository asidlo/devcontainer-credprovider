#!/bin/bash
# Master test runner - runs all tests
#
# This script runs all test suites to ensure comprehensive coverage

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "========================================="
echo "Running All Tests"
echo "========================================="
echo ""

# Track results
TESTS_PASSED=0
TESTS_FAILED=0

# Function to run test and track results
run_test() {
    local test_name="$1"
    local test_command="$2"
    
    echo "----------------------------------------"
    echo "Running: $test_name"
    echo "----------------------------------------"
    
    if eval "$test_command"; then
        echo "✓ $test_name PASSED"
        TESTS_PASSED=$((TESTS_PASSED + 1))
    else
        echo "✗ $test_name FAILED"
        TESTS_FAILED=$((TESTS_FAILED + 1))
        return 1
    fi
    echo ""
}

cd "$REPO_ROOT"

# Run unit tests
run_test "Unit Tests" "dotnet test --nologo --verbosity minimal"

# Run integration tests
run_test "Installation & Auth Integration Test" "$SCRIPT_DIR/test-installation-and-auth.sh"
run_test "Devcontainer Feature Test" "$SCRIPT_DIR/test-devcontainer-feature.sh"

echo "========================================="
echo "Test Summary"
echo "========================================="
echo "Passed: $TESTS_PASSED"
echo "Failed: $TESTS_FAILED"
echo ""

if [ $TESTS_FAILED -eq 0 ]; then
    echo "✓ All tests passed!"
    exit 0
else
    echo "✗ Some tests failed"
    exit 1
fi
