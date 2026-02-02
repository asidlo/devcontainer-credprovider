# Copilot Instructions for Devcontainer Credential Provider

## Project Overview

A **NuGet credential provider plugin** for headless/silent authentication in devcontainers and CI environments. It implements the [NuGet cross-platform authentication plugin protocol](https://github.com/NuGet/Home/wiki/NuGet-cross-plat-authentication-plugin) to provide credentials for Azure Artifacts feeds without interactive prompts.

### Architecture

- **Single-file plugin** ([src/CredentialProvider.Devcontainer/Program.cs](../src/CredentialProvider.Devcontainer/Program.cs)): Contains all logic including configuration, request handlers, and authentication
- **NuGet plugin protocol**: Uses `NuGet.Protocol` library for bidirectional JSON-RPC communication with NuGet client
- **Authentication chain**: Tries auth helpers (`~/ado-auth-helper`, `/usr/local/bin/azure-auth-helper`), returns `NotApplicable` to allow fallback to other providers (like Microsoft's artifacts-credprovider)

### Key Request Handlers

| Handler | Purpose |
|---------|---------|
| `GetOperationClaimsRequestHandler` | Tells NuGet this plugin handles Azure DevOps authentication |
| `GetAuthenticationCredentialsRequestHandler` | Returns access tokens for Azure feeds |
| `Disabled*` handlers | Return `NotApplicable` when plugin is disabled via config |

## Build & Test Commands

```bash
# Build and install locally with tests (primary development workflow)
RUN_TESTS=true ./scripts/install.sh

# Run tests only
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~AuthenticationTests"

# Test credential acquisition manually
dotnet run --project src/CredentialProvider.Devcontainer -- --test

# Show current configuration
dotnet run --project src/CredentialProvider.Devcontainer -- --config
```

Use VS Code tasks: "Install: Build Locally" runs the full build+test workflow.

## Project Conventions

### Code Structure

- **All plugin logic in `Program.cs`**: No separate class files - handlers, config, and auth logic are co-located
- **Internal visibility**: Classes/methods use `internal` with `InternalsVisibleTo` for test access
- **Singleton config**: `PluginConfig.Instance` loads from env vars and config file (`~/.config/devcontainer-credprovider/config.json`)

### Versioning

Uses **MinVer** for git-tag-based versioning:
- Tagged commits (e.g., `v1.2.3`) → version `1.2.3`
- Untagged commits → version `1.2.4-alpha.0.N`
- Tag prefix: `v` (configured in csproj)

### Test Patterns

Tests are in `tests/CredentialProvider.Devcontainer.Tests/` using xUnit:
- Call `Program.*` methods directly (internal visibility)
- Use `[Theory]` with `[InlineData]` for parameterized tests (see `IsAzureDevOpsUri` tests)
- Integration tests run actual install scripts and verify plugin behavior

### Installation Paths

```
/usr/local/share/nuget/plugins/custom/CredentialProvider.Devcontainer/  # This plugin
/usr/local/share/nuget/plugins/azure/                                    # Microsoft fallback
```

## Devcontainer Feature

Located in `.devcontainer-feature/src/devcontainer-credprovider/`:
- `install.sh`: Copies embedded binaries, installs Microsoft fallback provider
- `devcontainer-feature.json`: Sets `NUGET_PLUGIN_PATHS` and timeout env vars
- Feature version (`2.x`) is independent of plugin version

## Security Considerations

- **Never log tokens**: Use `Program.Log()` which only logs in verbose mode
- **Token handling**: Tokens are passed directly as NuGet passwords, never stored to disk
- **Auth helper validation**: Check output doesn't start with "Error" before using as token
