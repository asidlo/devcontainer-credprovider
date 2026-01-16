# Devcontainer Credential Provider

A silent/headless NuGet credential provider for devcontainers and container environments that authenticates **without interactive prompts** - perfect for devcontainers, codespaces, and CI environments.

## Why This Exists

Microsoft's official [artifacts-credprovider](https://github.com/microsoft/artifacts-credprovider) uses device code flow which requires interactive login. This doesn't work well in:

- Devcontainers / GitHub Codespaces
- Headless CI/CD environments
- Automated scripts

This credential provider authenticates silently using:

1. **Auth helpers** (`~/ado-auth-helper`) - from devcontainer features
2. **Fallback to artifacts-credprovider** - for device code flow when auth helpers are unavailable

## Prerequisites

- **.NET 8 runtime** - Download from https://dotnet.microsoft.com/download/dotnet/8.0
- **GitHub CLI** (for private repo downloads) - https://cli.github.com/

## Quick Install

Download and install from GitHub releases using the GitHub CLI:

**Linux/macOS:**

```bash
gh release download -R asidlo/devcontainer-credprovider -p "*.tar.gz" \
  && mkdir -p /tmp/cred-provider \
  && tar xzf devcontainer-credprovider.tar.gz -C /tmp/cred-provider \
  && /tmp/cred-provider/install.sh \
  && rm -rf /tmp/cred-provider devcontainer-credprovider.tar.gz
```

**Windows (PowerShell):**

```powershell
gh release download -R asidlo/devcontainer-credprovider -p "*.zip"
Expand-Archive -Path "devcontainer-credprovider.zip" -DestinationPath "$env:TEMP\cred-provider" -Force
& "$env:TEMP\cred-provider\install.ps1"
Remove-Item -Recurse -Force "$env:TEMP\cred-provider", "devcontainer-credprovider.zip"
```

After installation, just run `dotnet restore` - no environment variables needed!

### Verify Installation

```bash
# Check version
dotnet /usr/local/share/nuget/plugins/custom/CredentialProvider.Devcontainer.dll --version

# Test credential acquisition
dotnet /usr/local/share/nuget/plugins/custom/CredentialProvider.Devcontainer.dll --test
```

## Release Verification

Each GitHub release includes:

- `checksums.sha256` (SHA-256 of the `.tar.gz` and `.zip`)
- Sigstore (cosign) signatures: `*.sig` and certificates: `*.cert`

Verify checksums:

```bash
sha256sum -c checksums.sha256
```

Verify Sigstore signatures (requires `cosign`):

```bash
cosign verify-blob \
  --signature devcontainer-credprovider.tar.gz.sig \
  --certificate devcontainer-credprovider.tar.gz.cert \
  devcontainer-credprovider.tar.gz
```

## Building from Source

```bash
git clone https://github.com/asidlo/devcontainer-credprovider.git
cd devcontainer-credprovider
RUN_TESTS=true ./scripts/install.sh
```

## How It Works

This is a **NuGet credential provider plugin** that NuGet automatically calls when it needs authentication. The plugin:

1. Implements the NuGet plugin protocol using the official `NuGet.Protocol` library
2. Handles bidirectional JSON-RPC communication with NuGet
3. Returns credentials silently without prompting the user
4. Falls back to other providers (like artifacts-credprovider) if auth helpers are unavailable

### Authentication Flow

When NuGet requests credentials for an Azure Artifacts feed:

