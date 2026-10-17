<p align="center"><img src="src/Asgard.Brigitte.Desktop/Resources/AppIcon/appicon.svg" width="88" alt="Brigitte" /></p>

<h1 align="center">Brigitte Desktop</h1>

<p align="center">A native bug collaboration client built with .NET MAUI and Blazor Hybrid.</p>

<p align="center">
  <a href="https://github.com/BenLampson/Asgard.Brigitte.Desktop/actions/workflows/ci.yml"><img src="https://github.com/BenLampson/Asgard.Brigitte.Desktop/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-Apache--2.0-blue.svg" alt="Apache-2.0" /></a>
  <img src="https://img.shields.io/badge/.NET-10-512BD4.svg" alt=".NET 10" />
</p>

<p align="center">English · <a href="README.zh-CN.md">简体中文</a></p>

Brigitte connects issue discovery, assignment, fixes, and verification in a shared project workspace. The desktop client combines native windows, a system tray, notifications, and a local SQLite projection updated over authenticated WebSockets.

This repository publishes the desktop client, shared synchronization contracts, bundled application resources, and tests under Apache-2.0. The Brigitte backend, web administration application, and production deployment are maintained separately in a private repository. A compatible Brigitte API and Heimdall identity service are required; the client does not include a local standalone server.

## Features

- Project boards, search and filters, bug editing, status transitions, comments, and activity history.
- Personal projects, member management, and invitation links with optional passwords and expiration, subject to server authorization.
- Markdown editing, attachments, drag and drop, image paste, previews, and downloads.
- Browser-based OIDC/PKCE login, platform secure credential storage, session restoration, and refresh.
- Incremental synchronization, local SQLite reads, unread activity, desktop notifications, and a system tray.
- Separate task windows, light/dark/system themes, and persistent window preferences.
- Copyable AI repair prompts and MCP Key management for external tools. No AI model is bundled.

## Platform status

Windows is the primary development platform and is built in CI using MAUI, WinUI 3, and WebView2. macOS sources use Mac Catalyst and an AppKit tray bridge; building and validating them requires a Mac with Xcode. There is no Linux desktop host, but the Core/UI tests can run without MAUI. The current application version is `0.1.0`. Store certification, macOS signing, and notarization remain separate release work.

## Build on Windows

Install .NET SDK `10.0.401` or a compatible patch in the same feature band, the Windows SDK, and WebView2 Runtime, then run:

```powershell
git clone https://github.com/BenLampson/Asgard.Brigitte.Desktop.git
cd Asgard.Brigitte.Desktop
dotnet workload install maui-windows
dotnet build src/Asgard.Brigitte.Desktop/Asgard.Brigitte.Desktop.csproj -c Release
dotnet run --project src/Asgard.Brigitte.Desktop/Asgard.Brigitte.Desktop.csproj -f net10.0-windows10.0.19041.0
```

Open `Asgard.Brigitte.Desktop.slnx` to work on all desktop projects. The repository is self-contained and restores dependencies from public NuGet feeds; no private backend checkout is needed.

The embedded configuration points to the existing Brigitte service and contains public native-client identifiers, with no client secret. Accounts and data access follow that service's policies. To target your own compatible service, update both `Resources/Raw/clientsettings.json` in the MAUI project and the defaults in `Desktop.Core/ClientOptions.cs`, then rebuild. Environment variables do not override this configuration.

## Development and documentation

```powershell
dotnet test --project tests/Asgard.Brigitte.Desktop.Tests/Asgard.Brigitte.Desktop.Tests.csproj -c Release
```

The tests use local transports and local OIDC fixtures. Core, Razor UI, and shared synchronization contracts are independent of the MAUI host. See the Chinese-language [development guide](docs/development.md), [usage guide](docs/usage.md), [synchronization protocol](src/Asgard.Brigitte.Desktop/SYNC.md), [data handling notes](docs/data.md), and [Windows packaging guide](packaging/windows/README.md).

Contributions are welcome through [issues](https://github.com/BenLampson/Asgard.Brigitte.Desktop/issues) and pull requests. Read [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md) first. Code and original assets are licensed under [Apache-2.0](LICENSE); see [NOTICE](NOTICE). Third-party dependencies retain their own licenses. The license does not grant trademark rights to the Brigitte or Asgard names or marks.
