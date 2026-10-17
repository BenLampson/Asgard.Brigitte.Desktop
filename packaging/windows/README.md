# Windows MSIX 打包

此目录包含独立桌面仓库的 MSIX 清单模板、示例设置和 PowerShell 打包脚本。它们只生成包，不自动签名、安装或上传。便携版构建见[开发说明](../../docs/development.md)。

## 前提与状态

需要 Windows、PowerShell 7、.NET 10 SDK、`maui-windows` workload 和 Windows SDK。运行应用还需要 WebView2 Runtime，MSIX 不自动安装它。

当前未在此仓库提供正式 Partner Center 身份、签名证书、已上架应用或商店认证结论。预览包只用于检查构建链路，不能作为正式商店包提交。

## 预览构建

从仓库根目录执行：

```powershell
pwsh -NoProfile -File packaging/windows/publish-store.ps1 -Preview
```

输出位于 `publish/store-preview/1.0.0.0/`，`packages/` 下是未签名的 MSIX。预览身份为 `Asgard.Brigitte.PackagingPreview` / `CN=BrigittePackagingPreview`，不代表已登记发布者。不要直接把未签名预览包当作可安装产品分发。

## 正式构建

将 `store-settings.example.json` 复制到被 Git 忽略的 `publish/store-settings.json`，填写真实发布资料：

| 字段 | 来源 |
| --- | --- |
| `identityName` | Partner Center 的 Package/Identity/Name |
| `publisher` | Partner Center 的 Package/Identity/Publisher，保留完整 `CN=...` |
| `publisherDisplayName` | 已登记的发布者显示名称 |
| `version` | 四段包版本号，第一段大于 0，最后一段为 0 |
| `privacyPolicyUrl` | 发布者提供的公开 HTTPS 隐私政策 |
| `supportUrl` | 发布者提供的公开 HTTPS 支持页面 |

```powershell
pwsh -NoProfile -File packaging/windows/publish-store.ps1 -SettingsPath publish/store-settings.json
```

输出位于 `publish/store/<version>/packages/`。脚本将隐私和支持地址写入程序集，应用登录前即可访问“关于 → 隐私与数据”。URL 校验只检查格式，需要发布者另行确认页面内容与可访问性。

脚本禁用自动签名，带上 .NET 与 Windows App SDK，使用 `SideloadOnly` 生成 MSIX。签名、侧载安装、商店提交及认证属于发行流程，应使用自己的正式身份和渠道。不要提交证书、密码、审核账号或真实用户资料到本仓库。

## 发行验收

构建成功后，在隔离设备或测试账号下检查安装、升级、卸载，浏览器登录与会话恢复，单实例聚焦，托盘、通知与窗口行为，附件操作，隐私和支持链接。也需检查 WebView2 缺失提示、不同显示缩放与键盘操作。

公众服务的注册、邀请和项目权限由实际服务策略决定。商品页应准确说明账号、联网与服务依赖，需登录的审核流程应提供可用的测试账号。应用内[数据说明](../../docs/data.md)描述工程实现，正式隐私政策需由发行者结合实际服务提供。