# Azure Artifacts Silent Credential Provider

A silent/headless NuGet credential provider for Azure Artifacts that authenticates **without interactive prompts** - perfect for devcontainers, codespaces, and CI environments.

## Why This Exists

Microsoft's official [artifacts-credprovider](https://github.com/microsoft/artifacts-credprovider) uses device code flow which requires interactive login. This doesn't work well in:

- Devcontainers / GitHub Codespaces
- Headless CI/CD environments
- Automated scripts

This credential provider authenticates silently using:

1. **Environment variable** (`VSS_NUGET_ACCESSTOKEN`) - if already set
2. **Auth helper** (`~/ado-auth-helper`) - from devcontainer features
3. **Azure.Identity** (`DefaultAzureCredential`) - Azure CLI, managed identity, etc.

## Prerequisites

- **.NET 8 runtime** - Download from https://dotnet.microsoft.com/download/dotnet/8.0
- **GitHub CLI** (for private repo downloads) - https://cli.github.com/

## Quick Install

Download and install from GitHub releases using the GitHub CLI:

**Linux/macOS:**

```bash
gh release download -R asidlo/credentialprovider-azureartifacts -p "*.tar.gz" \
  && mkdir -p /tmp/cred-provider \
  && tar xzf credentialprovider-azureartifacts.tar.gz -C /tmp/cred-provider \
  && /tmp/cred-provider/install.sh \
  && rm -rf /tmp/cred-provider credentialprovider-azureartifacts.tar.gz
```

**Windows (PowerShell):**

```powershell
gh release download -R asidlo/credentialprovider-azureartifacts -p "*.zip"
Expand-Archive -Path "credentialprovider-azureartifacts.zip" -DestinationPath "$env:TEMP\cred-provider" -Force
& "$env:TEMP\cred-provider\install.ps1"
Remove-Item -Recurse -Force "$env:TEMP\cred-provider", "credentialprovider-azureartifacts.zip"
```

After installation, just run `dotnet restore` - no environment variables needed!

### Verify Installation

```bash
# Check version
dotnet ~/.nuget/plugins/netcore/CredentialProvider.AzureArtifacts/CredentialProvider.AzureArtifacts.dll --version

# Test credential acquisition
dotnet ~/.nuget/plugins/netcore/CredentialProvider.AzureArtifacts/CredentialProvider.AzureArtifacts.dll --test
```

## Building from Source

```bash
git clone https://github.com/asidlo/credentialprovider-azureartifacts.git
cd credentialprovider-azureartifacts
./scripts/test-local-install.sh
```

## How It Works

This is a **NuGet credential provider plugin** that NuGet automatically calls when it needs authentication. The plugin:

1. Implements the NuGet plugin protocol using the official `NuGet.Protocol` library
2. Handles bidirectional JSON-RPC communication with NuGet
3. Returns credentials silently without prompting the user

### Authentication Flow

When NuGet requests credentials for an Azure Artifacts feed:

1. **Check `VSS_NUGET_ACCESSTOKEN`** - Uses it directly if set
2. **Try auth helper** - Runs `~/ado-auth-helper get-access-token`
3. **Fall back to Azure.Identity** - Uses `DefaultAzureCredential` which tries:
   - Azure CLI (`az login`)
   - Azure PowerShell
   - Visual Studio credentials
   - Environment variables
   - Managed Identity (in Azure)

## Devcontainer Feature (Recommended)

The easiest way to use this in devcontainers is with the published devcontainer feature:

```json
{
  "features": {
    "ghcr.io/asidlo/credentialprovider-azureartifacts/devcontainer-feature:1": {}
  }
}
```

### With Specific Version

```json
{
  "features": {
    "ghcr.io/asidlo/credentialprovider-azureartifacts/devcontainer-feature:1": {
      "version": "v1.0.5"
    }
  }
}
```

## Troubleshooting

### Check if provider is installed

```bash
ls -la ~/.nuget/plugins/netcore/CredentialProvider.AzureArtifacts/
```

### Enable verbose NuGet logging

```bash
NUGET_PLUGIN_LOG_DIRECTORY_PATH=/tmp/nuget-logs dotnet restore -v detailed
cat /tmp/nuget-logs/*.log
```

### Test auth helper

```bash
~/ado-auth-helper get-access-token
```

### Test Azure CLI

```bash
az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv
```

### Uninstall

```bash
rm -rf ~/.nuget/plugins/netcore/CredentialProvider.AzureArtifacts
```

## License

MIT - See [LICENSE](LICENSE)
