# Security policy

## Reporting a vulnerability

Please use [GitHub private vulnerability reporting](https://github.com/BenLampson/Asgard.Brigitte.Desktop/security/advisories/new). Include the affected commit/version, platform, expected security boundary, and a minimal reproduction with synthetic data.

Do not put access or refresh tokens, MCP keys, credentials, private project contents, or active invitation links in public issues. If a private report cannot be submitted, open an issue asking for a private reporting channel without disclosing exploit details.

## Scope and updates

This repository covers the desktop client and its shared synchronization contracts. Fixes target the current main branch; no separate long-term maintenance branches or response-time guarantee are currently offered. Reports concerning the connected service may need coordination with its operator.

The client is a native public OAuth client. Its client ID, tenant/application identifiers, endpoints, and loopback redirect are public configuration. It must not contain a confidential client secret. Server authorization remains required for every resource and command.
