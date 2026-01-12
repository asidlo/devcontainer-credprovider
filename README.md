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

## Quick Install

Download and install from GitHub releases:

**Linux (x64):**

```bash
mkdir -p /tmp/cred-provider && curl -fsSL https://github.com/YOUR_ORG/azure-artifacts-silent-credprovider/releases/latest/download/credential-provider-linux.tar.gz | tar xz -C /tmp/cred-provider && /tmp/cred-provider/install.sh && rm -rf /tmp/cred-provider
```

**Linux (ARM64):**

```bash
mkdir -p /tmp/cred-provider && curl -fsSL https://github.com/YOUR_ORG/azure-artifacts-silent-credprovider/releases/latest/download/credential-provider-linux-arm64.tar.gz | tar xz -C /tmp/cred-provider && /tmp/cred-provider/install.sh && rm -rf /tmp/cred-provider
```

**macOS (Intel):**

```bash
mkdir -p /tmp/cred-provider && curl -fsSL https://github.com/YOUR_ORG/azure-artifacts-silent-credprovider/releases/latest/download/credential-provider-osx.tar.gz | tar xz -C /tmp/cred-provider && /tmp/cred-provider/install.sh && rm -rf /tmp/cred-provider
```

**macOS (Apple Silicon):**

```bash
mkdir -p /tmp/cred-provider && curl -fsSL https://github.com/YOUR_ORG/azure-artifacts-silent-credprovider/releases/latest/download/credential-provider-osx-arm64.tar.gz | tar xz -C /tmp/cred-provider && /tmp/cred-provider/install.sh && rm -rf /tmp/cred-provider
```

> **Note:** Replace `YOUR_ORG/azure-artifacts-silent-credprovider` with your actual GitHub repository.

After installation, just run `dotnet restore` - no environment variables needed!

## Building from Source

```bash
git clone https://github.com/YOUR_ORG/azure-artifacts-silent-credprovider.git
cd azure-artifacts-silent-credprovider
dotnet publish src/CredentialProvider.AzureArtifacts -c Release -o publish
cp scripts/install.sh publish/
./publish/install.sh
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

## Devcontainer Integration

Add this to your `.devcontainer/devcontainer.json`:

```json
{
  "postCreateCommand": "bash .devcontainer/scripts/setup-nuget.sh"
}
```

Create `.devcontainer/scripts/setup-nuget.sh`:

```bash
#!/bin/bash
mkdir -p /tmp/cred-provider \
  && curl -fsSL https://github.com/YOUR_ORG/azure-artifacts-silent-credprovider/releases/latest/download/credential-provider-linux.tar.gz \
  | tar xz -C /tmp/cred-provider \
  && /tmp/cred-provider/install.sh \
  && rm -rf /tmp/cred-provider

# Now restore works automatically
dotnet restore
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
