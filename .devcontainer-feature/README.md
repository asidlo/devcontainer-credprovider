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

### With Options

```json
{
  "features": {
    "ghcr.io/asidlo/credentialprovider-azureartifacts/devcontainer-feature:1": {
      "version": "v1.0.5",
      "repository": "asidlo/credentialprovider-azureartifacts"
    }
  }
}
```

## Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `version` | string | `latest` | Version to install (e.g., 'latest', 'v1.0.0', '1.2.3') |
| `repository` | string | `asidlo/credentialprovider-azureartifacts` | GitHub repository to download from |

## Requirements

- **GitHub CLI** - Required for private repositories. The feature will fall back to `curl` for public repos.
- **.NET 8 Runtime** - Required to run the credential provider.

## What It Does

1. Downloads the credential provider from GitHub releases
2. Extracts and installs to `~/.nuget/plugins/netcore/CredentialProvider.AzureArtifacts/`
3. Verifies the installation

After installation, `dotnet restore` will automatically use this credential provider for Azure Artifacts feeds.

## Publishing the Feature

To publish this feature to GHCR:

```bash
# From the repo root
cd .devcontainer-feature
devcontainer features publish -r ghcr.io -n asidlo/credentialprovider-azureartifacts ./src
```

## Testing

```bash
cd .devcontainer-feature
devcontainer features test -f devcontainer-feature
```
