# Windows MSIX 打包

简体中文 · [English](README.md) · [商店提交指南](store-submission.md)

此目录包含独立桌面仓库的 MSIX 清单模板、示例设置和 PowerShell 打包脚本。它们只生成包，不自动签名、安装或上传。便携版构建见[开发说明](../../docs/development.md)。

## 前提与状态

需要 Windows、PowerShell 7、.NET 10 SDK、`maui-windows` workload 和 Windows SDK。运行应用还需要 WebView2 Runtime，MSIX 不自动安装它。

Asgard Brigitte 已登记为 Store 产品 `9N5X2FD6C0XC`，正式公开身份见 [store-settings.public.json](store-settings.public.json)。目前产品仍为草稿，构建成功不代表商店认证和上架完成。预览包只用于检查构建链路，不能作为正式商店包提交。

## 预览构建

从仓库根目录执行：

```powershell
pwsh -NoProfile -File packaging/windows/publish-store.ps1 -Preview
```

输出位于 `publish/store-preview/1.0.0.0/`，`packages/` 下是未签名的 MSIX。预览身份为 `Asgard.Brigitte.PackagingPreview` / `CN=BrigittePackagingPreview`，不代表已登记发布者。不要直接把未签名预览包当作可安装产品分发。

## 正式构建

本产品可直接使用 `store-settings.public.json`；为其他已授权产品构建时，将示例复制到被 Git 忽略的本地设置文件并填写真实资料：

| 字段 | 来源 |
| --- | --- |
| `identityName` | Partner Center 的 Package/Identity/Name |
| `publisher` | Partner Center 的 Package/Identity/Publisher，保留完整 `CN=...` |
| `publisherDisplayName` | 已登记的发布者显示名称 |
| `version` | 四段包版本号，第一段大于 0，最后一段为 0 |
| `privacyPolicyUrl` | 发布者提供的公开 HTTPS 隐私政策 |
| `supportUrl` | 发布者提供的公开 HTTPS 支持页面 |

```powershell
pwsh -NoProfile -File packaging/windows/publish-store.ps1 -SettingsPath packaging/windows/store-settings.public.json
```

输出位于 `publish/store/<version>/packages/`。脚本将隐私和支持地址写入程序集，应用登录前即可访问“关于 → 隐私与数据”。URL 校验只检查格式，需要发布者另行确认页面内容与可访问性。

脚本禁用自动签名，带上 .NET 与 Windows App SDK，使用 `SideloadOnly` 生成 MSIX。包声明 `en-US` 和 `zh-CN`。Microsoft Store 会对接受的商店包签名，无需为提交 MSIX 专门购买证书；直接侧载安装则需要相应签名和信任配置。不要把未签名上传包当作普通安装器，也不要提交证书、密码、审核账号或真实用户资料到本仓库。

## 发行验收

构建成功后，在隔离设备或测试账号下检查安装、升级、卸载，浏览器登录与会话恢复，单实例聚焦，托盘、通知与窗口行为，附件操作，隐私和支持链接。也需检查 WebView2 缺失提示、不同显示缩放与键盘操作。

默认本机使用无需账号、联网或部署服务。商品页应将它与可选在线协作区分，在线注册、邀请和项目权限仍由服务策略决定。核心本机审核流程见[审核步骤](reviewer-notes.en.txt)，可选在线检查继续提供专用私密测试账号。正式[中文隐私政策](../../PRIVACY.zh-CN.md)及 [English](../../PRIVACY.md)、双语商品介绍和审核步骤见[提交指南](store-submission.md)。
