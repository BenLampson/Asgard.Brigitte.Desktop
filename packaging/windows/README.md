# Windows MSIX packaging

English · [简体中文](README.zh-CN.md) · [Store submission guide](store-submission.md)

This directory contains the manifest template, public registered settings, and PowerShell packaging script. The script builds a package; it does not sign, install, upload, or submit it for certification.

## Requirements and status

Use Windows, PowerShell 7, .NET 10 SDK, the `maui-windows` workload, and the Windows SDK. Running the app also requires Microsoft Edge WebView2 Runtime, which this MSIX does not install automatically.

Asgard Brigitte is registered as Store product `9N5X2FD6C0XC`. The public identity is in [store-settings.public.json](store-settings.public.json). The product is still a draft: package generation does not mean Store certification or publication has completed. Never commit signing keys, passwords, reviewer accounts, or other private submission data.

## Store upload package

From the desktop repository root:

```powershell
pwsh -NoProfile -File packaging/windows/publish-store.ps1 -SettingsPath packaging/windows/store-settings.public.json
```

The unsigned MSIX is generated under `publish/store/1.0.0.0/packages/`. It declares the registered name and publisher, `en-US` and `zh-CN`, and the native desktop `runFullTrust` capability. .NET and Windows App SDK are included. The policy and GitHub support links are embedded in the app and accessible before sign-in.

Upload the `.msix` to this product in Partner Center. Microsoft signs accepted Store packages, so a purchased certificate is not required merely for Store submission. The unsigned upload file is not an ordinary sideload installer; direct MSIX installation requires an appropriate signing and trust setup.

For a different authorized product or version, copy `store-settings.example.json` to an ignored local settings file and supply the exact Partner Center identity. The script validates URL format, not the content or availability of the linked pages.

## Packaging preview

```powershell
pwsh -NoProfile -File packaging/windows/publish-store.ps1 -Preview
```

Preview output is under `publish/store-preview/1.0.0.0/`, using `Asgard.Brigitte.PackagingPreview` / `CN=BrigittePackagingPreview`. It must not be uploaded as the formal Store product or distributed as a signed installer.

## Release validation

Use an isolated test device or account to check installation, upgrade and uninstall; browser sign-in and restoration; single-instance activation; tray, notifications and independent windows; attachment operations; both app languages and system-language fallback; and privacy/support links. Check WebView2 absence, display scaling, and keyboard access. Run the Windows App Certification Kit and resolve Partner Center warnings before submitting.

Store descriptions must disclose the account, network, and compatible-service requirements. A reviewer needs working private test credentials and a synthetic project. See the [English listing](store-listing.en.md), [Chinese listing](store-listing.zh-CN.md), and [submission guide](store-submission.md).