1. **Validate 2FA (if configured)** - Verifies TOTP code if `NUGET_CREDPROVIDER_2FA_SECRET` is set
2. **Try auth helpers** - Runs `~/ado-auth-helper get-access-token`
3. **Fall back to artifacts-credprovider** - If auth helpers are unavailable, returns `NotApplicable` to let NuGet try the next provider (Microsoft's artifacts-credprovider with device code flow)

### Two-Factor Authentication (2FA)

Optional TOTP-based 2FA support using authenticator apps (Google Authenticator, Microsoft Authenticator, etc.):

**Quick Setup (Recommended):**

1. Run the setup command to auto-generate a secret and display a QR code:

```bash
dotnet /usr/local/share/nuget/plugins/custom/CredentialProvider.Devcontainer.dll --setup-2fa
```

This will:
- Auto-generate a TOTP secret (saved to `~/.nuget-credprovider-2fa-secret`)
- Display a QR code you can scan with your authenticator app
- Show the current code for verification

2. Scan the QR code with your authenticator app

3. Enable 2FA in your devcontainer:

```json
{
  "remoteEnv": {
    "NUGET_CREDPROVIDER_2FA_ENABLED": "true",
    "NUGET_CREDPROVIDER_2FA_CODE": "123456"  // Current code from your app
  }
}
```

4. Before running `dotnet restore`, update `NUGET_CREDPROVIDER_2FA_CODE` with the current code from your authenticator app

**Environment Variables:**

- `NUGET_CREDPROVIDER_2FA_ENABLED`: Set to `true` to enable 2FA validation (optional, defaults to `false`)
- `NUGET_CREDPROVIDER_2FA_SECRET`: Base32-encoded TOTP secret (optional, auto-generated and stored if not provided)
- `NUGET_CREDPROVIDER_2FA_CODE`: Current 6-digit TOTP code from your authenticator app (required when enabled)

**Advanced: Manual Secret Setup:**

If you prefer to manage the secret yourself (e.g., via Codespaces secrets):

```json
{
  "remoteEnv": {
    "NUGET_CREDPROVIDER_2FA_ENABLED": "true",
    "NUGET_CREDPROVIDER_2FA_SECRET": "YOUR_BASE32_SECRET",
    "NUGET_CREDPROVIDER_2FA_CODE": "123456"
  }
}
```

**Notes:**

- 2FA is **completely optional** - if `NUGET_CREDPROVIDER_2FA_ENABLED` is not set or `false`, authentication works normally
- Secret is auto-generated on first use and persisted to `~/.nuget-credprovider-2fa-secret`
- Supports clock skew tolerance (±30 seconds)
- Compatible with all standard authenticator apps (Google Authenticator, Microsoft Authenticator, Authy, etc.)
- Run `--setup-2fa` anytime to view your QR code again

**Security Considerations:**

- The secret file `~/.nuget-credprovider-2fa-secret` is automatically set to user-read-only permissions
- For shared environments, use `NUGET_CREDPROVIDER_2FA_SECRET` environment variable with GitHub Codespaces secrets
- Update `NUGET_CREDPROVIDER_2FA_CODE` before each session (codes expire every 30 seconds)

## Devcontainer Feature (Recommended)

The easiest way to use this in devcontainers is with the published devcontainer feature:

```json
{
  "features": {
    "ghcr.io/asidlo/features/devcontainer-credprovider:2": {}
  }
}
```

The feature automatically:
- Installs the devcontainer credential provider to `/usr/local/share/nuget/plugins/custom/`
- Installs Microsoft's artifacts-credprovider to `/usr/local/share/nuget/plugins/azure/`
- Sets `NUGET_PLUGIN_PATH` so both CLI and C# DevKit can find the providers

## Troubleshooting

### Check if provider is installed

```bash
# Check devcontainer provider
ls -la /usr/local/share/nuget/plugins/custom/

# Check artifacts-credprovider fallback
ls -la /usr/local/share/nuget/plugins/azure/
```

### C# DevKit Integration

The feature automatically sets `NUGET_PLUGIN_PATH` so C# DevKit will use the credential providers. No additional configuration needed.

If you're not using the devcontainer feature and need to configure manually:

```json
{
  "remoteEnv": {
    "NUGET_PLUGIN_PATH": "/usr/local/share/nuget/plugins/custom:/usr/local/share/nuget/plugins/azure"
  }
}
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

### Test Azure CLI (for artifacts-credprovider fallback)

```bash
az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv
```

### Uninstall

```bash
# Remove devcontainer provider
rm -rf /usr/local/share/nuget/plugins/custom

# Remove artifacts-credprovider fallback
rm -rf /usr/local/share/nuget/plugins/azure

# Remove environment configuration
rm -f /etc/profile.d/nuget-credprovider.sh
```

## License

MIT - See [LICENSE](LICENSE)

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Security

Please report security issues privately. See [SECURITY.md](SECURITY.md).
