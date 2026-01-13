# Tests

This directory contains comprehensive tests for the Azure Artifacts Credential Provider.

## Test Coverage

### Unit and Integration Tests (C# with xUnit)

Located in `CredentialProvider.AzureArtifacts.Tests/`

All tests are implemented in C# using xUnit for better CI/CD integration and cross-platform support.

#### Authentication Tests (`AuthenticationTests.cs`)
- VSS_NUGET_ACCESSTOKEN environment variable authentication
- Auth helper fallback behavior
- Azure.Identity DefaultAzureCredential fallback
- Cancellation handling for async operations
- Azure DevOps URI detection (8 test cases)
- Authentication fallback chain priority

#### Identity Handling Tests (`IdentityHandlingTests.cs`) 
- Comprehensive authentication priority testing
- Multiple Azure DevOps URI patterns
- Concurrent request handling
- Cancellation and timeout scenarios
- Edge case validation
- Environment variable change handling

#### NuGet Plugin Integration Tests (`NuGetPluginIntegrationTests.cs`)
- Plugin startup and initialization
- Request handler registration
- URI pattern recognition
- Token acquisition flows
- Command-line argument handling (--version, --help, --test)
- Multiple concurrent token requests
- Plugin protocol behavior

#### Installation Tests (`InstallationTests.cs`)
- Plugin directory structure creation  
- Plugin execution verification
- Installation verification

#### Installation Integration Tests (`InstallationIntegrationTests.cs`)
- End-to-end build and publish workflow
- Install script execution
- Plugin version and help display
- Token acquisition with environment variable
- Graceful failure handling
- Devcontainer feature installation
- File permissions and structure validation

## Running Tests

### Run All Tests

```bash
dotnet test
```

### Run Specific Test Class

```bash
dotnet test --filter "FullyQualifiedName~AuthenticationTests"
dotnet test --filter "FullyQualifiedName~NuGetPluginIntegrationTests"
dotnet test --filter "FullyQualifiedName~IdentityHandlingTests"
```

### Run Tests with Verbose Output

```bash
dotnet test --verbosity detailed
```

### Run Tests with Code Coverage

```bash
# Collect coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Generate HTML report (requires ReportGenerator)
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:./coverage/**/coverage.cobertura.xml -targetdir:./coverage/report -reporttypes:"Html;MarkdownSummaryGithub"

# View report
open ./coverage/report/index.html  # macOS
xdg-open ./coverage/report/index.html  # Linux
start ./coverage/report/index.html  # Windows
```

### Run Tests in CI/CD

```bash
dotnet test --configuration Release --logger "trx;LogFileName=test-results.trx"
```

## Test Requirements

- .NET 8 SDK
- No additional dependencies (all tests are self-contained)

## Code Coverage

The project uses Coverlet for code coverage collection and ReportGenerator for report generation.

**CI/CD Integration:**
- Coverage is automatically collected on every build and PR
- Coverage reports are published as build artifacts
- Coverage summary is added to GitHub Actions workflow summary
- Coverage results are posted as PR comments
- Target coverage: 80% (currently tracking and reporting)

**Local Coverage:**
See "Run Tests with Code Coverage" section above for local coverage generation.

## What's Tested

✅ Installation process works as expected  
✅ Devcontainer install works as expected  
✅ All auth methods work (env var, auth helpers, Azure.Identity)  
✅ Auth fallbacks are being used  
✅ NuGet plugin protocol integration  
✅ URI detection for Azure DevOps feeds  
✅ Version and help display  
✅ Graceful failure handling  
✅ Concurrent request handling  
✅ Cancellation and timeout scenarios  

## Test Statistics

- **Total Tests**: 59
- **Authentication Tests**: 7
- **Identity Handling Tests**: 17
- **NuGet Plugin Integration Tests**: 16
- **Installation Tests**: 4  
- **Installation Integration Tests**: 9
- **Old Installation Tests**: 6

All tests are designed to run in CI/CD environments with no interactive components required.

