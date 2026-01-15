# Security Policy

## Reporting a Vulnerability

Please **do not** open public GitHub issues for suspected security vulnerabilities.

Instead, report privately by emailing:

- **security@TODO-REPLACE-ME**

Include:

- A description of the issue and impact
- Reproduction steps or a proof-of-concept
- Affected versions/commit SHA (if known)

## Scope

This repository includes:

- A NuGet credential provider plugin (handles authentication tokens)
- Install scripts and a devcontainer feature

Any issue that could expose credentials/tokens, enable privilege escalation, or allow arbitrary code execution should be treated as security-sensitive.

## Supported Versions

Security fixes are applied to the latest released version and `main`.
