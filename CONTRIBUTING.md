# Contributing

Thanks for helping improve Brigitte Desktop. Bug reports, documentation fixes, accessibility improvements, and focused pull requests are welcome in Chinese or English.

## Before making a change

Check existing issues first. For a substantial feature or a synchronization/authentication change, open an issue describing the behavior and compatibility requirements. The backend is maintained separately; this repository must continue to build without a private checkout.

## Development

Use the SDK specified in `global.json`. Read [docs/development.md](docs/development.md) for Windows and macOS setup. Keep package versions in `Directory.Packages.props` and preserve the Core/UI/Sync dependency boundary.

Use C# 14, nullable annotations, UTF-8, and the existing file conventions. Explain security, synchronization, and platform-specific decisions in comments. Keep changes focused and avoid unrelated formatting.

Run the relevant checks:

```powershell
dotnet test --project tests/Asgard.Brigitte.Desktop.Tests/Asgard.Brigitte.Desktop.Tests.csproj -c Release
dotnet build src/Asgard.Brigitte.Desktop/Asgard.Brigitte.Desktop.csproj -c Release
```

The second command requires the platform's MAUI workload. Test native windows, tray behavior, notifications, and file operations on the affected operating system. State what you could verify and what still needs device testing. Add tests when a change introduces behavior that needs regression coverage.

## Pull requests

Describe the user-visible problem, the resulting behavior, and validation. Include screenshots for visual changes using synthetic data. Keep write requests online, read committed workspace data from SQLite, and coordinate wire-contract changes with server compatibility.

Never include tokens, passwords, invitation links, signing keys, production logs, or personal workspace data. Report vulnerabilities through [SECURITY.md](SECURITY.md).

By submitting a contribution, you agree that it may be distributed under this repository's Apache-2.0 license. Third-party code or assets must carry compatible licensing and their required notices.
