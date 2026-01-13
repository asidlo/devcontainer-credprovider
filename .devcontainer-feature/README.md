# Azure Artifacts Credential Provider - Devcontainer Feature

This folder contains a [devcontainer feature](https://containers.dev/implementors/features/) that installs the Azure Artifacts silent credential provider.

## Usage

Add this feature to your `devcontainer.json`:

```json
{
  "features": {
    "ghcr.io/asidlo/credentialprovider-azureartifacts/devcontainer-feature:1": {}
  }
}
```

No additional options or authentication required - the credential provider binaries are embedded in the feature package.

## Requirements

- **.NET 8 Runtime** - Required to run the credential provider. Consider adding the dotnet feature:

```json
{
  "features": {
    "ghcr.io/devcontainers/features/dotnet:2": {},
    "ghcr.io/asidlo/credentialprovider-azureartifacts/devcontainer-feature:1": {}
  }
}
```

## What It Does

1. Copies the embedded credential provider binaries
2. Installs to `~/.nuget/plugins/netcore/CredentialProvider.AzureArtifacts/`
3. Verifies the installation

After installation, `dotnet restore` will automatically use this credential provider for Azure Artifacts feeds.

## How It Works

Unlike features that download at install time, this feature embeds the credential provider binaries directly in the OCI package. This means:

- ✅ Works with private repositories (no GitHub auth needed during container build)
- ✅ No network requests during feature installation
- ✅ Faster installation

The feature is built and published by the `publish-feature.yml` workflow, which compiles the credential provider and embeds it in the feature before publishing to GHCR.

## Publishing the Feature

To publish this feature to GHCR, run the `Publish Devcontainer Feature` workflow from GitHub Actions. The workflow will:

1. Build the credential provider from source
2. Embed the binaries into the feature
3. Publish to `ghcr.io/asidlo/credentialprovider-azureartifacts/devcontainer-feature`

## Testing

```bash
cd .devcontainer-feature
devcontainer features test -f devcontainer-feature
```
