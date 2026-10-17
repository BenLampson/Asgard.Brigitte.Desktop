# 开发说明

## 环境

- .NET 10 SDK：`global.json` 要求 `10.0.401`，允许同功能带的较新补丁。
- Windows 宿主：Windows SDK、`maui-windows` workload 和 WebView2 Runtime。
- macOS 宿主：Mac、兼容的 Xcode 和 `maui-maccatalyst` workload。
- Core/UI 测试只需要 .NET SDK，不需要安装 MAUI 或连接线上服务。

`Directory.Build.props` 与 `Directory.Packages.props` 都在本仓库内，公开 NuGet 依赖统一版本管理。Core、UI 和同步契约没有服务端项目引用。

## 构建与测试

Windows：

```powershell
dotnet workload install maui-windows
dotnet build src/Asgard.Brigitte.Desktop/Asgard.Brigitte.Desktop.csproj -c Release
dotnet run --project src/Asgard.Brigitte.Desktop/Asgard.Brigitte.Desktop.csproj -f net10.0-windows10.0.19041.0
```

Mac：

```sh
dotnet workload install maui-maccatalyst
dotnet build src/Asgard.Brigitte.Desktop/Asgard.Brigitte.Desktop.csproj -c Release -f net10.0-maccatalyst
```

Mac Catalyst 会调用 `Platforms/MacCatalyst/Native/build.sh` 编译 AppKit 桥接库。发行时需要匹配的代码签名和公证；Windows CI 不验证这些系统能力。

平台无关测试：

```powershell
dotnet test --project tests/Asgard.Brigitte.Desktop.Tests/Asgard.Brigitte.Desktop.Tests.csproj -c Release
```

测试覆盖会话恢复与续期、登录回调、HTTP 会话隔离、同步批次与回滚、偏好存储、Markdown 过滤和 Razor 渲染。OIDC 测试使用临时 RSA 签名及本地 JWKS，业务请求使用测试传输，不需要线上账号。loopback 测试会临时监听本机端口。原生窗口、托盘、文件选择器和真实登录仍需在对应系统手动验收。

Windows x64 便携版：

```powershell
dotnet publish src/Asgard.Brigitte.Desktop/Asgard.Brigitte.Desktop.csproj -c Release -f net10.0-windows10.0.19041.0 -p:RuntimeIdentifier=win-x64 -p:WindowsPackageType=None -p:SelfContained=true -p:WindowsAppSDKSelfContained=true -p:PublishTrimmed=false -o publish/windows-x64
```

MSIX 清单、预览与正式打包见 [Windows 打包](../packaging/windows/README.md)。构建脚本不会自动安装、签名或上传应用。

## 连接兼容服务

应用从 MAUI 程序集内嵌的 `src/Asgard.Brigitte.Desktop/Resources/Raw/clientsettings.json` 加载配置，不读取环境变量覆盖。`src/Asgard.Brigitte.Desktop.Core/ClientOptions.cs` 保存相同默认值；切换服务时同步修改两处并重新构建。

| 字段 | 含义 |
| --- | --- |
| `ApiBaseUrl` | 提供 `/api/brigitte` 与 `/api/brigitte/sync` 的 HTTPS 服务根地址 |
| `Authority` | 精确 OIDC issuer，包含目标租户路径 |
| `TenantId` | 目标租户标识 |
| `ClientId` | 为桌面应用登记的原生 public client |
| `ApplicationId` | Heimdall 应用标识 |
| `Audience` | Brigitte API 的资源受众，与 ClientId 分开 |
| `Scope` | `openid profile offline_access` 及目标资源 scope |
| `RedirectUri` | 精确登记的 loopback 回调，默认 `http://127.0.0.1:51843/callback` |
| `HeimdallWebBaseUrl` | HTTPS 账号管理前端根地址 |

桌面客户端使用 `authorization_code` 与 `refresh_token`、PKCE S256，以及 `token_endpoint_auth_method=none`。不要为它配置 confidential client 的 Secret。SDK 校验 state、nonce、签名、issuer、受众、用户和会话；资源访问权限始终由服务端判断。

默认地址和 ID 是公开客户端配置，不包含服务端注册凭据。自建服务需要服务方提供兼容的 REST、WebSocket 协议及身份登记；本仓库不包含服务端部署程序。

## 依赖边界

`Desktop → Desktop.UI → Desktop.Core → Sync`，每层只引用本仓库中的项目。Sync 是客户端与服务端共享的 wire contract；修改协议版本、记录键、JSON 格式或指纹算法时，需要与服务端协调兼容性。协议说明见 [SYNC.md](../src/Asgard.Brigitte.Desktop/SYNC.md)。

界面、搜索和统计读取已提交的 SQLite 投影，命令在线提交到 API，再等待 WebSocket 同步确认。不要在 UI 中新增 REST 列表轮询、直接修改本地业务记录或自动重放写请求。

## 应用资源

运行时所需的 SVG、ICO、PNG 和托盘 alpha 数据已提交到项目中。原生资源位于 `src/Asgard.Brigitte.Desktop/Resources/AppIcon/`，共享界面资源位于 `src/Asgard.Brigitte.Desktop.UI/wwwroot/`，常规构建无需额外的设计或导出工具。更新资源时检查大小图标、深浅主题及减少动画偏好。
