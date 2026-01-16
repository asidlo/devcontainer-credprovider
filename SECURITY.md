# Security Policy

## Reporting a Vulnerability

Please **do not** open public GitHub issues for suspected security vulnerabilities.

Instead, use GitHub's private vulnerability reporting:

1. Go to the **Security** tab of this repository
2. Click **Report a vulnerability**
3. Fill in the details

Or email: **asidlo@users.noreply.github.com**

Include:

- A description of the issue and impact
- Reproduction steps or a proof-of-concept
- Affected versions/commit SHA (if known)

We aim to respond within 48 hours and will work with you to understand and address the issue.

## Scope

This repository includes:

- A NuGet credential provider plugin (handles authentication tokens)
- Install scripts and a devcontainer feature

Any issue that could expose credentials/tokens, enable privilege escalation, or allow arbitrary code execution should be treated as security-sensitive.

## Security Measures

This project implements the following security practices:

- **Signed releases** - All release artifacts are signed with Sigstore cosign (keyless)
- **CodeQL analysis** - Automated code scanning on every PR and push
- **Dependabot** - Automated dependency updates for NuGet packages and GitHub Actions
- **Pinned actions** - All GitHub Actions are pinned to SHA hashes to prevent supply chain attacks
- **Minimal permissions** - Workflows use least-privilege permissions
- **Code review** - CODEOWNERS requires review for security-sensitive files
- **Two-factor authentication** - Optional TOTP-based 2FA support for additional security

## Two-Factor Authentication (2FA)

This credential provider supports optional TOTP-based two-factor authentication for enhanced security:

- **Environment-based**: Configure via `NUGET_CREDPROVIDER_2FA_SECRET` and `NUGET_CREDPROVIDER_2FA_CODE`
- **Standard TOTP**: Compatible with Google Authenticator, Microsoft Authenticator, Authy, etc.
- **Optional**: 2FA is opt-in; existing workflows continue to work without configuration
- **Clock skew tolerance**: Accepts codes from ±30 seconds to handle minor time differences

**Best Practices:**

1. Store TOTP secrets in secure secret management systems (GitHub Codespaces secrets, Azure Key Vault, etc.)
2. Never commit `NUGET_CREDPROVIDER_2FA_SECRET` to source control
3. Rotate TOTP secrets periodically
4. Use 2FA in addition to, not instead of, other security measures

For setup instructions, see the [README](README.md#two-factor-authentication-2fa).

## Verifying Releases

Releases are signed with Sigstore. To verify:

```bash
# Install cosign: https://docs.sigstore.dev/system_config/installation/
cosign verify-blob \
  --signature devcontainer-credprovider.tar.gz.sig \
  --certificate devcontainer-credprovider.tar.gz.cert \
  --certificate-identity-regexp "https://github.com/asidlo/devcontainer-credprovider/.*" \
  --certificate-oidc-issuer "https://token.actions.githubusercontent.com" \
  devcontainer-credprovider.tar.gz
```

## Supported Versions

Security fixes are applied to the latest released version and `main`.

| Version | Supported          |
| ------- | ------------------ |
| latest  | :white_check_mark: |
| < 1.0   | :x:                |

