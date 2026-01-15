# Devcontainer Credential Provider - Devcontainer Feature

This folder contains a [devcontainer feature](https://containers.dev/implementors/features/) that installs the Devcontainer silent credential provider.

## Usage

Add this feature to your `devcontainer.json`:

```json
{
  "features": {
    "ghcr.io/asidlo/features/devcontainer-credprovider:2": {}
  }
}
```

No additional configuration required - the credential provider binaries are embedded in the feature package.

## How It Works

This feature installs two credential providers:

1. **Devcontainer Credential Provider** (this plugin) - Uses auth helpers for silent authentication
2. **Microsoft artifacts-credprovider** - Fallback for device code flow when auth helpers are unavailable

The `NUGET_PLUGIN_PATH` is automatically set to check the devcontainer provider first, then fall back to artifacts-credprovider:

```bash
NUGET_PLUGIN_PATH="/usr/local/share/nuget/plugins/custom:/usr/local/share/nuget/plugins/azure"
```

## Requirements

- **.NET 8 Runtime** - Required to run the credential provider. Consider adding the dotnet feature:

```json
{
  "features": {
    "ghcr.io/devcontainers/features/dotnet:2": {},
    "ghcr.io/asidlo/features/devcontainer-credprovider:2": {}
  }
}
```

## What It Does

1. Installs the devcontainer credential provider to `/usr/local/share/nuget/plugins/custom/`
2. Installs Microsoft's artifacts-credprovider to `/usr/local/share/nuget/plugins/azure/`
3. Configures `NUGET_PLUGIN_PATH` to use both providers (via `containerEnv`)
4. Configures NuGet environment variables for non-interactive authentication
5. Verifies the installation

After installation, `dotnet restore` will automatically use this credential provider for Azure Artifacts feeds. C# DevKit will also use this credential provider.

## Authentication Flow

1. **Try auth helpers** - Runs `~/ado-auth-helper` or `/usr/local/bin/ado-auth-helper`
2. **Fall back to artifacts-credprovider** - If auth helpers are unavailable, returns NotApplicable and NuGet tries the next provider (Microsoft's artifacts-credprovider with device code flow)

## Publishing the Feature

To publish this feature to GHCR, run the `Publish Devcontainer Feature` workflow from GitHub Actions. The workflow will:

1. Build the credential provider from source
2. Embed the binaries into the feature
3. Publish to `ghcr.io/asidlo/features/devcontainer-credprovider`

## Testing

```bash
cd .devcontainer-feature
devcontainer features test -f devcontainer-credprovider
```
