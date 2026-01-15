# Contributing

Thanks for your interest in contributing!

## Quick Start

- Fork the repo and create a feature branch
- Make a focused change with tests/verification notes
- Open a PR targeting `main`

## Development

### Prerequisites

- .NET SDK 8

### Build

```bash
dotnet restore src/CredentialProvider.Devcontainer
dotnet build src/CredentialProvider.Devcontainer -c Release
```

### Run (standalone)

```bash
dotnet run --project src/CredentialProvider.Devcontainer -- --version
dotnet run --project src/CredentialProvider.Devcontainer -- --test
```

### Install locally (Linux/macOS)

```bash
# Build and install with tests
RUN_TESTS=true ./scripts/install.sh

# Install from GitHub release
SOURCE=release ./scripts/install.sh
```

## Pull Requests

Please include:

- What problem the change solves
- How you tested it (commands/output)
- Any security considerations (especially around token handling)

## Security

If your change touches authentication/token flows, avoid logging secrets. If you find a vulnerability, see [SECURITY.md](SECURITY.md).
