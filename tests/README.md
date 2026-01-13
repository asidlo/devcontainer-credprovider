# Tests

This directory contains comprehensive tests for the Azure Artifacts Credential Provider.

## Test Coverage

### Unit Tests (C# with xUnit)

Located in `CredentialProvider.AzureArtifacts.Tests/`

- **AuthenticationTests.cs** - Tests for authentication methods and fallback behavior:
  - VSS_NUGET_ACCESSTOKEN environment variable authentication
  - Auth helper fallback
  - Azure.Identity fallback
  - Cancellation handling
  - URI detection for Azure DevOps feeds
  - Fallback chain priority

- **InstallationTests.cs** - Tests for installation functionality:
  - Plugin directory structure
  - Plugin execution (version, help)
  - Installation verification

### Integration Tests (Shell Scripts)

- **test-installation-and-auth.sh** - End-to-end installation and authentication test:
  - Builds the credential provider
  - Tests the install.sh script
  - Verifies plugin execution (--version, --help, --test)
  - Tests authentication with VSS_NUGET_ACCESSTOKEN
  - Tests graceful failure without authentication
  - Verifies file permissions and installed files

- **test-devcontainer-feature.sh** - Devcontainer feature installation test:
  - Verifies devcontainer feature files exist
  - Builds binaries for embedding
  - Simulates devcontainer feature installation
  - Verifies plugin execution
  - Checks NuGet configuration creation

## Running Tests

### Run All Tests (Recommended)

```bash
./tests/run-all-tests.sh
```

This runs all unit tests and integration tests in sequence.

### Run All Unit Tests

```bash
dotnet test
```

### Run Specific Test Class

```bash
dotnet test --filter "FullyQualifiedName~AuthenticationTests"
```

### Run Integration Tests

```bash
# Installation and authentication test
./tests/test-installation-and-auth.sh

# Devcontainer feature test
./tests/test-devcontainer-feature.sh
```

## Test Requirements

- .NET 8 SDK
- bash (for shell integration tests)
- Standard Unix tools (tar, cp, mkdir, etc.)

## What's Tested

✅ Installation process works as expected  
✅ Devcontainer install works as expected  
✅ All auth methods work (env var, auth helpers, Azure.Identity)  
✅ Auth fallbacks are being used  
✅ URI detection for Azure DevOps feeds  
✅ Version and help display  
✅ Graceful failure handling  

## CI/CD Integration

Tests are designed to run in CI/CD environments:
- Unit tests run via `dotnet test`
- Integration tests can run in any bash environment
- No interactive authentication required
- All tests handle missing dependencies gracefully
